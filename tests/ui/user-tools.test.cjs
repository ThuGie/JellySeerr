const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-user-tools.js'), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
function fixture(handler) {
    const dom = new JSDOM('<!doctype html><body></body>', { runScripts: 'outside-only' });
    const w = dom.window, calls = [];
    w.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
    w.HTMLDialogElement.prototype.close = function () { this.open = false; this.dispatchEvent(new w.Event('close')); };
    w.confirm = () => true;
    w.ApiClient = { getUrl: p => p, ajax: async options => { calls.push(options); return handler(options); } };
    w.eval(script);
    return { w, calls, tools: w.jellySeerrUserTools, q: s => w.document.querySelector(s), buttons: () => [...w.document.querySelectorAll('button')], close: () => w.close() };
}

test('notification preferences submit event masks without account IDs or credentials', async () => {
    const f = fixture(o => o.type === 'POST' ? { message: 'Saved' } : { channels: [
        { id: 'email', enabled: true, linked: true, types: 8, allowedTypes: 8190 },
        { id: 'telegram', enabled: true, linked: false, types: 0, allowedTypes: 8190 }] });
    try {
        await f.tools.notifications();
        assert.equal(f.q('input[data-channel="telegram"]').disabled, true);
        f.q('input[data-bit="4"]').checked = true;
        f.buttons().find(b => b.textContent === 'Save preferences').click(); await tick();
        const payload = JSON.parse(f.calls.find(c => c.type === 'POST').data);
        assert.deepEqual(payload, { notificationTypes: { email: 12 } });
        assert.match(f.w.document.body.textContent, /Saved/);
    } finally { f.close(); }
});

test('calendar displays dates and filters titles safely and navigates to details', async () => {
    const f = fixture(() => ({ message: 'Dates are estimates.', events: [
        { date: '2026-10-24', title: '<img src=x>', label: 'Episode', mediaType: 'tv', tmdbId: 123, requestStatus: 2 },
        { date: '2026-10-25', title: 'Film', label: 'Digital release', mediaType: 'movie', tmdbId: 456, requestStatus: 2 }] }));
    try {
        let opened; f.w.jellySeerrModal = { open: (...args) => { opened = args; } };
        f.tools.calendar(); await tick();
        assert.equal(f.q('img'), null);
        const filter = f.q('input[type="search"]'); filter.value = 'film'; filter.dispatchEvent(new f.w.Event('input'));
        assert.equal(f.w.document.querySelectorAll('article').length, 1);
        f.buttons().find(b => b.textContent === 'Film').click();
        assert.deepEqual(opened, [456, 'movie']); assert.equal(f.q('dialog'), null);
        assert.match(f.calls[0].url, /calendar\?start=\d{4}-\d{2}-01&end=/);
    } finally { f.close(); }
});

test('issue reporting sends category and episode and supports comments on existing reports', async () => {
    const f = fixture(o => o.type === 'POST' ? { message: 'Saved' } : { issues: [{ id: 9, status: 1, issueType: 2, comments: [{ message: '<script>bad()</script>', author: 'Alice' }] }] });
    try {
        f.tools.issues(77, 'tv'); await tick();
        assert.equal(f.q('script'), null);
        f.q('select').value = '3'; f.q('input[aria-label="Problem season"]').value = '2'; f.q('input[aria-label="Problem episode"]').value = '4';
        f.q('textarea[aria-label="Problem description"]').value = 'Subtitle timing is wrong.';
        f.buttons().find(b => b.textContent === 'Report problem').click(); await tick();
        assert.deepEqual(JSON.parse(f.calls.find(c => c.url === 'JellySeerr/issue').data), { mediaId: 77, issueType: 3, message: 'Subtitle timing is wrong.', problemSeason: 2, problemEpisode: 4 });
        f.q('textarea[aria-label="Comment on issue 9"]').value = 'Same problem here';
        f.buttons().find(b => b.textContent === 'Add comment').click(); await tick();
        assert.ok(f.calls.some(c => c.url === 'JellySeerr/issues/9/comment' && JSON.parse(c.data).message === 'Same problem here'));
    } finally { f.close(); }
});

test('recovery needs confirmation and sends only the opaque selection', async () => {
    const f = fixture(o => o.type === 'POST' ? { message: 'Recovery started.' } : { message: 'Partial data will be removed.', downloads: [{ token: 'opaque', title: 'Bad.Release', scope: 'Whole season 1' }] });
    try {
        f.tools.requestTool([{ id: 42, type: 'tv' }], 'recovery'); await tick();
        f.w.confirm = () => false;
        f.buttons().find(b => b.textContent.startsWith('Remove,')).click(); await tick();
        assert.equal(f.calls.filter(c => c.type === 'POST').length, 0);
        let prompt = ''; f.w.confirm = text => { prompt = text; return true; };
        f.buttons().find(b => b.textContent.startsWith('Remove,')).click(); await tick();
        assert.match(prompt, /Whole season 1/); assert.match(prompt, /other users/);
        assert.deepEqual(JSON.parse(f.calls.find(c => c.type === 'POST').data), { token: 'opaque' });
        assert.equal(f.w.document.querySelectorAll('article').length, 0);
        assert.match(f.w.document.body.textContent, /Recovery started/);
    } finally { f.close(); }
});

test('history displays actor, outcome and date as plain text', async () => {
    const f = fixture(() => ({ message: 'Recent activity.', events: [{ action: 'Replacement accepted', actor: 'Alice', outcome: 'Queued', date: '2026-10-24T12:00:00Z', release: '<img src=x>' }] }));
    try {
        f.tools.requestTool([{ id: 42 }], 'activity'); await tick();
        assert.match(f.w.document.body.textContent, /Alice.*<img src=x>.*Queued/);
        assert.equal(f.q('img'), null);
    } finally { f.close(); }
});

test('comparison distinguishes inspected values from inference and unknown candidate fields', () => {
    const f = fixture(() => ({}));
    try {
        f.tools.compare({ videoCodec: 'H.264', audioCodec: 'AAC', size: 1073741824 }, { title: 'Movie.1080p.HEVC.DV.DDP', quality: 'WEB-1080p', size: 2147483648 });
        assert.match(f.w.document.body.textContent, /inferred from the release name/);
        assert.match(f.w.document.body.textContent, /H.264HEVC/);
        assert.match(f.w.document.body.textContent, /Dolby Vision/);
        assert.match(f.w.document.body.textContent, /1.00 GB2.00 GB/);
        assert.match(f.w.document.body.textContent, /Unknown/);
    } finally { f.close(); }
});

test('closing a tool while loading cannot repopulate its dialog', async () => {
    let resolve;
    const f = fixture(() => new Promise(r => { resolve = r; }));
    try {
        const pending = f.tools.notifications();
        f.buttons().find(b => b.textContent === 'Close').click();
        resolve({ channels: [] }); await pending;
        assert.equal(f.q('dialog'), null); assert.equal(f.w.document.body.children.length, 0);
    } finally { f.close(); }
});
