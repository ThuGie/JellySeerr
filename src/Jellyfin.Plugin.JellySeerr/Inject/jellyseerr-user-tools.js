(function () {
    'use strict';
    const events = [[2, 'Request pending'], [4, 'Approved'], [8, 'Available'], [16, 'Download failed'], [64, 'Declined'],
        [128, 'Auto approved'], [256, 'Issue reported'], [512, 'Issue comment'], [1024, 'Issue resolved'], [2048, 'Issue reopened'], [4096, 'Auto requested']];
    function api(path, body) {
        return ApiClient.ajax({ url: ApiClient.getUrl('JellySeerr/' + path), type: body ? 'POST' : 'GET', dataType: 'json',
            contentType: 'application/json', data: body ? JSON.stringify(body) : undefined, timeout: 240000 });
    }
    function errorText(e) {
        try { return (e.responseJSON || JSON.parse(e.responseText || '{}')).message || 'The service could not complete this operation.'; }
        catch (_) { return 'Could not reach the service. Try again shortly.'; }
    }
    function el(tag, text, parent) { const n = document.createElement(tag); if (text != null) n.textContent = text; if (parent) parent.appendChild(n); return n; }
    function button(text, parent, action) { const b = el('button', text, parent); b.type = 'button'; b.className = 'bst-btn-ghost'; b.addEventListener('click', action); return b; }
    function dialog(title) {
        const d = el('dialog'); d.className = 'bst-release-dialog bst-user-tools';
        const head = el('div', null, d); head.className = 'bst-release-header'; el('h2', title, head);
        let busy = false;
        const close = button('Close', head, () => { if (!busy) d.close(); });
        const body = el('div', null, d); const message = el('p', '', d); message.setAttribute('role', 'status'); message.setAttribute('aria-live', 'polite');
        d.addEventListener('keydown', e => e.stopPropagation());
        d.addEventListener('cancel', e => { if (busy) e.preventDefault(); });
        d.addEventListener('close', () => d.remove());
        document.body.appendChild(d); d.showModal();
        return { d, body, message, busy(value) { busy = value; close.disabled = value; }, fail(e) { if (d.isConnected) message.textContent = errorText(e); } };
    }
    async function notifications() {
        const view = dialog('Notification preferences'); view.message.textContent = 'Loading your Seerr preferences…';
        try {
            const settings = await api('notifications'); if (!view.d.isConnected) return;
            el('p', 'Choose which events Seerr sends through your configured channels. Link accounts, set your email address, and register browser push in Seerr first. Delivery still follows server settings.', view.body);
            const channels = settings.channels || [];
            channels.forEach(channel => {
                const box = el('fieldset', null, view.body); el('legend', channel.id, box);
                const enabled = channel.enabled && channel.linked;
                if (!enabled) el('p', 'Unavailable or not linked. Configure this channel in Seerr.', box);
                events.forEach(([bit, label]) => {
                    const row = el('label', null, box); row.className = 'bst-tools-check';
                    const input = el('input', null, row); input.type = 'checkbox'; input.checked = !!(channel.types & bit);
                    input.disabled = !enabled || !(channel.allowedTypes & bit); input.dataset.channel = channel.id; input.dataset.bit = bit;
                    row.appendChild(document.createTextNode(label));
                });
            });
            const save = button('Save preferences', view.body, async () => {
                view.busy(true); save.disabled = true;
                const notificationTypes = {};
                channels.filter(c => c.enabled && c.linked).forEach(c => {
                    notificationTypes[c.id] = 0;
                    view.body.querySelectorAll('input[data-channel]').forEach(input => {
                        if (input.dataset.channel === c.id && input.checked) notificationTypes[c.id] |= Number(input.dataset.bit);
                    });
                });
                try { const result = await api('notifications', { notificationTypes }); view.message.textContent = result.message; }
                catch (e) { view.fail(e); } finally { view.busy(false); save.disabled = false; }
            });
            view.message.textContent = channels.some(c => c.enabled && c.linked) ? '' : 'No notification channels are configured for your account.';
        } catch (e) { view.fail(e); }
    }
    function calendar() {
        const view = dialog('Release calendar'); let month = new Date(); month = new Date(month.getFullYear(), month.getMonth(), 1);
        const controls = el('div', null, view.body); controls.className = 'bst-release-controls';
        const heading = el('strong', '', controls);
        const prev = button('Previous month', controls, () => { month.setMonth(month.getMonth() - 1); load(); });
        const next = button('Next month', controls, () => { month.setMonth(month.getMonth() + 1); load(); });
        const type = el('select', null, controls); type.setAttribute('aria-label', 'Media type');
        [['all', 'All titles'], ['movie', 'Movies'], ['tv', 'Series']].forEach(([value, text]) => { const o = el('option', text, type); o.value = value; });
        const filter = el('input', null, controls); filter.type = 'search'; filter.placeholder = 'Filter titles'; filter.setAttribute('aria-label', 'Filter calendar titles');
        const list = el('div', null, view.body); let items = [], generation = 0;
        function render() {
            list.replaceChildren();
            const visible = items.filter(item => (type.value === 'all' || type.value === item.mediaType) && item.title.toLowerCase().includes(filter.value.toLowerCase()));
            let day = '';
            visible.forEach(item => {
                if (day !== item.date) { day = item.date; el('h3', new Date(day + 'T12:00:00').toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' }), list); }
                const row = el('article', null, list); row.className = 'bst-release-row';
                button(item.title, row, () => { view.d.close(); window.jellySeerrModal.open(item.tmdbId, item.mediaType); });
                el('p', item.label + ' · ' + (item.requestStatus === 1 ? 'Pending approval' : item.mediaStatus === 5 ? 'Available' : 'Requested'), row);
            });
            if (!visible.length) el('p', 'No dated releases for your requested titles in this month.', list);
        }
        function date(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
        async function load() {
            const version = ++generation; prev.disabled = next.disabled = true; view.message.textContent = 'Loading release dates…'; list.replaceChildren();
            heading.textContent = month.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
            const start = date(month), end = date(new Date(month.getFullYear(), month.getMonth() + 1, 1));
            try {
                const result = await api('calendar?start=' + start + '&end=' + end);
                if (!view.d.isConnected || version !== generation) return;
                items = result.events || []; render();
                view.message.textContent = result.message + (result.truncated ? ' Only the first 1000 requests were checked.' : '')
                    + (result.incompleteTitles ? ' Dates could not be loaded for ' + result.incompleteTitles + ' title(s).' : '');
            } catch (e) { view.fail(e); } finally { if (version === generation) prev.disabled = next.disabled = false; }
        }
        type.addEventListener('change', render); filter.addEventListener('input', render); load();
    }
    function requestTool(records, mode) {
        const view = dialog(mode === 'activity' ? 'Download & replacement history' : 'Recover stalled downloads');
        const select = el('select', null, view.body); select.setAttribute('aria-label', 'Request');
        records.forEach(r => { const o = el('option', 'Request #' + r.id + (r.is4k ? ' · 4K' : '') + (r.type === 'tv' ? ' · Series' : ''), select); o.value = r.id; });
        const refresh = button('Refresh', view.body, load); const list = el('div', null, view.body); let generation = 0, busy = false;
        async function load() {
            if (busy) return;
            const version = ++generation; list.replaceChildren(); view.message.textContent = 'Loading…';
            if (!select.value) { view.message.textContent = 'Choose an approved request first.'; return; }
            try {
                const result = await api('request/' + select.value + '/' + mode); if (!view.d.isConnected || version !== generation) return;
                view.message.textContent = result.message;
                const rows = mode === 'activity' ? result.events : result.downloads;
                (rows || []).forEach(item => {
                    const row = el('article', null, list); row.className = 'bst-release-row';
                    if (mode === 'activity') {
                        el('strong', item.action, row); el('p', [item.date ? new Date(item.date).toLocaleString() : '', item.actor, item.release, item.outcome,
                            item.seasonNumber != null ? 'Season ' + item.seasonNumber : '', item.episodeNumber != null ? 'Episode ' + item.episodeNumber : ''].filter(Boolean).join(' · '), row);
                    } else {
                        el('strong', item.title, row); el('p', item.scope, row);
                        if (item.episodes && item.episodes.length) el('p', item.episodes.map(e => 'S' + e.seasonNumber + ' E' + e.episodeNumber + (e.title ? ' � ' + e.title : '')).join('; '), row);
                        const recover = button('Remove, blocklist & search again', row, async () => {
                            if (!window.confirm('Recover ' + item.scope + '? This removes the failed download and partial data, blocklists only its specific release, and starts a fresh search. This may affect other users. Existing library files are retained.')) return;
                            busy = true; view.busy(true); select.disabled = refresh.disabled = true;
                            list.querySelectorAll('button').forEach(b => { b.disabled = true; });
                            try {
                                const result = await api('request/' + select.value + '/recovery', { token: item.token });
                                list.replaceChildren(); view.message.textContent = result.message;
                            } catch (e) { list.replaceChildren(); view.fail(e); }
                            finally { busy = false; view.busy(false); select.disabled = refresh.disabled = false; }
                        });
                        recover.disabled = busy;
                    }
                });
                if (!rows || !rows.length) el('p', mode === 'activity' ? 'No recent history for this request.' : 'No recoverable downloads. Healthy downloads and downloads with an uncertain episode scope are excluded.', list);
            } catch (e) { view.fail(e); }
        }
        select.addEventListener('change', load); load();
    }
    function issues(mediaId, mediaType) {
        const view = dialog('Report & track a problem');
        const category = el('select', null, view.body); category.setAttribute('aria-label', 'Problem category');
        [[1, 'Video / playback'], [2, 'Audio'], [3, 'Subtitles'], [4, 'Wrong content / other']].forEach(([id, name]) => { const o = el('option', name, category); o.value = id; });
        const season = el('input', null, view.body); season.type = 'number'; season.min = '0'; season.placeholder = 'Season (optional)'; season.setAttribute('aria-label', 'Problem season');
        const episode = el('input', null, view.body); episode.type = 'number'; episode.min = '1'; episode.placeholder = 'Episode (optional)'; episode.setAttribute('aria-label', 'Problem episode');
        season.hidden = episode.hidden = mediaType !== 'tv';
        const message = el('textarea', null, view.body); message.maxLength = 4000; message.placeholder = 'Describe what happens, the device/player, and when it occurs.'; message.setAttribute('aria-label', 'Problem description');
        let pending = false;
        const submit = button('Report problem', view.body, async () => {
            if (pending) return;
            if (!message.value.trim()) { view.message.textContent = 'Describe the problem first.'; return; }
            if ((season.value && (!Number.isInteger(Number(season.value)) || Number(season.value) < 0)) ||
                (episode.value && (!season.value || !Number.isInteger(Number(episode.value)) || Number(episode.value) < 1))) {
                view.message.textContent = 'Enter a valid season and episode, or leave both blank.'; return;
            }
            pending = true; view.busy(true); submit.disabled = true;
            try {
                await api('issue', { mediaId, issueType: Number(category.value), message: message.value.trim(),
                    problemSeason: mediaType === 'tv' && season.value !== '' ? Number(season.value) : null,
                    problemEpisode: mediaType === 'tv' && episode.value !== '' ? Number(episode.value) : null });
                message.value = ''; await load(); view.message.textContent = 'Problem reported. You can follow its status below.';
            } catch (e) { view.fail(e); } finally { pending = false; view.busy(false); submit.disabled = false; }
        });
        el('h3', 'Existing reports', view.body); el('p', 'Check for an existing report before submitting another. Seerr controls which reports your account can view.', view.body);
        const list = el('div', null, view.body);
        async function load() {
            try {
                const result = await api('issues/' + mediaId); if (!view.d.isConnected) return; list.replaceChildren();
                (result.issues || []).forEach(issue => {
                    const row = el('article', null, list); row.className = 'bst-release-row';
                    el('strong', '#' + issue.id + ' · ' + (issue.status === 2 ? 'Resolved' : 'Open') + ' · ' + (['', 'Video', 'Audio', 'Subtitles', 'Other'][issue.issueType] || 'Other')
                        + (issue.problemSeason != null ? ' · S' + issue.problemSeason : '') + (issue.problemEpisode != null ? ' E' + issue.problemEpisode : ''), row);
                    (issue.comments || []).forEach(c => el('p', (c.author ? c.author + ': ' : '') + c.message, row));
                    const comment = el('textarea', null, row); comment.maxLength = 4000; comment.setAttribute('aria-label', 'Comment on issue ' + issue.id);
                    const add = button('Add comment', row, async () => {
                        if (!comment.value.trim()) return;
                        add.disabled = true; view.busy(true);
                        try { await api('issues/' + issue.id + '/comment', { message: comment.value.trim() }); await load(); view.message.textContent = 'Comment added.'; }
                        catch (e) { view.fail(e); } finally { add.disabled = false; view.busy(false); }
                    });
                });
                if (!(result.issues || []).length) el('p', 'No visible reports for this title.', list);
                if (result.truncated) el('p', 'Only the latest 1000 reports were checked.', list);
            } catch (e) { el('p', errorText(e), list); }
        }
        load();
    }
    function inferred(title) {
        const t = title || '';
        return { videoCodec: /\b(x265|h[ .]?265|hevc)\b/i.test(t) ? 'HEVC' : /\b(x264|h[ .]?264|avc)\b/i.test(t) ? 'H.264' : /\bAV1\b/i.test(t) ? 'AV1' : '',
            audioCodec: /\b(TrueHD|DTS[ .-]?HD|DTS|DDP|EAC3|AC3|AAC|FLAC)\b/i.exec(t)?.[0] || '',
            videoDynamicRangeType: /\b(DV|DoVi|Dolby[ .]?Vision)\b/i.test(t) ? 'Dolby Vision' : /\bHDR10\+?\b/i.exec(t)?.[0] || (/\bHDR\b/i.test(t) ? 'HDR' : '') };
    }
    function compare(current, release) {
        const view = dialog('Compare release'); el('p', release.title || 'Selected release', view.body);
        el('p', 'Current file values come from Sonarr/Radarr inspection. Candidate codecs and HDR are inferred from the release name and may be inaccurate. Unknown values do not prove compatibility.', view.body);
        const sources = current && current.files ? current.files : [current || {}];
        const candidate = Object.assign(inferred(release.title), { quality: release.quality, size: release.size });
        const table = el('table', null, view.body); table.className = 'bst-tools-table';
        const head = el('tr', null, table); ['Property', 'Current file(s)', 'Selected release'].forEach(t => el('th', t, head));
        [['quality', 'Quality'], ['size', 'Size'], ['videoCodec', 'Video codec'], ['videoDynamicRangeType', 'HDR'], ['videoBitDepth', 'Bit depth'],
            ['audioCodec', 'Audio codec'], ['audioChannels', 'Audio channels'], ['audioLanguages', 'Audio languages'], ['subtitles', 'Subtitles']].forEach(([key, label]) => {
            const row = el('tr', null, table); el('th', label, row);
            const values = [...new Set(sources.map(s => s[key]).filter(v => v != null && v !== '').map(v => key === 'size' ? (Number(v) / 1073741824).toFixed(2) + ' GB' : String(v)))];
            el('td', values.join(' / ') || 'Unknown', row);
            const value = candidate[key]; el('td', value ? key === 'size' ? (Number(value) / 1073741824).toFixed(2) + ' GB' : String(value) : 'Unknown', row);
        });
    }
    window.jellySeerrUserTools = { calendar, notifications, issues, requestTool, compare };
})();
