const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');

const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js'), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
const ownRequest = { id: 42, status: 5, requestedBy: { id: 7 } };

function fixture({ tv = false, records = [ownRequest], postError = null, searchError = null } = {}) {
    const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only', url: 'https://jellyfin.test/' });
    const w = dom.window;
    w.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
    w.HTMLDialogElement.prototype.close = function () { this.open = false; this.dispatchEvent(new w.Event('close')); };
    w.confirm = () => true;
    const calls = [];
    w.ApiClient = {
        getUrl: p => p,
        ajax: async options => {
            calls.push(options);
            const payload = options.data ? JSON.parse(options.data) : null;
            if (payload && payload.download) {
                if (postError) throw { responseJSON: { message: postError } };
                return { state: 'completed', result: { message: 'Replacement queued.' } };
            }
            if (payload && searchError) throw searchError;
            if (options.url.endsWith('/downloads')) return { isTv: tv,
                seasons: tv ? [{ seasonNumber: 1, episodeCount: 2, fileCount: 2 }] : [],
                episodes: tv ? [{ id: 21, seasonNumber: 1, episodeNumber: 1, title: 'Pilot', hasFile: true }] : [] };
            return { state: 'completed', result: {
                ...(payload && payload.seasonNumber != null ? { seasonNumber: 1, fileCount: 2,
                    affectedEpisodes: [{ seasonNumber: 1, episodeNumber: 1, title: 'Pilot' }, { seasonNumber: 1, episodeNumber: 2, title: 'Second episode' }] } : {}),
                searchToken: 'opaque-token', hasFile: true, currentRelease: 'Old.Release', canRejectCurrent: true,
                releases: [
                    { index: 0, title: '<img src=x onerror=alert(1)>', quality: '1080p', indexer: 'Indexer A', size: 1000000000, seeders: 15, selectable: true, rejections: [] },
                    { index: 1, title: 'Blocked.Release', selectable: false, rejections: ['Quality is not wanted in profile'] },
                    { index: 2, title: 'Old.Release', selectable: false, current: true, note: 'Current release', rejections: [] }
                ]
            } };
        }
    };
    // Expose the internal picker only in this test's isolated script evaluation.
    w.eval(script.replace('    window.jellySeerrModal = {',
        '    clientSettingsCache = { seerrUserId: 7 }; window.testPicker = openReleasePicker; window.jellySeerrModal = {'));
    w.testPicker({ mediaInfo: { requests: records } }, tv ? 'tv' : 'movie');
    return { dom, w, calls, q: s => w.document.querySelector(s), close: () => dom.window.close() };
}

test('normal requester sees safe release details, current label, rejected toggle and filter', async () => {
    const f = fixture();
    try {
        await tick();
        assert.equal(f.q('[data-episode-label]').hidden, true);
        f.q('[data-search]').click();
        await tick();
        assert.equal(f.w.document.querySelectorAll('.bst-release-row').length, 2);
        assert.equal(f.q('.bst-release-row img'), null);
        assert.match(f.q('.bst-release-row').textContent, /15 seeders/);
        assert.equal(f.q('[data-reject]').checked, true);
        const show = f.q('[data-show-rejected]');
        show.checked = true;
        show.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.w.document.querySelectorAll('.bst-release-row').length, 3);
        assert.match(f.w.document.body.textContent, /Quality is not wanted/);
        const filter = f.q('[data-filter]');
        filter.value = 'blocked';
        filter.dispatchEvent(new f.w.Event('input'));
        assert.equal(f.w.document.querySelectorAll('.bst-release-row').length, 1);
        assert.equal(f.q('.bst-release-row button').disabled, true);
    } finally { f.close(); }
});

test('request selector excludes other owners and requests awaiting approval', async () => {
    const f = fixture({ records: [ownRequest, { id: 43, status: 2, requestedBy: { id: 8 } }, { id: 44, status: 1, requestedBy: { id: 7 } }] });
    try {
        await tick();
        assert.equal(f.q('[data-request]').options.length, 1);
        assert.equal(f.q('[data-request]').value, '42');
    } finally { f.close(); }
});

test('TV picker searches the selected episode', async () => {
    const f = fixture({ tv: true });
    try {
        await tick();
        assert.equal(f.q('[data-episode-label]').hidden, false);
        assert.match(f.q('[data-episode]').textContent, /S1 E1/);
        f.q('[data-search]').click();
        await tick();
        assert.ok(f.calls.some(c => c.data && JSON.parse(c.data).episodeId === 21));
    } finally { f.close(); }
});

test('selection sends only the opaque result identity and replacement preference', async () => {
    const f = fixture();
    try {
        await tick();
        f.q('[data-search]').click();
        await tick();
        f.q('.bst-release-row button').click();
        await tick();
        const posts = f.calls.filter(c => c.type === 'POST' && JSON.parse(c.data).download);
        assert.equal(posts.length, 1);
        assert.deepEqual(JSON.parse(posts[0].data).download, { searchToken: 'opaque-token', releaseIndex: 0, rejectCurrent: true });
        assert.match(f.q('[data-message]').textContent, /Replacement queued/);
        assert.equal(f.q('.bst-release-row'), null);
    } finally { f.close(); }
});

test('failed grab shows the server message and clears stale download buttons', async () => {
    const f = fixture({ postError: 'The file changed. Search again.' });
    try {
        await tick();
        f.q('[data-search]').click();
        await tick();
        f.q('.bst-release-row button').click();
        await tick();
        assert.match(f.q('[data-message]').textContent, /The file changed/);
        assert.equal(f.q('.bst-release-row'), null);
        assert.equal(f.q('[data-search]').disabled, false);
    } finally { f.close(); }
});

test('unapproved titles explain the prerequisite without searching', async () => {
    const f = fixture({ records: [] });
    try {
        await tick();
        assert.equal(f.calls.length, 0);
        assert.equal(f.q('[data-search]').disabled, true);
        assert.match(f.q('[data-message]').textContent, /Request this title first/);
    } finally { f.close(); }
});

test('Escape closes only the release picker', async () => {
    const f = fixture();
    try {
        await tick();
        let parentClosed = false;
        f.w.document.addEventListener('keydown', () => { parentClosed = true; });
        f.q('dialog').dispatchEvent(new f.w.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        assert.equal(f.q('dialog'), null);
        assert.equal(parentClosed, false);
    } finally { f.close(); }
});

test('another requester can select a whole season and sees affected episodes before confirmation', async () => {
    const f = fixture({ tv: true, records: [{ id: 43, status: 5, requestedBy: { id: 8, displayName: 'Other viewer' }, seasons: [{ seasonNumber: 1 }] }] });
    try {
        await tick();
        assert.equal(f.q('[data-request]').options.length, 1);
        assert.match(f.q('[data-request]').textContent, /Other viewer/);
        assert.equal(f.q('[data-mode]').value, 'season');
        assert.equal(f.q('[data-mode] option[value="episode"]').disabled, true);
        assert.equal(f.q('[data-episode-label]').hidden, true);
        assert.equal(f.q('[data-season-label]').hidden, false);
        f.q('[data-search]').click();
        await tick();
        assert.ok(f.calls.some(c => c.data && JSON.parse(c.data).seasonNumber === 1));
        assert.match(f.q('[data-affected]').textContent, /S1 E2 — Second episode/);
        let confirmation = '';
        f.w.confirm = text => { confirmation = text; return true; };
        f.q('.bst-release-row button').click();
        await tick();
        assert.match(confirmation, /whole of season 1/);
        assert.match(confirmation, /2 existing files/);
        assert.match(confirmation, /S1 E1, S1 E2/);
        const post = f.calls.find(c => c.type === 'POST' && JSON.parse(c.data).download);
        assert.ok(post.url.includes('/request/43/'));
        assert.equal(JSON.parse(post.data).download.seasonNumber, 1);
    } finally { f.close(); }
});

test('own TV request can switch between episode and season without reusing old search results', async () => {
    const f = fixture({ tv: true });
    try {
        await tick();
        f.q('[data-search]').click();
        await tick();
        assert.ok(f.q('.bst-release-row'));
        const mode = f.q('[data-mode]');
        mode.value = 'season';
        mode.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.q('.bst-release-row'), null);
        f.q('[data-search]').click();
        await tick();
        assert.ok(f.calls.some(c => c.data && JSON.parse(c.data).seasonNumber === 1));
        f.w.confirm = () => false;
        f.q('.bst-release-row button').click();
        await tick();
        assert.equal(f.calls.filter(c => c.type === 'POST' && JSON.parse(c.data).download).length, 0);
        mode.value = 'episode';
        mode.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.q('[data-episode-label]').hidden, false);
        assert.equal(f.q('[data-season-label]').hidden, true);
        assert.equal(f.q('.bst-release-row'), null);
    } finally { f.close(); }
});

test('real Fetch error responses display the server diagnosis instead of the generic download fallback', async () => {
    const f = fixture({ searchError: new Response(JSON.stringify({ message: 'Sonarr/Radarr episodefile lookup returned HTTP 404.' }), { status: 502 }) });
    try {
        await tick(); f.q('[data-search]').click(); await tick(); await tick();
        assert.match(f.q('[data-message]').textContent, /episodefile lookup returned HTTP 404/);
        assert.doesNotMatch(f.q('[data-message]').textContent, /Could not complete the download operation/);
        assert.equal(f.q('[data-search]').disabled, false);
        assert.equal(f.q('.bst-release-row'), null);
    } finally { f.close(); }
});

test('proxy error pages show HTTP status without rendering HTML', async () => {
    const f = fixture({ searchError: new Response('<html><h1>Gateway timeout</h1></html>', { status: 504 }) });
    try {
        await tick(); f.q('[data-search]').click(); await tick(); await tick();
        assert.match(f.q('[data-message]').textContent, /Release search failed \(HTTP 504\)/);
        assert.match(f.q('[data-message]').textContent, /proxy returned an error page/);
        assert.equal(f.q('h1'), null);
    } finally { f.close(); }
});

test('long search polls a short status endpoint and renders eventual releases', async () => {
    const f = fixture();
    try {
        await tick();
        let polls = 0;
        f.w.ApiClient.ajax = async options => {
            f.calls.push(options);
            if (options.type === 'POST') return { state: 'running', jobToken: 'job-123', elapsedSeconds: 0 };
            polls++;
            if (polls === 1) return { state: 'running', jobToken: 'job-123', elapsedSeconds: 70 };
            return { state: 'completed', result: { searchToken: 'selection', hasFile: false, releases: [{ index: 0, title: 'New.Release', selectable: true }] } };
        };
        f.w.setTimeout = callback => { queueMicrotask(callback); return 1; };
        f.q('[data-search]').click(); await tick();
        assert.equal(polls, 2);
        assert.ok(f.calls.some(c => c.url.endsWith('/release-jobs/job-123')));
        assert.match(f.q('.bst-release-row').textContent, /New.Release/);
    } finally { f.close(); }
});
