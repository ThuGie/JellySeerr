const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js'), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
const deferred = () => { let resolve; const promise = new Promise(r => { resolve = r; }); return { promise, resolve }; };

function setup() {
    const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
    const w = dom.window;
    w.ApiClient = { getCurrentUserId: () => 'one', getUrl: p => p, ajax: async () => ({}) };
    w.Dashboard = { alert: () => assert.fail('Unexpected error dialog') };
    w.eval(script.replace('    window.jellySeerrModal = {', `
        window.loadingTest = { settings: loadClientSettings, load: loadModalDetails, fetch: fetchTmdbJson };
        buildDetailsDom = function(data) { const root = document.createElement('div'); root.dataset.title = data.title; return root; };
        window.jellySeerrModal = {`));
    return { dom, w, close: () => dom.window.close() };
}

test('concurrent settings requests share one fetch, cache briefly and refresh on user change', async () => {
    const f = setup();
    try {
        let calls = 0;
        f.w.ApiClient.ajax = async () => { calls++; return { seerrUserId: calls }; };
        await Promise.all(Array.from({ length: 10 }, () => f.w.loadingTest.settings()));
        await f.w.loadingTest.settings();
        assert.equal(calls, 1);
        f.w.ApiClient.getCurrentUserId = () => 'two';
        const changed = await f.w.loadingTest.settings();
        assert.equal(calls, 2);
        assert.equal(changed.seerrUserId, 2);
    } finally { f.close(); }
});

test('quota fetches are shared and cannot reuse the previous signed-in user', async () => {
    const f = setup();
    try {
        let calls = 0;
        f.w.ApiClient.ajax = async () => ({ used: ++calls });
        await Promise.all(Array.from({ length: 10 }, () => f.w.jellySeerrQuota.fetch()));
        assert.equal(calls, 1);
        f.w.ApiClient.getCurrentUserId = () => 'two';
        assert.equal((await f.w.jellySeerrQuota.fetch()).used, 2);
    } finally { f.close(); }
});

test('TMDB metadata fetches are shared and failures can be retried', async () => {
    const f = setup();
    try {
        let calls = 0;
        f.w.fetch = async () => { calls++; return { ok: true, json: async () => ({ id: 1 }) }; };
        await Promise.all(Array.from({ length: 10 }, () => f.w.loadingTest.fetch('https://tmdb.test/1', 'key')));
        assert.equal(calls, 1);
        await f.w.loadingTest.fetch('https://tmdb.test/1', 'different-key');
        assert.equal(calls, 2);
        f.w.fetch = async () => { calls++; throw new Error('temporary'); };
        await assert.rejects(f.w.loadingTest.fetch('https://tmdb.test/2', 'key'));
        f.w.fetch = async () => { calls++; return { ok: true, json: async () => ({ id: 2 }) }; };
        assert.equal((await f.w.loadingTest.fetch('https://tmdb.test/2', 'key')).id, 2);
        assert.equal(calls, 4);
    } finally { f.close(); }
});

test('title data resolves while optional logos and backdrop are still pending', async () => {
    const f = setup();
    try {
        const artwork = deferred();
        const details = deferred();
        const calls = [];
        f.w.ApiClient.ajax = options => {
            calls.push(options.url);
            if (options.url.includes('client-settings')) return Promise.resolve({ tmdbApiKey: 'key' });
            if (options.url.includes('backdrop/')) return artwork.promise;
            return details.promise;
        };
        f.w.fetch = url => url.includes('/images') ? artwork.promise : Promise.resolve({ ok: true, json: async () => ({ id: 1, title: 'Title' }) });
        let loaded = false;
        const work = f.w.loadingTest.load(1, 'movie').then(result => { loaded = true; return result; });
        assert.ok(calls.some(c => c.includes('/details/')));
        assert.ok(calls.some(c => c.includes('/client-settings')));
        details.resolve({ id: 1, mediaInfo: { status: 5 } });
        await tick();
        assert.equal(loaded, true);
        const result = await work;
        assert.equal(result.title, 'Title');
        assert.equal(result.mediaInfo.status, 5);
        assert.ok(result._artworkPromise);
        artwork.resolve({ ok: true, json: async () => ({ logos: [{ file_path: '/logo.png', iso_639_1: 'en' }] }) });
        await result._artworkPromise;
    } finally { f.close(); }
});

test('slow previous modal cannot overwrite a newer title or reopen after close', async () => {
    const f = setup();
    try {
        const first = deferred();
        const third = deferred();
        f.w.ApiClient.ajax = options => {
            if (options.url.includes('client-settings')) return Promise.resolve({});
            if (options.url.endsWith('/1')) return first.promise;
            if (options.url.endsWith('/3')) return third.promise;
            return Promise.resolve({ id: 2, title: 'Second' });
        };
        f.w.jellySeerrModal.open(1, 'movie');
        f.w.jellySeerrModal.open(2, 'movie');
        await tick();
        assert.equal(f.w.document.querySelector('[data-title]').dataset.title, 'Second');
        first.resolve({ id: 1, title: 'First' });
        await tick();
        assert.equal(f.w.document.querySelector('[data-title]').dataset.title, 'Second');
        f.w.jellySeerrModal.open(3, 'movie');
        f.w.jellySeerrModal.close();
        third.resolve({ id: 3, title: 'Third' });
        await tick();
        assert.equal(f.w.document.body.children.length, 0);
    } finally { f.close(); }
});
