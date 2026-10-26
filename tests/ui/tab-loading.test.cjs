const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');
const script = fs.readFileSync(path.join(__dirname, '../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-tabs.js'), 'utf8');
const tick = () => new Promise(r => setImmediate(r));
function fixture() {
    const dom = new JSDOM('<div class="page"><div class="tabContent pageTabContent is-active"><div class="jellySeerr-movies-sections"></div></div></div>', { runScripts: 'outside-only', pretendToBeVisual: true });
    const w = dom.window;
    w.eval(script.replace('window.jellySeerrPlugin.init();', '/* no automatic boot */'));
    const p = w.jellySeerrPlugin, container = w.document.querySelector('.jellySeerr-movies-sections');
    p.isContainerVisible = el => el.isConnected && el.closest('.tabContent').classList.contains('is-active');
    p.loadDisplaySettings = async () => {};
    p.getDisplaySettingsKey = () => 'test';
    container.dataset.jellySeerrDisplaySettings = 'test';
    p.movieRows = [{ title: 'Popular', path: 'popular' }];
    const node = cls => { const el = w.document.createElement('div'); el.className = cls; return el; };
    p.buildRowSkeleton = () => node('jellySeerr-poster-section jellySeerr-skeleton-section');
    p.buildPosterRow = () => node('jellySeerr-poster-section');
    p.buildCarouselSection = () => node('jellySeerr-carousel-section');
    p.refreshScrollers = () => {};
    p.shouldUseNativeCarousels = () => true;
    const pending = [];
    p.fetchDiscover = () => new Promise(resolve => pending.push(() => resolve({ items: [{}], total: 1 })));
    p.fetchJson = () => new Promise(resolve => pending.push(() => resolve([{}])));
    return { w, p, container, pending, panel: container.parentElement, close: () => w.close() };
}
test('results arriving while a discovery tab is hidden populate it for the next visit', async () => {
    const f = fixture();
    try {
        f.p.loadTab('movies', f.container); await tick();
        assert.equal(f.pending.length, 4);
        f.panel.classList.remove('is-active');
        f.pending.forEach(resolve => resolve()); await tick();
        f.panel.classList.add('is-active');
        assert.equal(f.container.querySelectorAll('.jellySeerr-skeleton-section').length, 0);
        assert.equal(f.container.querySelectorAll('.jellySeerr-poster-section').length, 1);
        assert.equal(f.container.querySelectorAll('.jellySeerr-carousel-section').length, 3);
        assert.equal(f.container.dataset.jellySeerrLoading, 'false');
    } finally { f.close(); }
});
test('native loading placeholders are not treated as populated content', () => {
    const f = fixture();
    try {
        f.container.appendChild(f.p.buildRowSkeleton());
        assert.equal(f.p.isContainerPopulated(f.container), false);
    } finally { f.close(); }
});
test('a superseded lookup cannot overwrite a newer discovery load', async () => {
    const f = fixture();
    try {
        f.p.loadTab('movies', f.container); await tick();
        f.p.cancelContainerLoad(f.container);
        f.container.innerHTML = '<div data-jellySeerr-grid-view="true">Selected genre</div>';
        f.pending.forEach(resolve => resolve()); await tick();
        assert.equal(f.container.textContent, 'Selected genre');
        assert.equal(f.container.children.length, 1);
    } finally { f.close(); }
});
