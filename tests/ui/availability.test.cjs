const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const tick = () => new Promise(resolve => setImmediate(resolve));
const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js'), 'utf8');

function setup({ shared = false, pending = false } = {}) {
    const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
    const w = dom.window;
    w.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
    w.HTMLDialogElement.prototype.close = function () { this.dispatchEvent(new w.Event('close')); };
    w.confirm = () => true;
    const calls = [];
    const rows = [
        { id: 21, seasonNumber: 1, episodeNumber: 1, title: '<img src=x>', statusKey: 'available', statusLabel: 'Available', reason: 'A file is in the library.', hasFile: true },
        { id: 22, seasonNumber: 1, episodeNumber: 2, title: 'Second', statusKey: 'missing', statusLabel: 'Missing', reason: 'No file.', canSearch: !shared },
        { id: 23, seasonNumber: 1, episodeNumber: 3, title: 'Third', statusKey: 'downloading', statusLabel: 'Downloading', percent: 50, reason: 'Wait for import.' },
        { id: 24, seasonNumber: 1, episodeNumber: 4, title: 'Fourth', statusKey: 'unaired', statusLabel: 'Not aired yet', reason: 'Not aired.' },
        { id: 25, seasonNumber: 2, episodeNumber: 1, title: 'Other season', statusKey: 'missing', statusLabel: 'Missing', reason: 'No file.', canSearch: !shared }
    ];
    let searched = false;
    w.ApiClient = {
        getUrl: value => value,
        ajax: async options => {
            calls.push(options);
            if (options.type === 'POST') { searched = true; return { message: 'Search started for 1 missing episode.' }; }
            if (options.url.endsWith('/downloads')) return { isTv: true, canChooseEpisodes: !shared,
                episodes: rows, seasons: [{ seasonNumber: 1, episodeCount: 4, fileCount: 1 }] };
            if (pending) return { isTv: true, statusLabel: 'Awaiting approval', reason: 'This request needs approval.', episodes: [] };
            return { isTv: true, statusLabel: 'Episode availability', reason: shared ? 'Only its requester or a manager can search missing episodes.' : 'Missing episodes can be searched.',
                canChooseEpisodes: !shared, canSearchMissing: !shared,
                episodes: rows.map(e => searched && e.id === 22 ? { ...e, canSearch: false, statusLabel: 'Search requested recently' } : e) };
        }
    };
    w.eval(script.replace('    window.jellySeerrModal = {',
        '    clientSettingsCache = { seerrUserId: 7 }; window.testAvailability = openAvailability; window.jellySeerrModal = {'));
    w.testAvailability({ mediaInfo: { requests: [{ id: 42, status: pending ? 1 : 2, requestedBy: { id: shared ? 8 : 7 } }] } }, 'tv');
    return { w, calls, q: selector => w.document.querySelector(selector), close: () => dom.window.close() };
}

test('episode overview shows per-season states and renders titles as plain text', async () => {
    const f = setup();
    try {
        await tick();
        assert.equal(f.w.document.querySelectorAll('.bst-episode-card').length, 4);
        assert.equal(f.q('.bst-episode-card img'), null);
        assert.match(f.q('[data-episodes]').textContent, /Downloading · 50%/);
        assert.match(f.q('[data-episodes]').textContent, /Not aired yet/);
        assert.equal(f.w.document.querySelectorAll('[data-episodes] input').length, 1);
        f.q('[data-season]').value = '2';
        f.q('[data-season]').dispatchEvent(new f.w.Event('change'));
        assert.equal(f.w.document.querySelectorAll('.bst-episode-card').length, 1);
        assert.match(f.q('[data-episodes]').textContent, /Other season/);
    } finally { f.close(); }
});

test('missing search confirms and submits only selected missing episodes', async () => {
    const f = setup();
    try {
        await tick();
        f.w.confirm = () => false;
        f.q('[data-search-missing]').click();
        await tick();
        assert.equal(f.calls.filter(c => c.type === 'POST').length, 0);
        f.w.confirm = text => { assert.match(text, /Existing files will not be deleted/); return true; };
        f.q('[data-search-missing]').click();
        await tick();
        const posts = f.calls.filter(c => c.type === 'POST');
        assert.equal(posts.length, 1);
        assert.ok(posts[0].url.endsWith('/request/42/search-missing'));
        assert.deepEqual(JSON.parse(posts[0].data), { seasonNumber: 1, episodeIds: [22] });
        assert.match(f.q('[data-message]').textContent, /Search started/);
        assert.equal(f.q('[data-search-missing]').disabled, true);
    } finally { f.close(); }
});

test('shared requests remain viewable but cannot search individual missing episodes', async () => {
    const f = setup({ shared: true });
    try {
        await tick();
        assert.equal(f.w.document.querySelectorAll('.bst-episode-card').length, 4);
        assert.equal(f.q('[data-missing-controls]').hidden, true);
        assert.equal(f.q('[data-episodes] input'), null);
        assert.equal(f.q('[data-episodes] button'), null);
        assert.match(f.q('[data-reason]').textContent, /Only its requester/);
    } finally { f.close(); }
});

test('pending request explains approval without offering episode actions', async () => {
    const f = setup({ pending: true });
    try {
        await tick();
        assert.match(f.q('[data-message]').textContent, /Awaiting approval/);
        assert.equal(f.q('[data-season-label]').hidden, true);
        assert.equal(f.q('[data-search-missing]').disabled, true);
        assert.equal(f.q('.bst-episode-card'), null);
    } finally { f.close(); }
});

test('episode action opens Advanced download with the matching episode selected', async () => {
    const f = setup();
    try {
        await tick();
        const row = f.q('.bst-episode-card[data-status="available"]');
        row.querySelector('button').click();
        await tick();
        assert.equal(f.q('#bst-availability-heading'), null);
        assert.ok(f.q('#bst-release-heading'));
        assert.equal(f.q('[data-episode]').value, '21');
        assert.equal(f.q('[data-mode]').value, 'episode');
    } finally { f.close(); }
});

test('deselecting missing episodes disables the search button', async () => {
    const f = setup();
    try {
        await tick();
        const check = f.q('[data-episodes] input');
        check.checked = false;
        check.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.q('[data-search-missing]').disabled, true);
        assert.match(f.q('[data-selection]').textContent, /0 missing/);
    } finally { f.close(); }
});
