(function () {
    'use strict';
    let routeRestored = false;
    function node(tag, text, parent) { const n = document.createElement(tag); if (text != null) n.textContent = text; if (parent) parent.appendChild(n); return n; }
    function button(text, parent, action) { const b = node('button', text, parent); b.type = 'button'; b.className = 'jellySeerr-calendar-button'; b.addEventListener('click', action); return b; }
    function dateKey(date) { return date.getFullYear() + '-' + String(date.getMonth() + 1).padStart(2, '0') + '-' + String(date.getDate()).padStart(2, '0'); }
    function route(active) {
        const parts = location.hash.split('?'); const params = new URLSearchParams(parts[1] || '');
        if (active) params.set('jellySeerrTab', 'calendar'); else params.delete('jellySeerrTab');
        const query = params.toString(); history.replaceState(history.state, '', (parts[0] || '#/home') + (query ? '?' + query : ''));
    }
    async function open() {
        const plugin = window.jellySeerrPlugin; if (!plugin) return;
        for (let attempt = 0; attempt < 8; attempt++) {
            await plugin.selectjellySeerrTab('calendar');
            const container = document.querySelector('.jellySeerr-calendar-sections');
            if (container && container.isConnected) { routeRestored = true; route(true); ensureMounted(); return; }
            await new Promise(resolve => setTimeout(resolve, 400));
        }
        if (window.Dashboard && Dashboard.alert) Dashboard.alert('The calendar page could not open. Reload Jellyfin and check that the Release calendar tab is enabled in JellySeerr settings.');
    }
    function ensureMounted() {
        const plugin = window.jellySeerrPlugin;
        const container = document.querySelector('.jellySeerr-calendar-sections');
        if (!plugin || !container || !container.isConnected) return;
        if (!routeRestored && new URLSearchParams(location.hash.split('?')[1] || '').get('jellySeerrTab') === 'calendar') {
            routeRestored = true; open(); return;
        }
        if (!plugin.isContainerVisible(container) || container.dataset.calendarMounted === 'true') return;
        container.dataset.calendarMounted = 'true'; mount(container);
    }
    function mount(container) {
        const page = node('section', null, container); page.className = 'jellySeerr-calendar-page';
        node('h1', 'Release calendar', page);
        node('p', 'Upcoming episodes for all monitored series in Sonarr, regardless of who requested them. Times are shown in your local time zone.', page);
        const toolbar = node('div', null, page); toolbar.className = 'jellySeerr-calendar-toolbar';
        let month = new Date(); month = new Date(month.getFullYear(), month.getMonth(), 1);
        const previous = button('Previous', toolbar, () => changeMonth(-1));
        const today = button('Today', toolbar, () => { month = new Date(); month = new Date(month.getFullYear(), month.getMonth(), 1); load(); });
        const next = button('Next', toolbar, () => changeMonth(1));
        const heading = node('h2', '', toolbar);
        const refresh = button('Refresh', toolbar, load);
        const filters = node('div', null, page); filters.className = 'jellySeerr-calendar-toolbar';
        const search = node('input', null, filters); search.type = 'search'; search.placeholder = 'Find a series or episode'; search.setAttribute('aria-label', 'Filter series or episodes');
        const server = node('select', null, filters); server.setAttribute('aria-label', 'Sonarr instance');
        const view = node('select', null, filters); view.setAttribute('aria-label', 'Calendar view');
        [['month', 'Month'], ['agenda', 'Agenda']].forEach(([value, label]) => { const o = node('option', label, view); o.value = value; });
        if (window.matchMedia && window.matchMedia('(max-width: 760px)').matches) view.value = 'agenda';
        const hideLabel = node('label', null, filters); const hide = node('input', null, hideLabel); hide.type = 'checkbox'; hideLabel.appendChild(document.createTextNode(' Hide available episodes'));
        const message = node('p', '', page); message.setAttribute('role', 'status'); message.setAttribute('aria-live', 'polite');
        const schedule = node('div', null, page); schedule.className = 'jellySeerr-calendar-schedule';
        const waiting = node('section', null, page); waiting.className = 'jellySeerr-calendar-waiting';
        let result = { events: [], awaitingDates: [] }, generation = 0;
        function changeMonth(offset) { month = new Date(month.getFullYear(), month.getMonth() + offset, 1); load(); }
        function bounds() {
            const start = new Date(month); const mondayOffset = (start.getDay() + 6) % 7; start.setDate(start.getDate() - mondayOffset);
            const end = new Date(start); end.setDate(end.getDate() + 42); return { start, end };
        }
        function matches(item) { return (!server.value || item.server === server.value) && (item.title + ' ' + (item.episodeTitle || '')).toLowerCase().includes(search.value.trim().toLowerCase()); }
        function titleLink(item, parent) {
            if (Number(item.tmdbId) > 0) {
                const b = button(item.title, parent, () => window.jellySeerrModal && window.jellySeerrModal.open(item.tmdbId, 'tv')); b.classList.add('jellySeerr-calendar-title');
            } else node('strong', item.title, parent);
        }
        function episode(item, parent, compact) {
            const card = node('article', null, parent); card.className = 'jellySeerr-calendar-event' + (item.hasFile ? ' is-available' : '');
            titleLink(item, card);
            node('div', 'S' + item.seasonNumber + ' E' + item.episodeNumber + (item.episodeTitle ? ' · ' + item.episodeTitle : ''), card);
            const air = new Date(item.airDateUtc);
            const time = node('time', air.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }), card); time.dateTime = air.toISOString();
            node('span', item.hasFile ? ' · Available' : air > new Date() ? ' · Upcoming' : ' · Aired', card);
            if (!compact) { node('p', [item.network, item.server].filter(Boolean).join(' · '), card); if (item.overview) node('p', item.overview, card); }
        }
        function render() {
            schedule.replaceChildren(); waiting.replaceChildren();
            const items = (result.events || []).filter(matches).filter(e => !hide.checked || !e.hasFile);
            const days = new Map(); items.forEach(item => { const key = dateKey(new Date(item.airDateUtc)); if (!days.has(key)) days.set(key, []); days.get(key).push(item); });
            if (view.value === 'month') {
                const labels = node('div', null, schedule); labels.className = 'jellySeerr-calendar-weekdays';
                const monday = new Date(2026, 9, 19); for (let i = 0; i < 7; i++) { const d = new Date(monday); d.setDate(d.getDate() + i); node('span', d.toLocaleDateString([], { weekday: 'short' }), labels); }
                const grid = node('div', null, schedule); grid.className = 'jellySeerr-calendar-grid';
                const { start } = bounds();
                for (let i = 0; i < 42; i++) {
                    const day = new Date(start); day.setDate(day.getDate() + i); const key = dateKey(day);
                    const cell = node('section', null, grid); cell.className = 'jellySeerr-calendar-day'; cell.dataset.date = key;
                    if (day.getMonth() !== month.getMonth()) cell.classList.add('is-adjacent');
                    if (key === dateKey(new Date())) cell.classList.add('is-today');
                    const label = node('h3', String(day.getDate()), cell); label.setAttribute('aria-label', day.toLocaleDateString([], { dateStyle: 'full' }));
                    (days.get(key) || []).forEach(item => episode(item, cell, true));
                }
            } else {
                [...days.keys()].sort().forEach(key => { node('h3', new Date(key + 'T12:00:00').toLocaleDateString([], { dateStyle: 'full' }), schedule); days.get(key).forEach(item => episode(item, schedule, false)); });
            }
            if (!items.length) node('p', 'No monitored episodes with known air dates in this range match your filters.', schedule);
            const unknown = (result.awaitingDates || []).filter(matches);
            node('h2', 'Awaiting an air date (' + unknown.length + ')', waiting);
            node('p', 'Continuing or upcoming monitored series without a known next air date. Sonarr may not have received the schedule yet.', waiting);
            const list = node('div', null, waiting); list.className = 'jellySeerr-calendar-unknown-list';
            unknown.forEach(show => { const card = node('article', null, list); titleLink(show, card); node('p', [show.network, show.server].filter(Boolean).join(' · '), card); });
        }
        async function load() {
            const version = ++generation; previous.disabled = today.disabled = next.disabled = refresh.disabled = true;
            heading.textContent = month.toLocaleDateString([], { month: 'long', year: 'numeric' }); message.textContent = 'Loading monitored episodes from Sonarr…';
            schedule.replaceChildren(); waiting.replaceChildren(); const { start, end } = bounds();
            try {
                const data = await ApiClient.ajax({ url: ApiClient.getUrl('JellySeerr/calendar/monitored') + '?start=' + encodeURIComponent(start.toISOString()) + '&end=' + encodeURIComponent(end.toISOString()), type: 'GET', dataType: 'json', timeout: 60000 });
                if (!page.isConnected || version !== generation) return;
                result = data; const selected = server.value; server.replaceChildren(); node('option', 'All Sonarr instances', server).value = '';
                [...new Set([...(data.events || []), ...(data.awaitingDates || [])].map(i => i.server))].sort().forEach(name => { node('option', name, server).value = name; });
                if ([...server.options].some(o => o.value === selected)) server.value = selected;
                message.textContent = data.message + ((data.failures || []).length ? ' Some instances could not load: ' + data.failures.map(f => f.server + ': ' + f.message).join('; ') : ''); render();
            } catch (error) {
                let text = 'Could not load the Sonarr calendar' + (error && error.status ? ' (HTTP ' + error.status + ')' : '') + '. Check the service connections.';
                try { const body = error.responseJSON || await error.clone().json(); text = body.message || text; } catch (_) { /* Keep safe fallback for proxy/network errors. */ }
                if (page.isConnected && version === generation) message.textContent = text;
            } finally { if (version === generation) previous.disabled = today.disabled = next.disabled = refresh.disabled = false; }
        }
        [search, server, view, hide].forEach(input => input.addEventListener(input === search ? 'input' : 'change', render)); load();
    }
    window.addEventListener('hashchange', () => { routeRestored = false; });
    window.jellySeerrCalendar = { open, ensureMounted, mount, route };
})();
