const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-tabs.js'), 'utf8');
function fixture(cleanupFirst = false, api = true) {
    const names = ['home', 'favorites', 'movies', 'tv', 'requests', 'calendar', 'spotify'];
    const buttons = names.map((name, i) => `<button class="emby-tab-button" data-index="${i}" ${i >= 2 && i <= 5 ? `data-jellySeerr-tab="${name}"` : ''} ${i === 6 ? 'data-jellyspot-tab="browse"' : ''}>${name}</button>`).join('');
    const panels = names.map((name, i) => `<div class="tabContent pageTabContent" data-index="${i}" ${i >= 2 && i <= 5 ? `data-jellySeerr-tab="${name}"` : ''} ${i === 6 ? 'data-jellyspot-tab="browse"' : ''}>${name}</div>`).join('');
    const dom = new JSDOM(`<style>.hide { display:none !important } #indexPage.jellyspot-is-open > .tabContent:not([data-jellyspot-tab]) { display:none !important }</style><div class="headerTabs"><div is="emby-tabs"><div class="emby-tabs-slider">${buttons}</div></div></div><div id="indexPage" class="page">${panels}</div>`, { runScripts: 'outside-only', url: 'https://jellyfin.test/web/#/home.html' });
    const w = dom.window;
    w.eval(script.replace('window.jellySeerrPlugin.init();', '/* disabled boot */'));
    const p = w.jellySeerrPlugin, page = w.document.querySelector('#indexPage'), tabs = w.document.querySelector('[is="emby-tabs"]');
    p.isHomeTabContext = () => true;
    p.onjellySeerrTabShown = () => {};
    p.updateModernNavActiveState = () => {};
    function hideSpotify() {
        page.classList.remove('jellyspot-is-open');
        page.querySelector('[data-jellyspot-tab]').classList.add('hide');
        page.querySelector('[data-jellyspot-tab]').classList.remove('is-active');
    }
    if (api) w.jellySpotPlugin = { hidePluginPanels: hideSpotify };
    const cleanup = () => tabs.addEventListener('tabchange', e => { if (e.detail.selectedTabIndex !== 6) hideSpotify(); }, true);
    if (cleanupFirst) cleanup();
    p.attachPluginTabGuard(tabs);
    if (!cleanupFirst) cleanup();
    const showSpotify = () => {
        page.classList.add('jellyspot-is-open');
        page.querySelectorAll('.tabContent').forEach(panel => {
            const active = panel.hasAttribute('data-jellyspot-tab');
            panel.classList.toggle('hide', !active);
            panel.classList.toggle('is-active', active);
        });
    };
    return { w, p, page, tabs, showSpotify, close: () => w.close() };
}
for (const cleanupFirst of [false, true]) {
    for (const index of [0, 1, 2, 3, 4, 5]) {
        test(`leaving Spotify restores tab ${index}, Spotify cleanup registered ${cleanupFirst ? 'first' : 'last'}`, () => {
            const f = fixture(cleanupFirst);
            try {
                f.showSpotify();
                for (const event of ['beforetabchange', 'tabchange']) f.tabs.dispatchEvent(new f.w.CustomEvent(event, { detail: { selectedTabIndex: index } }));
                const panel = f.page.querySelector(`[data-index="${index}"]`);
                assert.equal(f.page.classList.contains('jellyspot-is-open'), false);
                assert.equal(panel.classList.contains('hide'), false);
                assert.equal(panel.classList.contains('is-active'), true);
                assert.notEqual(f.w.getComputedStyle(panel).display, 'none');
                assert.equal(f.page.querySelector('[data-jellyspot-tab]').classList.contains('is-active'), false);
            } finally { f.close(); }
        });
    }
}
test('entering Spotify leaves Spotify activation to its own handler', () => {
    const f = fixture();
    try {
        f.showSpotify();
        f.p.syncTabPanelsActiveState(6);
        assert.equal(f.page.classList.contains('jellyspot-is-open'), true);
        assert.equal(f.page.querySelector('[data-jellyspot-tab]').classList.contains('is-active'), true);
    } finally { f.close(); }
});
test('restores selected panel even when Spotify cleanup API is unavailable', () => {
    const f = fixture(false, false);
    try {
        f.showSpotify(); f.p.syncTabPanelsActiveState(4);
        assert.equal(f.page.classList.contains('jellyspot-is-open'), false);
        assert.equal(f.page.querySelector('[data-index="4"]').classList.contains('hide'), false);
    } finally { f.close(); }
});
