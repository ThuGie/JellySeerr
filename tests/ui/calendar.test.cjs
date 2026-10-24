const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const root = '../../src/Jellyfin.Plugin.JellySeerr/Inject/';
const calendar = fs.readFileSync(path.join(__dirname, root + 'jellyseerr-calendar.js'), 'utf8');
const tabs = fs.readFileSync(path.join(__dirname, root + 'jellyseerr-tabs.js'), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
function data() {
    const date = new Date(); date.setDate(15); date.setHours(20, 0, 0, 0);
    return { message: 'All monitored series.', events: [
        { title: 'Shared Show', tmdbId: 123, episodeTitle: 'New episode', seasonNumber: 2, episodeNumber: 3, airDateUtc: date.toISOString(), hasFile: false, server: 'Main' },
        { title: '<img src=x>', tmdbId: 456, episodeTitle: 'Already available', seasonNumber: 1, episodeNumber: 4, airDateUtc: date.toISOString(), hasFile: true, server: '4K' }
    ], awaitingDates: [{ title: 'Waiting Show', tmdbId: 789, server: 'Main' }], failures: [] };
}
function fixture({ navigation = false, error = null } = {}) {
    const html = navigation ? '<header class="skinHeader"><div class="headerRight"></div></header><div class="headerTabs"><div is="emby-tabs"><div class="emby-tabs-slider"><button class="emby-tab-button" data-index="0">Home</button><button class="emby-tab-button" data-index="1">Favorites</button></div></div></div><div id="indexPage" class="page"><div id="homeTab" class="tabContent is-active" data-index="0"></div><div id="favoritesTab" class="tabContent" data-index="1"></div></div>' : '<div class="jellySeerr-calendar-sections"></div>';
    const dom = new JSDOM(html, { runScripts: 'outside-only', pretendToBeVisual: true, url: 'https://jellyfin.test/web/#/home.html' });
    const w = dom.window, calls = [];
    w.ApiClient = { getUrl: p => p, ajax: async options => { calls.push(options); if (error) throw error; return data(); } };
    w.eval(calendar);
    if (navigation) {
        w.eval(tabs.replace('window.jellySeerrPlugin.init();', '/* automatic boot disabled in test */'));
        const plugin = w.jellySeerrPlugin;
        plugin.isElementLaidOut = el => !!el;
        plugin.isContainerVisible = el => !!el && el.closest('.tabContent')?.classList.contains('is-active');
        const config = { tabs: plugin.getDefaultTabConfig(), customTabs: [], tabBarOrder: plugin.normalizeBarOrder([], []) };
        plugin.loadTabConfig = async () => config;
        plugin.waitForCustomTabs = async () => {};
    }
    return { w, calls, q: s => w.document.querySelector(s), close: () => w.close() };
}

test('monitored calendar is a full page with a month grid and awaiting-date series', async () => {
    const f = fixture();
    try {
        f.w.jellySeerrCalendar.mount(f.q('.jellySeerr-calendar-sections')); await tick();
        assert.equal(f.q('dialog'), null);
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-day').length, 42);
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-event').length, 2);
        assert.match(f.q('.jellySeerr-calendar-waiting').textContent, /Waiting Show/);
        assert.equal(f.q('img'), null);
        assert.match(f.calls[0].url, /calendar\/monitored\?start=/);
    } finally { f.close(); }
});

test('calendar filters instance, text and available episodes and supports agenda view', async () => {
    const f = fixture();
    try {
        f.w.jellySeerrCalendar.mount(f.q('.jellySeerr-calendar-sections')); await tick();
        const hide = f.q('input[type=checkbox]'); hide.checked = true; hide.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-event').length, 1);
        const view = f.q('select[aria-label="Calendar view"]'); view.value = 'agenda'; view.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.q('.jellySeerr-calendar-grid'), null);
        const search = f.q('input[type=search]'); search.value = 'missing'; search.dispatchEvent(new f.w.Event('input'));
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-event').length, 0);
        search.value = ''; search.dispatchEvent(new f.w.Event('input'));
        const server = f.q('select[aria-label="Sonarr instance"]'); server.value = '4K'; server.dispatchEvent(new f.w.Event('change'));
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-event').length, 0);
    } finally { f.close(); }
});

test('calendar title opens existing TV details without requiring a personal request', async () => {
    const f = fixture();
    try {
        let selected; f.w.jellySeerrModal = { open: (...args) => { selected = args; } };
        f.w.jellySeerrCalendar.mount(f.q('.jellySeerr-calendar-sections')); await tick();
        [...f.w.document.querySelectorAll('.jellySeerr-calendar-title')].find(b => b.textContent === 'Shared Show').click();
        assert.deepEqual(selected, [123, 'tv']);
    } finally { f.close(); }
});

test('calendar shows native Fetch errors and preserves server connection diagnosis', async () => {
    const f = fixture({ error: new Response(JSON.stringify({ message: 'Could not load the Sonarr connections from Seerr.' }), { status: 502 }) });
    try {
        f.w.jellySeerrCalendar.mount(f.q('.jellySeerr-calendar-sections')); await tick(); await tick();
        assert.match(f.q('[role=status]').textContent, /Sonarr connections from Seerr/);
    } finally { f.close(); }
});

test('header icon opens the native calendar page and is not duplicated on updates', async () => {
    const f = fixture({ navigation: true });
    try {
        const plugin = f.w.jellySeerrPlugin;
        await plugin.ensureNativeTabs();
        plugin.ensureCalendarHeaderIcon({ tabs: plugin.getDefaultTabConfig() });
        assert.equal(f.w.document.querySelectorAll('[data-jellySeerr-calendar-nav]').length, 1);
        const icon = f.q('[data-jellySeerr-calendar-nav]');
        assert.equal(icon.getAttribute('aria-label'), 'Release calendar');
        assert.match(icon.href, /jellySeerrTab=calendar/);
        await f.w.jellySeerrCalendar.open(); await tick();
        assert.equal(f.q('.tabContent[data-jellySeerr-tab="calendar"]').classList.contains('is-active'), true);
        assert.ok(f.q('.jellySeerr-calendar-page')); assert.equal(f.q('dialog'), null);
        assert.match(f.w.location.hash, /jellySeerrTab=calendar/);
        await plugin.selectjellySeerrTab('movies');
        assert.doesNotMatch(f.w.location.hash, /jellySeerrTab=calendar/);
        plugin.ensureCalendarHeaderIcon({ tabs: [{ id: 'calendar', enabled: false }] });
        assert.equal(f.q('[data-jellySeerr-calendar-nav]'), null);
    } finally { f.close(); }
});

test('calendar icon mounts in modern header action tray', async () => {
    const f = fixture({ navigation: true });
    try {
        const toolbar = f.w.document.createElement('div'); toolbar.className = 'MuiToolbar-root';
        const appbar = f.w.document.createElement('div'); appbar.className = 'MuiAppBar-root'; appbar.appendChild(toolbar); f.w.document.body.appendChild(appbar);
        const plugin = f.w.jellySeerrPlugin;
        plugin.ensureCalendarHeaderIcon({ tabs: plugin.getDefaultTabConfig() });
        assert.ok(toolbar.querySelector('[data-jellySeerr-calendar-nav]'));
        assert.match(toolbar.querySelector('a').href, /#\/home\?/);
    } finally { f.close(); }
});

test('bookmark restores calendar and initial home activation does not erase its route', async () => {
    const f = fixture({ navigation: true });
    try {
        f.w.history.replaceState(null, '', '#/home.html?jellySeerrTab=calendar');
        await f.w.jellySeerrPlugin.ensureNativeTabs();
        f.q('[is="emby-tabs"]').dispatchEvent(new f.w.CustomEvent('beforetabchange', { detail: { selectedTabIndex: 0 } }));
        assert.match(f.w.location.hash, /jellySeerrTab=calendar/);
        f.w.jellySeerrCalendar.ensureMounted(); await tick(); await tick();
        assert.equal(f.q('.tabContent[data-jellySeerr-tab="calendar"]').classList.contains('is-active'), true);
        assert.ok(f.q('.jellySeerr-calendar-page'));
    } finally { f.close(); }
});

test('small screens start with the agenda view', async () => {
    const f = fixture();
    try {
        f.w.matchMedia = () => ({ matches: true });
        f.w.jellySeerrCalendar.mount(f.q('.jellySeerr-calendar-sections')); await tick();
        assert.equal(f.q('select[aria-label="Calendar view"]').value, 'agenda');
        assert.equal(f.q('.jellySeerr-calendar-grid'), null);
        assert.equal(f.w.document.querySelectorAll('.jellySeerr-calendar-event').length, 2);
    } finally { f.close(); }
});
