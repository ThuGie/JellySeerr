const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const source = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-requests.js'), 'utf8');
const tick = () => new Promise(r => setImmediate(r));
function fixture(manager = true) {
    const dom = new JSDOM('<div id="requests"></div>', { runScripts: 'outside-only' });
    const w = dom.window, calls = [], opened = [];
    w.ApiClient = { getUrl: p => p, ajax: async o => { calls.push(o); return {}; } };
    w.jellySeerrModal = { open: (...args) => opened.push(args) };
    w.eval(source.replace("    if (document.readyState === 'loading') {", `
        window.testRequests = { state, renderModerationActions, bindContainerEvents };
        loadRequests = () => { window.refreshes = (window.refreshes || 0) + 1; };
        refreshQuotaChip = () => {};
        return;
        if (document.readyState === 'loading') {`));
    w.testRequests.state.clientSettings = { canManageRequests: manager };
    const container = w.document.querySelector('#requests');
    container.innerHTML = '<div class="jellySeerr-requests-action-message" role="status"></div><article class="jellySeerr-request-box" data-tmdb-id="12" data-media-type="tv">'
        + w.testRequests.renderModerationActions({ id: 78, isPending: true }) + '</article>';
    w.testRequests.bindContainerEvents(container);
    return { w, container, calls, opened, close: () => w.close() };
}
test('only managers see actions and only on pending requests', () => {
    for (const manager of [true, false]) {
        const f = fixture(manager);
        try {
            assert.equal(f.container.querySelectorAll('button').length, manager ? 2 : 0);
            assert.equal(f.w.testRequests.renderModerationActions({ id: 78, isPending: false }), '');
        } finally { f.close(); }
    }
});
for (const action of ['approve', 'decline']) {
    test(action + ' posts exact request, blocks duplicates and refreshes without opening details', async () => {
        const f = fixture();
        try {
            let finish;
            f.w.ApiClient.ajax = o => { f.calls.push(o); return new Promise(r => { finish = r; }); };
            const button = f.container.querySelector('[data-request-action="' + action + '"]');
            button.click(); button.click();
            assert.equal(f.calls.length, 1);
            assert.equal(f.calls[0].url, 'JellySeerr/request/78/' + action);
            assert.equal(f.calls[0].type, 'POST');
            assert.ok([...f.container.querySelectorAll('button')].every(b => b.disabled));
            assert.equal(f.opened.length, 0);
            finish({}); await tick();
            assert.equal(f.w.refreshes, 1);
            assert.match(f.container.textContent, action === 'approve' ? /Request approved/ : /Request declined/);
        } finally { f.close(); }
    });
}
test('failed actions show server message safely and restore buttons', async () => {
    const f = fixture();
    try {
        f.w.ApiClient.ajax = async () => { throw { clone: () => ({ json: async () => ({ message: '<b>Permission denied</b>' }) }) }; };
        f.container.querySelector('button').click(); await tick();
        assert.match(f.container.textContent, /<b>Permission denied<\/b>/);
        assert.equal(f.container.querySelector('b'), null);
        assert.ok([...f.container.querySelectorAll('button')].every(b => !b.disabled));
        assert.equal(f.w.refreshes, undefined);
    } finally { f.close(); }
});
test('keyboard on nested actions does not activate the details card', () => {
    const f = fixture();
    try {
        f.container.querySelector('button').dispatchEvent(new f.w.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        assert.equal(f.opened.length, 0);
        assert.equal(f.calls.length, 0);
    } finally { f.close(); }
});
