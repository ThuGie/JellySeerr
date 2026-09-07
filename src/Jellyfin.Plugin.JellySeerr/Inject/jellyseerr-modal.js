'use strict';

window.jellySeerrLog = window.jellySeerrLog || {
    info: function (msg) {
        console.log('JS • ' + msg);
    },
    warn: function (msg, detail) {
        if (detail !== undefined) {
            console.warn('JS • ' + msg, detail);
        } else {
            console.warn('JS • ' + msg);
        }
    },
    error: function (msg, detail) {
        if (detail !== undefined) {
            console.error('JS • ' + msg, detail);
        } else {
            console.error('JS • ' + msg);
        }
    }
};

(function () {
    const log = window.jellySeerrLog;
    const TMDB_LOGO_SVG = '<svg width="2em" height="2em" fill="currentColor" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 190.24 81.52"><defs><linearGradient id="bst-tmdb-grad" y1="40.76" x2="190.24" y2="40.76" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#90cea1"/><stop offset="0.56" stop-color="#3cbec9"/><stop offset="1" stop-color="#00b3e5"/></linearGradient></defs><path fill="url(#bst-tmdb-grad)" d="M105.67,36.06h66.9A17.67,17.67,0,0,0,190.24,18.4h0A17.67,17.67,0,0,0,172.57.73h-66.9A17.67,17.67,0,0,0,88,18.4h0A17.67,17.67,0,0,0,105.67,36.06Zm-88,45h76.9A17.67,17.67,0,0,0,112.24,63.4h0A17.67,17.67,0,0,0,94.57,45.73H17.67A17.67,17.67,0,0,0,0,63.4H0A17.67,17.67,0,0,0,17.67,81.06ZM10.41,35.42h7.8V6.92h10.1V0H.31v6.9h10.1Zm28.1,0h7.8V8.25h.1l9,27.15h6l9.3-27.15h.1V35.4h7.8V0H66.76l-8.2,23.1h-.1L50.31,0H38.51ZM152.43,55.67a15.07,15.07,0,0,0-4.52-5.52,18.57,18.57,0,0,0-6.68-3.08,33.54,33.54,0,0,0-8.07-1h-11.7v35.4h12.75a24.58,24.58,0,0,0,7.55-1.15A19.34,19.34,0,0,0,148.11,77a16.27,16.27,0,0,0,4.37-5.5,16.91,16.91,0,0,0,1.63-7.58A18.5,18.5,0,0,0,152.43,55.67ZM145,68.6A8.8,8.8,0,0,1,142.36,72a10.7,10.7,0,0,1-4,1.82,21.57,21.57,0,0,1-5,.55h-4.05v-21h4.6a17,17,0,0,1,4.67.63,11.66,11.66,0,0,1,3.88,1.87A9.14,9.14,0,0,1,145,59a9.87,9.87,0,0,1,1,4.52A11.89,11.89,0,0,1,145,68.6Zm44.63-.13a8,8,0,0,0-1.58-2.62A8.38,8.38,0,0,0,185.63,64a10.31,10.31,0,0,0-3.17-1v-.1a9.22,9.22,0,0,0,4.42-2.82,7.43,7.43,0,0,0,1.68-5,8.42,8.42,0,0,0-1.15-4.65,8.09,8.09,0,0,0-3-2.72,12.56,12.56,0,0,0-4.18-1.3,32.84,32.84,0,0,0-4.62-.33h-13.2v35.4h14.5a22.41,22.41,0,0,0,4.72-.5,13.53,13.53,0,0,0,4.28-1.65,9.42,9.42,0,0,0,3.1-3,8.52,8.52,0,0,0,1.2-4.68A9.39,9.39,0,0,0,189.66,68.47ZM170.21,52.72h5.3a10,10,0,0,1,1.85.18,6.18,6.18,0,0,1,1.7.57,3.39,3.39,0,0,1,1.22,1.13,3.22,3.22,0,0,1,.48,1.82,3.63,3.63,0,0,1-.43,1.8,3.4,3.4,0,0,1-1.12,1.2,4.92,4.92,0,0,1-1.58.65,7.51,7.51,0,0,1-1.77.2h-5.65Zm11.72,20a3.9,3.9,0,0,1-1.22,1.3,4.64,4.64,0,0,1-1.68.7,8.18,8.18,0,0,1-1.82.2h-7v-8h5.9a15.35,15.35,0,0,1,2,.15,8.47,8.47,0,0,1,2.05.55,4,4,0,0,1,1.57,1.18,3.11,3.11,0,0,1,.63,2A3.71,3.71,0,0,1,181.93,72.72Z"/></svg>';
    const CLOSE_ICON = '<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 320 512"><path fill="currentColor" d="M310.6 150.6c12.5-12.5 12.5-32.8 0-45.3s-32.8-12.5-45.3 0L160 210.7 54.6 105.4c-12.5-12.5-32.8-12.5-45.3 0s-12.5 32.8 0 45.3L114.7 256 9.4 361.4c-12.5 12.5-12.5 32.8 0 45.3s32.8 12.5 45.3 0L160 301.3 265.4 406.6c12.5 12.5 32.8 12.5 45.3 0s12.5-32.8 0-45.3L205.3 256 310.6 150.6z"/></svg>';
    const IMDB_ICON = '<svg width="2em" height="2em" fill="currentColor" viewBox="0 0 32 32"><path d="M8.4,21.1H5.9V9.9h3.8l0.7,4.7h0.1L11,9.9h3.8v11.2h-2.5v-6.7h-0.1l-0.9,6.7H9.4l-1-6.7h0L8.4,21.1z"/><path d="M15.8,9.8c0.4,0,3.2-0.1,4.7,0.1c1.2,0.1,1.8,1.1,1.9,2.3c0.1,2.2,0.1,4.4,0.1,6.6c0,0.2,0,0.5-0.1,0.8c-0.2,0.9-0.7,1.4-1.9,1.5c-1.5,0.1-3,0.1-4.4,0.1c0,0-0.1,0-0.2,0V9.8z M18.8,11.9v7.2c0.5,0,0.8-0.2,0.8-0.7c0-1.9,0-3.9,0-5.9C19.6,12,19.4,11.8,18.8,11.9z"/><path d="M2,21.1V9.9h2.9v11.2H2z"/><path d="M29.9,14.1c-0.1-0.8-0.6-1.2-1.4-1.4c-0.8-0.1-1.6,0-2.3,0.7V9.9h-2.8v11.2H26c0.1-0.2,0.1-0.4,0.2-0.5c0.1,0.1,0.2,0.2,0.3,0.3c0.7,0.5,1.5,0.6,2.3,0.3c0.7-0.3,1-0.9,1-1.6c0-0.8,0.1-1.7,0.1-2.6C30,16,30,15,29.9,14.1z M27.1,19.1c0,0.2-0.2,0.4-0.4,0.4s-0.4-0.2-0.4-0.4v-4.3c0-0.2,0.2-0.4,0.4-0.4s0.4,0.2,0.4,0.4V19.1z"/></svg>';

    let activeDetailsRoot = null;
    let activeSeasonRoot = null;
    let activeQualityRoot = null;
    let escapeHandler = null;
    let pendingRequestContext = null;
    let clientSettingsCache = {};
    let quotaCache = { at: 0, data: null };

    function formatQuotaPart(entry, label) {
        if (!entry) {
            return '';
        }
        const limit = Number(entry.limit ?? entry.Limit ?? entry.quotaLimit ?? entry.QuotaLimit ?? entry.quota ?? entry.Quota);
        if (!Number.isFinite(limit) || limit <= 0) {
            return '';
        }
        const used = Number(entry.used ?? entry.Used ?? entry.quotaUsed ?? entry.QuotaUsed ?? 0);
        const remainingRaw = entry.remaining ?? entry.Remaining;
        const left = Number.isFinite(Number(remainingRaw)) ? Number(remainingRaw) : Math.max(0, limit - (Number.isFinite(used) ? used : 0));
        return left + ' of ' + limit + ' ' + label + ' left';
    }

    function formatQuotaSummary(data, mediaType) {
        if (!data) {
            return '';
        }
        if (mediaType === 'movie') {
            return formatQuotaPart(data.movie || data.Movie, 'movies');
        }
        if (mediaType === 'tv') {
            return formatQuotaPart(data.tv || data.Tv || data.TV, 'TV');
        }
        return [formatQuotaPart(data.movie || data.Movie, 'movies'), formatQuotaPart(data.tv || data.Tv || data.TV, 'TV')]
            .filter(Boolean)
            .join(' · ');
    }

    window.jellySeerrQuota = {
        fetch: function () {
            if (quotaCache.data && Date.now() - quotaCache.at < 30000) {
                return Promise.resolve(quotaCache.data);
            }
            return ApiClient.ajax({
                url: ApiClient.getUrl('JellySeerr/quota'),
                type: 'GET',
                dataType: 'json'
            }).then(function (data) {
                quotaCache = { at: Date.now(), data: data };
                return data;
            }).catch(function () {
                return null;
            });
        },
        formatSummary: formatQuotaSummary
    };

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }

    function mountFromHtml(html) {
        const mount = document.createElement('div');
        mount.innerHTML = html.trim();
        return mount.firstElementChild;
    }

    function readAdvancedBool(value, fallback) {
        if (value === true || value === false) {
            return value;
        }
        return fallback;
    }

    function getRequestModalAdvanced() {
        const plugin = window.jellySeerrPlugin;
        const modal = plugin && plugin._displaySettings && plugin._displaySettings.Advanced
            ? plugin._displaySettings.Advanced.requestModal
            : null;
        return {
            tvSeasonPickerEnabled: readAdvancedBool(modal && modal.tvSeasonPickerEnabled, true),
            includeSpecialsSeason: readAdvancedBool(modal && modal.includeSpecialsSeason, false),
            requireExplicitSeasonSelection: readAdvancedBool(modal && modal.requireExplicitSeasonSelection, false),
            showRequest4kButton: readAdvancedBool(modal && modal.showRequest4kButton, true),
            backdropLanguageFilter: (modal && modal.backdropLanguageFilter) || 'en,null,en-US'
        };
    }

    function tmdbImage(path, size) {
        if (!path) {
            return '';
        }
        return 'https://image.tmdb.org/t/p/' + (size || 'original') + path;
    }

    function resolveImageUrl(url) {
        if (!url) {
            return '';
        }
        if (url.startsWith('http') || url.startsWith('data:')) {
            return url;
        }
        return ApiClient.getUrl(url);
    }

    const PLUGIN_ID = '8f3a2c91-4e7b-4d6a-9c18-2b5e0f7a6d44';

    function getLogoImageUrl(data) {
        const rawPath = data && (data.logoPath || data.logo_path);
        if (!rawPath) {
            return '';
        }
        return rawPath.startsWith('http') ? rawPath : tmdbImage(rawPath, 'original');
    }

    function getTmdbApiKey(config) {
        const key = config && (config.tmdbApiKey || config.TmdbApiKey);
        return key && String(key).trim() ? String(key).trim() : '';
    }

    function isEnglishLogo(logo) {
        return logo && String(logo.iso_639_1 || '').toLowerCase() === 'en';
    }

    function isEnUsLogo(logo) {
        return isEnglishLogo(logo) && String(logo.iso_3166_1 || '').toUpperCase() === 'US';
    }

    function pickLogoFilePath(logos) {
        if (!logos || !logos.length) {
            return null;
        }

        const valid = logos.filter(function (logo) {
            return logo && logo.file_path;
        });

        if (!valid.length) {
            return null;
        }

        function byVote(a, b) {
            return (b.vote_average || 0) - (a.vote_average || 0) ||
                (b.width || 0) - (a.width || 0);
        }

        // Prioritize getting English logos over en-US. Fallbacks to highest rated logo (not locale specific)
        const english = valid
            .filter(function (logo) { return isEnglishLogo(logo) && !isEnUsLogo(logo); })
            .sort(byVote)[0];
        if (english) {
            return english.file_path;
        }

        const enUs = valid
            .filter(isEnUsLogo)
            .sort(byVote)[0];
        if (enUs) {
            return enUs.file_path;
        }

        return valid.sort(byVote)[0].file_path;
    }

    function isTmdbBearerToken(apiKey) {
        // v4 read tokens are JWTs, while v3 keys are plain strings.
        const parts = String(apiKey).split('.');
        return parts.length === 3;
    }

    function appendTmdbQuery(url, name, value) {
        return url + (url.indexOf('?') >= 0 ? '&' : '?') +
            encodeURIComponent(name) + '=' + encodeURIComponent(value);
    }

    function fetchTmdbJson(url, apiKey) {
        const headers = { accept: 'application/json' };
        let requestUrl = url;

        if (isTmdbBearerToken(apiKey)) {
            headers.Authorization = 'Bearer ' + apiKey;
        } else {
            requestUrl = appendTmdbQuery(url, 'api_key', apiKey);
        }

        return fetch(requestUrl, { headers: headers }).then(function (response) {
            if (!response.ok) {
                throw new Error('TMDB request failed: ' + response.status);
            }
            return response.json();
        });
    }

    function mapRelatedList(raw) {
        const results = raw && (raw.results || raw.Results || raw);
        if (!Array.isArray(results)) {
            return [];
        }
        return results.map(function (item) {
            if (!item) {
                return null;
            }
            const id = item.id || item.Id;
            if (!id) {
                return null;
            }
            return {
                id: id,
                title: item.title || item.name || item.Title || item.Name || 'Title',
                mediaType: item.media_type || item.mediaType || item.MediaType || (item.first_air_date || item.firstAirDate ? 'tv' : 'movie'),
                posterPath: item.poster_path || item.posterPath || item.PosterPath || ''
            };
        }).filter(Boolean);
    }

    function mapTmdbSeasons(rawSeasons) {
        if (!Array.isArray(rawSeasons)) {
            return [];
        }
        return rawSeasons.map(function (season) {
            if (!season) {
                return null;
            }
            const seasonNumber = season.seasonNumber != null ? season.seasonNumber : (season.season_number != null ? season.season_number : season.SeasonNumber);
            if (!Number.isFinite(Number(seasonNumber))) {
                return null;
            }
            return {
                seasonNumber: Number(seasonNumber),
                name: season.name || season.Name || '',
                episodeCount: season.episodeCount != null ? season.episodeCount : (season.episode_count != null ? season.episode_count : season.EpisodeCount),
                posterPath: season.posterPath || season.poster_path || season.PosterPath || ''
            };
        }).filter(Boolean);
    }

    function mapMovieDetails(raw) {
        const details = {
            id: raw.id,
            mediaType: 'movie',
            title: raw.title,
            overview: raw.overview,
            backdropPath: raw.backdrop_path,
            posterPath: raw.poster_path,
            voteAverage: raw.vote_average,
            voteCount: raw.vote_count,
            releaseDate: raw.release_date,
            runtime: raw.runtime,
            originalLanguage: raw.original_language,
            adult: raw.adult,
            genres: raw.genres || [],
            credits: raw.credits || {},
            releaseDates: raw.release_dates || {},
            similar: mapRelatedList(raw.similar),
            recommendations: mapRelatedList(raw.recommendations)
        };

        if (raw.videos && raw.videos.results) {
            details.relatedVideos = raw.videos.results;
        }

        if (raw.external_ids) {
            details.externalIds = { imdbId: raw.external_ids.imdb_id };
        }

        return details;
    }

    function mapTvDetails(raw) {
        const details = {
            id: raw.id,
            mediaType: 'tv',
            name: raw.name,
            overview: raw.overview,
            backdropPath: raw.backdrop_path,
            posterPath: raw.poster_path,
            voteAverage: raw.vote_average,
            voteCount: raw.vote_count,
            firstAirDate: raw.first_air_date,
            episodeRunTime: raw.episode_run_time || [],
            originalLanguage: raw.original_language,
            genres: raw.genres || [],
            credits: raw.credits || {},
            contentRatings: raw.content_ratings || {},
            seasons: mapTmdbSeasons(raw.seasons),
            similar: mapRelatedList(raw.similar),
            recommendations: mapRelatedList(raw.recommendations)
        };

        if (raw.videos && raw.videos.results) {
            details.relatedVideos = raw.videos.results;
        }

        if (raw.external_ids) {
            details.externalIds = { imdbId: raw.external_ids.imdb_id };
        }

        return details;
    }

    function fetchTmdbLogoPath(mediaId, mediaType, apiKey) {
        const segment = mediaType === 'tv' ? 'tv' : 'movie';
        const base = 'https://api.themoviedb.org/3/' + segment + '/' + mediaId + '/images';
        const filteredUrl = appendTmdbQuery(
            base,
            'include_image_language',
            getRequestModalAdvanced().backdropLanguageFilter
        );

        return fetchTmdbJson(filteredUrl, apiKey)
            .then(function (payload) {
                let path = pickLogoFilePath(payload.logos);
                if (path) {
                    return path;
                }
                // If no English logo in filtered set, retry with all languages
                return fetchTmdbJson(base, apiKey).then(function (all) {
                    return pickLogoFilePath(all.logos);
                });
            })
            .catch(function (err) {
                log.warn('TMDB logo fetch failed', err);
                return null;
            });
    }

    function fetchSettingsBackdrop(mediaId, mediaType) {
        // Modals have logos, so always use plain backdrop rather than the English backdrop used by cards.
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/backdrop/' + mediaType + '/' + mediaId, { preferNeutral: true }),
            type: 'GET',
            dataType: 'json'
        }).catch(function (err) {
            log.warn('settings backdrop fetch failed for ' + mediaType + '/' + mediaId, err);
            return null;
        });
    }

    function fetchTmdbDetailsFromBrowser(mediaId, mediaType, apiKey) {
        const isTv = mediaType === 'tv';
        const segment = isTv ? 'tv' : 'movie';
        const append = isTv
            ? 'videos,credits,content_ratings,external_ids,similar,recommendations'
            : 'videos,credits,release_dates,external_ids,similar,recommendations';
        let detailsUrl = 'https://api.themoviedb.org/3/' + segment + '/' + mediaId;
        detailsUrl = appendTmdbQuery(detailsUrl, 'append_to_response', append);

        return Promise.all([
            fetchTmdbJson(detailsUrl, apiKey),
            fetchTmdbLogoPath(mediaId, mediaType, apiKey),
            fetchSettingsBackdrop(mediaId, mediaType)
        ]).then(function (results) {
            const raw = results[0];
            const logoPath = results[1];
            const backdrop = results[2];
            const mapped = isTv ? mapTvDetails(raw) : mapMovieDetails(raw);
            if (logoPath) {
                mapped.logoPath = logoPath;
            }
            if (backdrop) {
                mapped.backdropUrl = resolveImageUrl(backdrop.backdropUrl || backdrop.BackdropUrl || '');
                mapped.backdropPath = backdrop.tmdbBackdropPath || backdrop.TmdbBackdropPath || mapped.backdropPath;
            }
            return mapped;
        }).catch(function (err) {
            log.warn('TMDB details fetch failed', err);
            return null;
        });
    }

    function mergeJellyseerrOverlay(tmdbDetails, jellyseerrDetails) {
        if (!jellyseerrDetails) {
            return tmdbDetails;
        }

        // Tmdb gives rich metadata. Seer adds request/availability
        if (jellyseerrDetails.mediaInfo) {
            tmdbDetails.mediaInfo = jellyseerrDetails.mediaInfo;
        }

        ['mediaAdded', 'status', 'status4k', 'inProduction'].forEach(function (key) {
            if (jellyseerrDetails[key] !== undefined && jellyseerrDetails[key] !== null) {
                tmdbDetails[key] = jellyseerrDetails[key];
            }
        });

        ['similar', 'recommendations', 'Similar', 'Recommendations'].forEach(function (key) {
            const mapped = mapRelatedList(jellyseerrDetails[key]);
            const targetKey = key.toLowerCase();
            if (mapped.length && (!tmdbDetails[targetKey] || !tmdbDetails[targetKey].length)) {
                tmdbDetails[targetKey] = mapped;
            }
        });

        const seerrSeasons = mapTmdbSeasons(jellyseerrDetails.seasons || jellyseerrDetails.Seasons);
        if (seerrSeasons.length) {
            const byNumber = {};
            (tmdbDetails.seasons || []).forEach(function (season) {
                byNumber[season.seasonNumber] = season;
            });
            seerrSeasons.forEach(function (season) {
                const existing = byNumber[season.seasonNumber];
                if (existing) {
                    if (!existing.posterPath && season.posterPath) {
                        existing.posterPath = season.posterPath;
                    }
                    if (!existing.episodeCount && season.episodeCount) {
                        existing.episodeCount = season.episodeCount;
                    }
                    if (!existing.name && season.name) {
                        existing.name = season.name;
                    }
                } else {
                    byNumber[season.seasonNumber] = season;
                }
            });
            tmdbDetails.seasons = Object.keys(byNumber).map(function (key) {
                return byNumber[key];
            });
        }

        return tmdbDetails;
    }

    function normalizeMediaStatus(raw) {
        if (raw == null || raw === '') {
            return null;
        }
        if (typeof raw === 'number' && !Number.isNaN(raw)) {
            return raw;
        }
        const key = String(raw).trim().toUpperCase();
        const map = {
            UNKNOWN: 1,
            PENDING: 2,
            PROCESSING: 3,
            PARTIALLY_AVAILABLE: 4,
            AVAILABLE: 5,
            DELETED: 6,
            BLACKLISTED: 7,
            BLOCKED: 7
        };
        if (key in map) {
            return map[key];
        }
        const asNum = parseInt(key, 10);
        return Number.isNaN(asNum) ? null : asNum;
    }

    function pendingContextMatches(is4k) {
        return !!(pendingRequestContext && pendingRequestContext.requestId && !!pendingRequestContext.is4k === !!is4k);
    }

    function pendingContextIsActive(is4k) {
        if (!pendingContextMatches(is4k)) {
            return false;
        }
        if (pendingRequestContext.isPending || pendingRequestContext.isFailed) {
            return true;
        }
        const status = Number(pendingRequestContext.requestStatus);
        return status === 1 || status === 2 || status === 4;
    }

    function hasActiveRequest(data, is4k) {
        return getRequestRecords(data).some(function (req) {
            if (!!is4k !== requestIs4k(req)) {
                return false;
            }
            const status = requestStatusOf(req);
            return status === 1 || status === 2 || status === 4;
        }) || pendingContextIsActive(is4k);
    }

    function getEditableRequests(data, is4k) {
        return getRequestRecords(data).filter(function (req) {
            if (!!is4k !== requestIs4k(req)) {
                return false;
            }
            const status = requestStatusOf(req);
            return (status === 1 || status === 2) && requestIdOf(req);
        });
    }

    function getRequestButtonState(data, is4k) {
        const defaultLabel = is4k ? 'Request 4K' : 'Request';
        const info = data && (data.mediaInfo || data.media_info);
        const raw = is4k
            ? (info && (info.status4k != null ? info.status4k : info.status4K)) ?? (data && data.status4k)
            : (info && info.status != null ? info.status : (data && data.status));
        const status = normalizeMediaStatus(raw);
        const active = hasActiveRequest(data, is4k);

        if (status === 5) {
            return { requested: true, label: 'Available' };
        }
        if (status === 7) {
            return { requested: true, label: 'Blocklisted' };
        }
        if (status === 4) {
            return { requested: false, label: is4k ? defaultLabel : 'Request seasons' };
        }

        // After cancel, Seerr often leaves an empty mediaInfo stub (unknown/processing)
        // with no live request. That must be requestable again.
        if (!active) {
            return { requested: false, label: defaultLabel };
        }

        if (status === 2) {
            return { requested: true, label: 'Pending' };
        }
        if (status === 3) {
            return { requested: true, label: 'Processing' };
        }
        return { requested: true, label: 'Already requested' };
    }

    function pickVal(obj) {
        if (!obj) {
            return undefined;
        }
        for (let i = 1; i < arguments.length; i++) {
            if (obj[arguments[i]] !== undefined) {
                return obj[arguments[i]];
            }
        }
        return undefined;
    }

    function getRequestRecords(data) {
        const info = (data && (data.mediaInfo || data.media_info)) || {};
        const list = info.requests || info.Requests || (data && (data.requests || data.Requests)) || [];
        return Array.isArray(list) ? list : [];
    }

    function requestIdOf(req) {
        return req && (req.id || req.Id);
    }

    function requestStatusOf(req) {
        return Number(req && (req.status != null ? req.status : req.Status));
    }

    function requestIs4k(req) {
        return !!(req && (req.is4k || req.Is4k));
    }

    function getPendingRequests(data) {
        return getRequestsByStatus(data, [1], function () {
            return pendingRequestContext && pendingRequestContext.isPending;
        });
    }

    function getCancellableRequests(data) {
        return getRequestsByStatus(data, [1, 2], function () {
            if (!pendingRequestContext || !pendingRequestContext.requestId) {
                return false;
            }
            if (pendingRequestContext.isPending) {
                return true;
            }
            if (pendingRequestContext.isFailed) {
                return false;
            }
            const status = Number(pendingRequestContext.requestStatus);
            return !Number.isFinite(status) || status === 1 || status === 2;
        });
    }

    function getFailedRequests(data) {
        return getRequestsByStatus(data, [4], function () {
            return pendingRequestContext && pendingRequestContext.isFailed;
        });
    }

    function getRequestsByStatus(data, statuses, useContext) {
        const fromSeerr = getRequestRecords(data).filter(function (req) {
            return statuses.indexOf(requestStatusOf(req)) !== -1 && requestIdOf(req);
        });
        if (fromSeerr.length) {
            return fromSeerr;
        }
        if (pendingRequestContext && pendingRequestContext.requestId && useContext()) {
            const status = Number(pendingRequestContext.requestStatus);
            return [{
                id: pendingRequestContext.requestId,
                status: Number.isFinite(status) ? status : statuses[0],
                is4k: pendingRequestContext.is4k === true
            }];
        }
        return [];
    }

    function getRequestSeasons(data) {
        const seasons = [];
        function addSeason(n) {
            if (Number.isFinite(n) && seasons.indexOf(n) === -1) {
                seasons.push(n);
            }
        }
        getRequestRecords(data).forEach(function (req) {
            const list = req.seasons || req.Seasons || [];
            list.forEach(function (entry) {
                addSeason(typeof entry === 'number' ? entry : (entry && (entry.seasonNumber != null ? entry.seasonNumber : entry.SeasonNumber)));
            });
        });
        if (!seasons.length && pendingRequestContext && Array.isArray(pendingRequestContext.seasons)) {
            pendingRequestContext.seasons.forEach(addSeason);
        }
        return seasons;
    }

    function canOpenLocalServices() {
        return pickVal(clientSettingsCache, 'canOpenLocalServices', 'CanOpenLocalServices') === true;
    }

    function getJellyseerrBrowseUrl() {
        if (!canOpenLocalServices()) {
            return '';
        }
        return String(pickVal(clientSettingsCache, 'jellyseerrBrowseUrl', 'JellyseerrBrowseUrl') || '').replace(/\/+$/, '');
    }

    function shouldShowQuotaWarnings() {
        return pickVal(clientSettingsCache, 'showQuotaWarnings', 'ShowQuotaWarnings') !== false;
    }

    function lookupJellyfinPlayItem(tmdbId, mediaType) {
        if (!tmdbId || typeof ApiClient === 'undefined') {
            return Promise.resolve(null);
        }
        const userId = ApiClient.getCurrentUserId && ApiClient.getCurrentUserId();
        if (!userId) {
            return Promise.resolve(null);
        }
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/library-item/' + mediaType + '/' + tmdbId),
            type: 'GET',
            dataType: 'json'
        }).then(function (result) {
            const itemId = result && (result.id || result.Id);
            if (!itemId) {
                return null;
            }
            if (typeof ApiClient.getItem !== 'function') {
                return { Id: itemId };
            }
            return ApiClient.getItem(userId, itemId).then(function (item) {
                return item && (item.Id || item.id) ? item : { Id: itemId };
            }).catch(function () {
                return { Id: itemId };
            });
        }).catch(function () {
            return null;
        });
    }

    function navigateToJellyfinItem(item) {
        if (!item) {
            return;
        }
        const id = item.Id || item.id;
        if (window.AppRouter && typeof AppRouter.showItem === 'function') {
            AppRouter.showItem(item);
            return;
        }
        if (window.Dashboard && typeof Dashboard.navigate === 'function' && id) {
            Dashboard.navigate('details?id=' + encodeURIComponent(id));
        }
    }

    function openJellyseerrManage(tmdbId, mediaType) {
        const base = getJellyseerrBrowseUrl();
        if (!base || !tmdbId) {
            return;
        }
        const segment = mediaType === 'tv' ? 'tv' : 'movie';
        window.open(base + '/' + segment + '/' + tmdbId + '?manage=1', '_blank', 'noopener,noreferrer');
    }

    function canUnmonitor() {
        return !!(pickVal(clientSettingsCache, 'sonarrConfigured', 'SonarrConfigured') || pickVal(clientSettingsCache, 'sonarrUrl', 'SonarrUrl'))
            || !!(pickVal(clientSettingsCache, 'radarrConfigured', 'RadarrConfigured') || pickVal(clientSettingsCache, 'radarrUrl', 'RadarrUrl'));
    }

    function isOnWatchlist(data) {
        if (!data) {
            return false;
        }
        if (data.onWatchlist === true || data.OnWatchlist === true) {
            return true;
        }
        const info = data.mediaInfo || data.media_info || {};
        if (info.onWatchlist === true || info.OnWatchlist === true) {
            return true;
        }
        const lists = info.watchlists || info.watchLists || info.Watchlists || data.watchlists;
        return Array.isArray(lists) && lists.length > 0;
    }

    function canManageRequests() {
        return pickVal(clientSettingsCache, 'canManageRequests', 'CanManageRequests') === true;
    }

    function isAdminUser() {
        return pickVal(clientSettingsCache, 'isAdmin', 'IsAdmin') === true || canManageRequests();
    }

    function currentSeerrUserId() {
        return Number(pickVal(clientSettingsCache, 'seerrUserId', 'SeerrUserId')) || 0;
    }

    function requestOwnerId(req) {
        const by = req && (req.requestedBy || req.RequestedBy || req.requested_by);
        if (!by || typeof by !== 'object') {
            return null;
        }
        return by.id || by.Id || null;
    }

    function requestOwnerName(req) {
        const by = req && (req.requestedBy || req.RequestedBy || req.requested_by);
        if (!by) {
            return '';
        }
        if (typeof by === 'string') {
            return by;
        }
        return by.displayName || by.DisplayName || by.username || by.Username || '';
    }

    function canModifyRequest(req) {
        if (isAdminUser()) {
            return true;
        }
        const owner = requestOwnerId(req);
        const me = currentSeerrUserId();
        if (owner && me) {
            return Number(owner) === Number(me);
        }
        return !!(pendingRequestContext && pendingRequestContext.requestId && requestIdOf(req) === pendingRequestContext.requestId);
    }

    function canUnmonitorTitle(data) {
        if (!canUnmonitor()) {
            return false;
        }
        if (isAdminUser()) {
            return true;
        }
        return getRequestRecords(data).some(canModifyRequest);
    }

    function requestStatusLabel(req) {
        const status = requestStatusOf(req);
        if (status === 1) {
            return 'Pending';
        }
        if (status === 2) {
            return 'Approved';
        }
        if (status === 3) {
            return 'Declined';
        }
        if (status === 4) {
            return 'Failed';
        }
        return '';
    }

    function requestSeasonNumbers(req) {
        const seasons = [];
        const list = (req && (req.seasons || req.Seasons)) || [];
        list.forEach(function (entry) {
            const n = typeof entry === 'number' ? entry : (entry && (entry.seasonNumber != null ? entry.seasonNumber : entry.SeasonNumber));
            if (Number.isFinite(n) && seasons.indexOf(n) === -1) {
                seasons.push(n);
            }
        });
        return seasons.sort(function (a, b) { return a - b; });
    }

    function parseResolutionLabel(profileName, is4k) {
        const text = String(profileName || '').toLowerCase();
        if (/2160|3840|\b4k\b|\buhd\b|ultra[\s-]?hd/.test(text)) {
            return '4K';
        }
        if (/1440|2560|\b2k\b|\bqhd\b/.test(text)) {
            return '2K';
        }
        if (/1080|1920|\bfhd\b|full[\s-]?hd/.test(text)) {
            return '1080p';
        }
        if (/720|1280/.test(text)) {
            return '720p';
        }
        if (/(?:^|[^a-z0-9])(?:576|480|360|sd|dvd|sdtv)(?:[^a-z0-9]|$)/.test(text)) {
            return 'SD';
        }
        return is4k ? '4K' : '';
    }

    function qualityLabelFromRequest(req) {
        const existing = req && (req.qualityLabel || req.QualityLabel);
        if (existing) {
            return existing;
        }
        return parseResolutionLabel(req && (req.profileName || req.ProfileName), requestIs4k(req));
    }

    function sameQualityText(quality, profile) {
        if (!quality || !profile) {
            return false;
        }
        const normalize = function (value) {
            return String(value).toLowerCase().replace(/[^a-z0-9]/g, '');
        };
        const nq = normalize(quality);
        const np = normalize(profile);
        return nq === np || np === nq + 'p';
    }

    function formatRequestSummary(req, mediaType) {
        const parts = [];
        const quality = qualityLabelFromRequest(req);
        const profile = req && (req.profileName || req.ProfileName);
        if (quality) {
            parts.push(quality);
        }
        if (profile && !sameQualityText(quality, profile)) {
            parts.push(profile);
        }
        if (!quality && !profile) {
            parts.push(requestIs4k(req) ? '4K' : 'Requested');
        }
        const seasons = requestSeasonNumbers(req);
        if (mediaType === 'tv' && seasons.length) {
            parts.push(seasons.length === 1 ? ('Season ' + seasons[0]) : ('Seasons ' + seasons.join(', ')));
        }
        const status = requestStatusLabel(req);
        if (status) {
            parts.push(status);
        }
        const by = requestOwnerName(req);
        if (by) {
            parts.push('Requested by ' + by);
        }
        return parts.join(' · ');
    }

    function getActiveRequestSummaries(data) {
        const seen = {};
        const list = [];
        getRequestRecords(data).forEach(function (req) {
            const status = requestStatusOf(req);
            const id = requestIdOf(req);
            if (!id || seen[id] || (status !== 1 && status !== 2 && status !== 4)) {
                return;
            }
            seen[id] = true;
            list.push(req);
        });
        if (!list.length && pendingRequestContext && pendingRequestContext.requestId && !seen[pendingRequestContext.requestId]) {
            list.push({
                id: pendingRequestContext.requestId,
                status: pendingRequestContext.requestStatus,
                is4k: pendingRequestContext.is4k === true,
                seasons: pendingRequestContext.seasons || []
            });
        }
        return list;
    }

    function renderActiveRequestSummaries(data, mediaType) {
        const reqs = getActiveRequestSummaries(data);
        if (!reqs.length) {
            return '';
        }
        return `<div class="bst-request-summaries">${reqs.map(function (req) {
            return `<div class="bst-request-summary">${escapeHtml(formatRequestSummary(req, mediaType))}</div>`;
        }).join('')}</div>`;
    }

    function shouldConfirmCancel() {
        return pickVal(clientSettingsCache, 'confirmCancel', 'ConfirmCancel') !== false;
    }

    function renderRequestLifecycleButtons(data, mediaType) {
        const pending = getPendingRequests(data);
        const cancellable = getCancellableRequests(data).filter(canModifyRequest);
        const failed = getFailedRequests(data).filter(canModifyRequest);
        const manage = canManageRequests();
        const parts = [];

        cancellable.forEach(function (req) {
            const id = requestIdOf(req);
            const fourK = requestIs4k(req);
            parts.push(`<button type="button" class="bst-btn-danger" data-action="cancel-request" data-request-id="${id}">${fourK ? 'Cancel 4K request' : 'Cancel request'}</button>`);
        });

        pending.forEach(function (req) {
            const id = requestIdOf(req);
            const fourK = requestIs4k(req);
            if (manage) {
                parts.push(`<button type="button" class="bst-btn-success" data-action="approve-request" data-request-id="${id}">${fourK ? 'Approve 4K' : 'Approve'}</button>`);
                parts.push(`<button type="button" class="bst-btn-trailer" data-action="decline-request" data-request-id="${id}">${fourK ? 'Decline 4K' : 'Decline'}</button>`);
            }
        });

        failed.forEach(function (req) {
            const id = requestIdOf(req);
            parts.push(`<button type="button" class="bst-btn-trailer" data-action="retry-request" data-request-id="${id}">${requestIs4k(req) ? 'Retry 4K' : 'Retry request'}</button>`);
        });

        const editable = getEditableRequests(data, false).concat(getEditableRequests(data, true)).filter(canModifyRequest);
        const seenEdit = {};
        editable.forEach(function (req) {
            const id = requestIdOf(req);
            if (!id || seenEdit[id]) {
                return;
            }
            seenEdit[id] = true;
            const fourK = requestIs4k(req);
            parts.push(`<button type="button" class="bst-btn-trailer" data-action="change-request" data-request-id="${id}" data-is-4k="${fourK ? '1' : '0'}">${fourK ? 'Change 4K request' : 'Change request'}</button>`);
        });

        if (canUnmonitorTitle(data)) {
            parts.push(`<button type="button" class="bst-btn-ghost" data-action="unmonitor">Unmonitor</button>`);
        }

        return parts.join('');
    }

    function reloadDetailsModal(mediaId, mediaType) {
        return loadModalDetails(mediaId, mediaType).then(function (data) {
            const dom = buildDetailsDom(data, mediaId, mediaType);
            if (activeDetailsRoot) {
                activeDetailsRoot.replaceWith(dom);
                activeDetailsRoot = dom;
            }
            if (typeof window.__jellySeerrRequestsEnsureMounted === 'function') {
                window.__jellySeerrRequestsEnsureMounted({ tabShown: true });
            }
        }).catch(function (err) {
            log.error('details reload failed', err);
        });
    }

    function markRequestButton(is4k, label) {
        if (!activeDetailsRoot) {
            return;
        }
        const btn = activeDetailsRoot.querySelector(is4k ? '[data-action="request-4k"]' : '[data-action="request"]');
        if (!btn) {
            return;
        }
        btn.disabled = true;
        btn.textContent = label || 'Already requested';
    }

    function fetchJustWatchQualities(mediaId, mediaType) {
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/justwatch/qualities/' + mediaType + '/' + mediaId),
            type: 'GET',
            dataType: 'json'
        }).catch(function (err) {
            if (err && err.status === 404) {
                return null;
            }
            log.warn('JustWatch qualities fetch failed', err);
            return null;
        });
    }

    function buildJustWatchQualityLines(mediaId, mediaType) {
        const qualityLines = mountFromHtml(`
            <div class="bst-sidebar-lines bst-sidebar-lines--qualities" hidden>
                <div><span class="bst-label">Highest released quality:</span> <span class="bst-quality-value">…</span></div>
                <div><span class="bst-label">Most common quality:</span> <span class="bst-quality-value">…</span></div>
            </div>`);

        fetchJustWatchQualities(mediaId, mediaType).then(function (result) {
            if (!result) {
                qualityLines.remove();
                return;
            }

            const values = qualityLines.querySelectorAll('.bst-quality-value');
            values[0].textContent =
                result.highestReleasedQuality || result.HighestReleasedQuality || 'Unknown';
            values[1].textContent =
                result.mostCommonQuality || result.MostCommonQuality || 'Unknown';
            qualityLines.hidden = false;
        });

        return qualityLines;
    }

    function fetchJellyseerrDetails(mediaId, mediaType) {
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/details/' + mediaType + '/' + mediaId),
            type: 'GET',
            dataType: 'json'
        });
    }

    function loadClientSettings() {
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/client-settings'),
            type: 'GET',
            dataType: 'json'
        }).then(function (config) {
            clientSettingsCache = config || {};
            return clientSettingsCache;
        }).catch(function (err) {
            log.warn('client settings fetch failed. falling back to plugin config', err);
            return ApiClient.getPluginConfiguration(PLUGIN_ID).then(function (config) {
                clientSettingsCache = {
                    tmdbApiKey: (config && (config.TmdbApiKey || config.tmdbApiKey)) || '',
                    canOpenLocalServices: false,
                    jellyseerrBrowseUrl: '',
                    radarrUrl: '',
                    sonarrUrl: '',
                    showQuotaWarnings: !config || (config.ShowQuotaWarnings !== false && config.showQuotaWarnings !== false)
                };
                return clientSettingsCache;
            }).catch(function (configErr) {
                log.warn('plugin config fetch failed', configErr);
                clientSettingsCache = {};
                return {};
            });
        });
    }

    function loadModalDetails(mediaId, mediaType) {
        return loadClientSettings().then(function (config) {
                const apiKey = getTmdbApiKey(config);
                const jellyseerrPromise = fetchJellyseerrDetails(mediaId, mediaType);

                if (!apiKey) {
                    return jellyseerrPromise;
                }

                // Fetch both sources but prefer TMDB content with Seerr status
                return Promise.all([
                    jellyseerrPromise,
                    fetchTmdbDetailsFromBrowser(mediaId, mediaType, apiKey)
                ]).then(function (results) {
                    const jellyseerr = results[0];
                    const tmdb = results[1];
                    if (tmdb) {
                        return mergeJellyseerrOverlay(tmdb, jellyseerr);
                    }
                    return jellyseerr;
                });
            });
    }

    function formatRuntime(minutes) {
        if (!minutes) {
            return '';
        }
        const h = Math.floor(minutes / 60);
        const m = minutes % 60;
        if (h && m) {
            return h + 'h ' + m + 'm';
        }
        if (h) {
            return h + 'h';
        }
        return m + 'm';
    }

    function formatEndsAt(minutes) {
        if (!minutes) {
            return '';
        }
        const end = new Date(Date.now() + minutes * 60 * 1000);
        return end.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
    }

    function formatReleaseDate(dateStr) {
        if (!dateStr) {
            return '';
        }
        const d = new Date(dateStr);
        if (isNaN(d.getTime())) {
            return dateStr;
        }
        return d.toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' });
    }

    function getCertification(data, mediaType) {
        // Get a US certification from Tmdb release_dates (movies) or content_ratings (TV)
        if (mediaType === 'movie' && data.releaseDates) {
            const us = (data.releaseDates.results || []).find(function (r) { return r.iso_3166_1 === 'US'; });
            const rel = us && us.release_dates && us.release_dates.find(function (rd) { return rd.certification; });
            if (rel && rel.certification) {
                return rel.certification;
            }
        }
        if (mediaType === 'tv' && data.contentRatings) {
            const us = (data.contentRatings.results || []).find(function (r) { return r.iso_3166_1 === 'US'; });
            if (us && us.rating) {
                return us.rating;
            }
        }
        return '';
    }

    function getTrailerKey(data) {
        const videos = data.relatedVideos || data.videos;
        const results = videos && (videos.results || videos);
        if (!results || !results.length) {
            return null;
        }
        const trailer = results.find(function (v) {
            return v.type === 'Trailer' && v.site === 'YouTube';
        }) || results[0];
        return trailer && trailer.key ? trailer.key : null;
    }

    function getCast(data) {
        const credits = data.credits || data.aggregateCredits;
        const cast = credits && (credits.cast || []);
        const crew = credits && (credits.crew || []);
        const directors = crew.filter(function (c) { return c.job === 'Director'; }).slice(0, 2);
        const list = [];

        directors.forEach(function (d) {
            list.push({
                id: d.id,
                name: d.name,
                role: 'Director',
                profile_path: d.profilePath || d.profile_path
            });
        });

        (cast || []).slice(0, 24).forEach(function (c) {
            list.push({
                id: c.id,
                name: c.name,
                role: c.character || (c.roles && c.roles[0] && c.roles[0].character) || '',
                profile_path: c.profilePath || c.profile_path
            });
        });

        return list;
    }

    function removeEscapeHandler() {
        if (escapeHandler) {
            document.removeEventListener('keydown', escapeHandler);
            escapeHandler = null;
        }
    }

    function closeQualityModal() {
        if (activeQualityRoot) {
            activeQualityRoot.remove();
            activeQualityRoot = null;
        }
    }

    function closeSeasonModal() {
        if (activeSeasonRoot) {
            activeSeasonRoot.remove();
            activeSeasonRoot = null;
        }
    }

    function closeDetailsModal() {
        pendingRequestContext = null;
        closeQualityModal();
        closeSeasonModal();
        if (activeDetailsRoot) {
            activeDetailsRoot.remove();
            activeDetailsRoot = null;
        }
        removeEscapeHandler();
        document.body.style.overflow = '';
    }

    function getRequestableSeasons(details) {
        const includeSpecials = getRequestModalAdvanced().includeSpecialsSeason === true;
        return mapTmdbSeasons(details && (details.seasons || details.Seasons))
            .filter(function (season) {
                if (season.episodeCount === 0) {
                    return false;
                }
                if (!includeSpecials && season.seasonNumber <= 0) {
                    return false;
                }
                return true;
            })
            .sort(function (a, b) {
                return a.seasonNumber - b.seasonNumber;
            });
    }

    function getSeasonMediaStatus(details, seasonNumber) {
        const info = (details && (details.mediaInfo || details.media_info)) || {};
        const list = info.seasons || info.Seasons || [];
        for (let i = 0; i < list.length; i++) {
            const entry = list[i];
            const num = entry && (entry.seasonNumber != null ? entry.seasonNumber : entry.SeasonNumber);
            if (Number(num) === seasonNumber) {
                return normalizeMediaStatus(entry.status != null ? entry.status : entry.Status);
            }
        }
        return null;
    }

    function annotateRequestableSeasons(details, options) {
        const requested = getRequestSeasons(details);
        const allowRequested = !!(options && options.allowRequested);
        return getRequestableSeasons(details).map(function (season) {
            const status = getSeasonMediaStatus(details, season.seasonNumber);
            const isAvailable = status === 5;
            const isProcessing = status === 3;
            const isPending = status === 2;
            const wasRequested = requested.indexOf(season.seasonNumber) !== -1;
            const requestable = status === 4
                || (!isAvailable && !isProcessing && !isPending && !wasRequested)
                || (allowRequested && wasRequested && !isAvailable);
            let badge = '';
            if (isAvailable) {
                badge = 'Available';
            } else if (isProcessing && !allowRequested) {
                badge = 'Processing';
            } else if ((isPending || wasRequested) && !requestable) {
                badge = 'Requested';
            }
            return {
                seasonNumber: season.seasonNumber,
                name: season.name || season.Name || '',
                episodeCount: season.episodeCount != null ? season.episodeCount : season.episode_count,
                posterPath: season.posterPath || season.poster_path || season.PosterPath || '',
                requestable: requestable,
                locked: !requestable,
                preselected: allowRequested && wasRequested && requestable,
                badge: badge
            };
        });
    }

    function notifyUser(message) {
        const text = String(message || 'Request failed');

        if (activeDetailsRoot) {
            let notice = activeDetailsRoot.querySelector('[data-request-notice]');
            if (!notice) {
                const actionsRow = activeDetailsRoot.querySelector('.bst-actions-row');
                if (!actionsRow) {
                    return;
                }
                notice = document.createElement('div');
                notice.className = 'bst-request-notice';
                notice.setAttribute('data-request-notice', '1');
                notice.setAttribute('role', 'alert');
                actionsRow.insertAdjacentElement('afterend', notice);
            }

            notice.textContent = text;
            notice.hidden = false;
            return;
        }

        try {
            if (typeof Dashboard !== 'undefined' && typeof Dashboard.alert === 'function') {
                Dashboard.alert(text);
                return;
            }
        } catch (err) {}
        window.alert(text);
    }

    function readAjaxErrorMessage(err, fallback) {
        const fallbackText = fallback || 'That action failed.';
        if (err && err.responseJSON && (err.responseJSON.message || err.responseJSON.Message || err.responseJSON.error)) {
            return Promise.resolve(String(err.responseJSON.message || err.responseJSON.Message || err.responseJSON.error));
        }
        if (err && typeof err.text === 'function') {
            const reader = typeof err.clone === 'function' ? err.clone() : err;
            return reader.text().then(function (text) {
                try {
                    const body = JSON.parse(text);
                    return String(body.message || body.Message || body.error || text || fallbackText);
                } catch (e) {
                    return text || fallbackText;
                }
            }).catch(function () {
                return fallbackText;
            });
        }
        if (err && typeof err.message === 'string' && err.message && err.message !== 'Error') {
            return Promise.resolve(err.message);
        }
        return Promise.resolve(fallbackText);
    }

    function unmonitorTitle(mediaType, mediaId, seasons) {
        return ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/servarr/unmonitor'),
            type: 'POST',
            data: JSON.stringify({
                MediaType: mediaType,
                MediaId: mediaId,
                Seasons: Array.isArray(seasons) ? seasons : []
            }),
            contentType: 'application/json',
            dataType: 'json'
        });
    }

    function renderDetailsStatusNotice(data) {
        if (getFailedRequests(data).length) {
            return `<div class="bst-request-notice" data-request-notice="1" role="status">Seerr could not find any matching files yet.</div>`;
        }
        return '';
    }

    function submitRequest(mediaId, mediaType, option, onSuccess, onError) {
        const payload = {
            MediaType: mediaType,
            mediaType: mediaType,
            MediaId: parseInt(mediaId, 10),
            mediaId: parseInt(mediaId, 10),
            Is4k: !!option.is4k
        };

        if (option.serverId != null && !Number.isNaN(Number(option.serverId))) {
            payload.ServerId = Number(option.serverId);
        }
        if (option.profileId != null && !Number.isNaN(Number(option.profileId))) {
            payload.ProfileId = Number(option.profileId);
        }
        if (option.rootFolder) {
            payload.RootFolder = option.rootFolder;
        }

        if (mediaType === 'tv' && option.seasons && option.seasons.length) {
            payload.Seasons = option.seasons.slice().sort(function (a, b) { return a - b; });
        }

        const requestId = option.requestId ? parseInt(option.requestId, 10) : 0;
        const isUpdate = Number.isFinite(requestId) && requestId > 0;

        return ApiClient.ajax({
            url: isUpdate ? ApiClient.getUrl('JellySeerr/request/' + requestId) : ApiClient.getUrl('JellySeerr/request'),
            type: isUpdate ? 'PUT' : 'POST',
            data: JSON.stringify(payload),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json'
        }).then(function (response) {
            // Seerr gives 202 with only a message when every selected season already exists in Seerr
            const apiMessage = response && response.errors && response.errors.length > 0
                ? 'Request failed. Check logs for details.'
                : (response && response.message && response.id == null ? String(response.message) : '');

            if (apiMessage) {
                log.error('request was not created for ' + mediaType + '/' + mediaId, response);
                if (typeof onError === 'function') {
                    onError(apiMessage);
                } else {
                    notifyUser(apiMessage);
                }
                return Promise.reject({ handled: true });
            }
            log.info('request submitted for ' + mediaType + '/' + mediaId);
            // TV might still have more seasons to request (so don't lock the button)
            if (mediaType !== 'tv') {
                markRequestButton(!!option.is4k, 'Already requested');
            }
            if (typeof onSuccess === 'function') {
                onSuccess();
            }
        }).catch(function (err) {
            if (err && err.handled) {
                return Promise.reject(err);
            }
            if (err && err.status === 409) {
                if (mediaType !== 'tv') {
                    markRequestButton(!!option.is4k, 'Already requested');
                }
                if (typeof onSuccess === 'function') {
                    onSuccess();
                }
                return;
            }

            let messagePromise;
            if (err && err.responseJSON && (err.responseJSON.message || err.responseJSON.error)) {
                messagePromise = Promise.resolve(String(err.responseJSON.message || err.responseJSON.error));
            } else if (err && typeof err.text === 'function') {
                // Jellyfin ApiClient (fetch) often rejects with a Response object.
                const reader = typeof err.clone === 'function' ? err.clone() : err;
                messagePromise = reader.text().then(function (text) {
                    try {
                        const body = JSON.parse(text);
                        return String(body.message || body.error || text || 'Request failed');
                    } catch (e) {
                        return text || 'Request failed';
                    }
                }).catch(function () {
                    return 'Request failed';
                });
            } else if (err && typeof err.message === 'string' && err.message) {
                messagePromise = Promise.resolve(err.message);
            } else {
                messagePromise = Promise.resolve('Request failed');
            }

            return messagePromise.then(function (message) {
                log.error('request failed for ' + mediaType + '/' + mediaId, err);
                if (typeof onError === 'function') {
                    onError(message);
                } else {
                    notifyUser(message);
                }
                return Promise.reject(err);
            });
        });
    }

    function renderSeasonModalShell() {
        return `
            <div class="bst-quality-wrapper">
                <div class="bst-quality-backdrop"></div>
                <div class="bst-quality-panel" role="dialog" aria-modal="true">
                    <div class="bst-quality-header">
                        <h3 id="bst-season-title">Select seasons</h3>
                        <button type="button" class="bst-quality-close" aria-label="Close">${CLOSE_ICON}</button>
                    </div>
                    <div class="bst-quality-list"><div class="bst-quality-loading">Loading seasons…</div></div>
                    <div class="bst-quality-footer">
                        <button type="button" class="bst-quality-continue" disabled>Continue</button>
                    </div>
                </div>
            </div>`;
    }

    function renderSeasonList(seasons) {
        const rowsHtml = seasons.map(function (season) {
            const seasonNumber = season.seasonNumber;
            const displayName = season.name && season.name !== `Season ${seasonNumber}`
                ? escapeHtml(season.name)
                : `Season ${seasonNumber}`;
            const episodesHtml = season.episodeCount
                ? `<span class="bst-season-episodes"> (${season.episodeCount}${season.episodeCount === 1 ? ' episode' : ' episodes'})</span>`
                : '';
            const posterSrc = season.posterPath ? tmdbImage(season.posterPath, 'w92') : '';
            const posterHtml = posterSrc
                ? `<img class="bst-season-poster" src="${escapeHtml(posterSrc)}" alt="" />`
                : '<span class="bst-season-poster bst-season-poster--empty" aria-hidden="true"></span>';
            const badgeHtml = season.badge ? `<span class="bst-season-badge">${escapeHtml(season.badge)}</span>` : '';
            const locked = season.locked || season.requestable === false;
            const checked = (locked && season.badge === 'Available') || season.preselected ? ' checked' : '';
            const disabled = locked ? ' disabled' : '';
            return `
                <label class="bst-season-option${locked ? ' is-locked' : ''}">
                    ${posterHtml}
                    <input type="checkbox" class="bst-season-checkbox bst-season-row-input" value="${seasonNumber}" data-requestable="${locked ? '0' : '1'}"${checked}${disabled} />
                    <span class="bst-season-label">${displayName}${episodesHtml}</span>
                    ${badgeHtml}
                </label>`;
        }).join('');

        return `
            <label class="bst-season-option bst-season-select-all">
                <input type="checkbox" class="bst-season-checkbox" data-select-all-seasons />
                <span class="bst-season-label">Select all</span>
            </label>
            ${rowsHtml}`;
    }

    function bindSeasonList(root, seasons, ctx) {
        const list = root.querySelector('.bst-quality-list');
        const continueBtn = root.querySelector('.bst-quality-continue');
        const selectedSeasons = ctx.selectedSeasons;
        const requestable = seasons.filter(function (s) { return s.requestable !== false; });

        function syncSelectAll() {
            const seasonNumbers = requestable.map(function (s) { return s.seasonNumber; });
            const selectAllInput = list.querySelector('[data-select-all-seasons]');
            if (!selectAllInput) {
                return;
            }
            selectAllInput.checked = seasonNumbers.length > 0 && seasonNumbers.every(function (num) {
                return selectedSeasons.indexOf(num) !== -1;
            });
            selectAllInput.indeterminate = selectedSeasons.length > 0 && !selectAllInput.checked;
            selectAllInput.disabled = seasonNumbers.length === 0;
            const requireExplicit = getRequestModalAdvanced().requireExplicitSeasonSelection === true;
            continueBtn.disabled = (requireExplicit && selectedSeasons.length === 0) || requestable.length === 0;
        }

        list.addEventListener('change', function (event) {
            const selectAllInput = event.target.closest('[data-select-all-seasons]');
            if (selectAllInput) {
                if (selectAllInput.checked) {
                    ctx.selectedSeasons.length = 0;
                    requestable.forEach(function (s) {
                        ctx.selectedSeasons.push(s.seasonNumber);
                    });
                } else {
                    ctx.selectedSeasons.length = 0;
                }
                list.querySelectorAll('.bst-season-row-input').forEach(function (input) {
                    if (input.disabled) {
                        return;
                    }
                    input.checked = ctx.selectedSeasons.indexOf(parseInt(input.value, 10)) !== -1;
                });
                syncSelectAll();
                return;
            }

            const rowInput = event.target.closest('.bst-season-row-input');
            if (!rowInput || rowInput.disabled) {
                return;
            }

            const seasonNumber = parseInt(rowInput.value, 10);
            if (rowInput.checked) {
                if (ctx.selectedSeasons.indexOf(seasonNumber) === -1) {
                    ctx.selectedSeasons.push(seasonNumber);
                }
            } else {
                const idx = ctx.selectedSeasons.indexOf(seasonNumber);
                if (idx !== -1) {
                    ctx.selectedSeasons.splice(idx, 1);
                }
            }
            syncSelectAll();
        });

        syncSelectAll();
    }

    function openSeasonModal(mediaId, mediaType, title, onSuccess, is4k, requestId) {
        closeSeasonModal();
        is4k = !!is4k;

        document.body.insertAdjacentHTML('beforeend', renderSeasonModalShell());
        activeSeasonRoot = document.body.lastElementChild;

        const ctx = { selectedSeasons: [] };
        const continueBtn = activeSeasonRoot.querySelector('.bst-quality-continue');
        const list = activeSeasonRoot.querySelector('.bst-quality-list');

        activeSeasonRoot.querySelector('.bst-quality-backdrop').addEventListener('click', closeSeasonModal);
        activeSeasonRoot.querySelector('.bst-quality-close').addEventListener('click', closeSeasonModal);

        continueBtn.addEventListener('click', function () {
            const seasons = ctx.selectedSeasons.slice();
            closeSeasonModal();
            openQualityModal(mediaId, mediaType, title, onSuccess, is4k, seasons, requestId);
        });

        loadClientSettings().then(function () {
            return fetchJellyseerrDetails(mediaId, mediaType);
        }).then(function (details) {
            details = details || {};
            const apiKey = getTmdbApiKey(clientSettingsCache);
            if (!apiKey) {
                return details;
            }
            return fetchTmdbJson('https://api.themoviedb.org/3/tv/' + mediaId, apiKey).then(function (raw) {
                const tmdbSeasons = mapTmdbSeasons(raw && raw.seasons);
                if (!tmdbSeasons.length) {
                    return details;
                }
                const byNumber = {};
                tmdbSeasons.forEach(function (season) {
                    byNumber[season.seasonNumber] = season;
                });
                mapTmdbSeasons(details.seasons || details.Seasons).forEach(function (season) {
                    const existing = byNumber[season.seasonNumber] || {};
                    byNumber[season.seasonNumber] = {
                        seasonNumber: season.seasonNumber,
                        name: season.name || existing.name,
                        episodeCount: season.episodeCount != null ? season.episodeCount : existing.episodeCount,
                        posterPath: season.posterPath || existing.posterPath
                    };
                });
                details.seasons = Object.keys(byNumber).map(function (key) {
                    return byNumber[key];
                });
                return details;
            }).catch(function () {
                return details;
            });
        }).then(function (details) {
            const seasons = annotateRequestableSeasons(details || {}, { allowRequested: !!requestId });

            if (!seasons.length) {
                list.innerHTML = `<div class="bst-quality-empty">No seasons available.</div>`;
                continueBtn.disabled = true;
                return;
            }

            seasons.forEach(function (season) {
                if (season.preselected) {
                    ctx.selectedSeasons.push(season.seasonNumber);
                }
            });

            list.innerHTML = renderSeasonList(seasons);
            bindSeasonList(activeSeasonRoot, seasons, ctx);
        }).catch(function (err) {
            log.error('seasons load failed', err);
            list.innerHTML = `<div class="bst-quality-empty">Failed to load seasons.</div>`;
            continueBtn.disabled = true;
        });
    }

    function renderQualityModalShell(is4k) {
        const title = is4k ? 'Choose 4K quality profile' : 'Choose quality profile';
        return `
            <div class="bst-quality-wrapper">
                <div class="bst-quality-backdrop"></div>
                <div class="bst-quality-panel" role="dialog" aria-modal="true">
                    <div class="bst-quality-header">
                        <h3 id="bst-quality-title">${title}</h3>
                        <button type="button" class="bst-quality-close" aria-label="Close">${CLOSE_ICON}</button>
                    </div>
                    <div class="bst-quality-list"><div class="bst-quality-loading">Loading profiles…</div></div>
                    <div class="bst-quality-quota" hidden></div>
                </div>
            </div>`;
    }

    function renderQualityOptions(options) {
        return options.map(function (opt) {
            const label = escapeHtml(opt.profileName || 'Default');
            const subParts = [];
            if (opt.serverName) {
                subParts.push(opt.serverName);
            }
            if (opt.is4k) {
                subParts.push('4K');
            }
            if (opt.isDefaultProfile) {
                subParts.push('default');
            }
            const subHtml = subParts.length ? `<span class="bst-quality-option-sub">${escapeHtml(subParts.join(' · '))}</span>` : '';
            const descHtml = opt.description ? `<span class="bst-quality-option-sub">${escapeHtml(opt.description)}</span>` : '';
            return `
                <button type="button" class="bst-quality-option"
                    data-server-id="${opt.serverId}" data-profile-id="${opt.profileId}"
                    data-root-folder="${escapeHtml(opt.rootFolder || '')}" data-is-4k="${opt.is4k ? '1' : '0'}">
                    ${label}
                    ${subHtml}${descHtml}
                </button>`;
        }).join('');
    }

    function fillQualityQuota(root, mediaType) {
        const el = root && root.querySelector('.bst-quality-quota');
        if (!el) {
            return;
        }
        loadClientSettings().then(function () {
            if (!shouldShowQuotaWarnings()) {
                return null;
            }
            return window.jellySeerrQuota.fetch();
        }).then(function (data) {
            if (!el.isConnected) {
                return;
            }
            const text = formatQuotaSummary(data, mediaType);
            el.textContent = text;
            el.hidden = !text;
        }).catch(function () {
            el.hidden = true;
        });
    }

    function openQualityModal(mediaId, mediaType, title, onSuccess, is4k, selectedSeasons, requestId) {
        if (mediaType === 'tv' && selectedSeasons === undefined) {
            if (getRequestModalAdvanced().tvSeasonPickerEnabled !== false) {
                openSeasonModal(mediaId, mediaType, title, onSuccess, is4k, requestId);
                return;
            }
            selectedSeasons = [];
        }

        is4k = !!is4k;

        // Show shell so the click feels instant (fill profiles when the api returns)
        closeQualityModal();
        document.body.insertAdjacentHTML('beforeend', renderQualityModalShell(is4k));
        activeQualityRoot = document.body.lastElementChild;

        const list = activeQualityRoot.querySelector('.bst-quality-list');
        activeQualityRoot.querySelector('.bst-quality-backdrop').addEventListener('click', closeQualityModal);
        activeQualityRoot.querySelector('.bst-quality-close').addEventListener('click', closeQualityModal);
        fillQualityQuota(activeQualityRoot, mediaType);

        function finishRequest() {
            closeQualityModal();
            if (typeof onSuccess === 'function') {
                onSuccess();
            }
        }

        function failRequest(message) {
            if (!list || !activeQualityRoot) {
                notifyUser(message || 'Request failed');
                return;
            }
            list.innerHTML = `<div class="bst-quality-empty">${escapeHtml(message || 'Request failed')}</div>`;
        }

        ApiClient.ajax({
            url: ApiClient.getUrl('JellySeerr/request-options/' + mediaType),
            type: 'GET',
            dataType: 'json'
        }).then(function (data) {
            if (!activeQualityRoot) {
                return;
            }

            const rawOptions = (data && (data.options || data.Options)) || [];
            const payload = {
                options: (Array.isArray(rawOptions) ? rawOptions : []).map(function (opt) {
                    if (!opt) {
                        return null;
                    }
                    return {
                        serverId: opt.serverId != null ? opt.serverId : opt.ServerId,
                        serverName: opt.serverName || opt.ServerName || '',
                        profileId: opt.profileId != null ? opt.profileId : opt.ProfileId,
                        profileName: opt.profileName || opt.ProfileName || opt.displayName || '',
                        description: opt.description || opt.Description || '',
                        rootFolder: opt.rootFolder || opt.RootFolder || '',
                        is4k: !!(opt.is4k != null ? opt.is4k : opt.Is4k),
                        isDefaultProfile: !!(opt.isDefaultProfile != null ? opt.isDefaultProfile : opt.IsDefaultProfile)
                    };
                }).filter(Boolean),
                canRequest: !!(data && (data.canRequest || data.CanRequest)),
                canRequest4k: !!(data && (data.canRequest4k || data.CanRequest4k)),
                canRequestAdvanced: !!(data && (data.canRequestAdvanced || data.CanRequestAdvanced))
            };
            const allowed = is4k ? payload.canRequest4k : payload.canRequest;
            if (!allowed) {
                closeQualityModal();
                notifyUser(is4k
                    ? 'You do not have permission to make 4K requests.'
                    : (mediaType === 'tv' ? 'You do not have permission to make series requests.' : 'You do not have permission to make movie requests.'));
                return;
            }

            if (!payload.canRequestAdvanced || !payload.options.length) {
                list.innerHTML = `<div class="bst-quality-loading">Submitting request…</div>`;
                submitRequest(mediaId, mediaType, {
                    is4k: is4k,
                    seasons: selectedSeasons,
                    requestId: requestId
                }, finishRequest, failRequest).catch(function () {});
                return;
            }

            let filteredOptions = payload.options.filter(function (opt) {
                return !!opt.is4k === is4k;
            });
            if (!filteredOptions.length) {
                filteredOptions = payload.options;
            }

            list.innerHTML = renderQualityOptions(filteredOptions);

            list.addEventListener('click', function (event) {
                const btn = event.target.closest('.bst-quality-option');
                if (!btn || btn.disabled) {
                    return;
                }

                btn.disabled = true;
                submitRequest(mediaId, mediaType, {
                    serverId: parseInt(btn.getAttribute('data-server-id'), 10),
                    profileId: parseInt(btn.getAttribute('data-profile-id'), 10),
                    rootFolder: btn.getAttribute('data-root-folder') || null,
                    is4k: btn.getAttribute('data-is-4k') === '1',
                    seasons: selectedSeasons,
                    requestId: requestId
                }, finishRequest, failRequest).catch(function () {
                    btn.disabled = false;
                });
            });
        }).catch(function (err) {
            log.error('profiles load failed', err);
            failRequest('Failed to load quality profiles.');
        });
    }

    function renderCast(cast) {
        if (!cast.length) {
            return;
        }

        const cardsHtml = cast.map(function (person) {
            const imgSrc = person.profile_path
                ? tmdbImage(person.profile_path, 'w185')
                : `data:image/svg+xml,${encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect fill="#333" width="128" height="128"/></svg>')}`;
            return `
                <a class="bst-cast-card" href="https://www.themoviedb.org/person/${person.id}" target="_blank" rel="noopener noreferrer">
                    <div class="bst-cast-avatar">
                        <img alt="${escapeHtml(person.name)}" src="${imgSrc}" />
                    </div>
                    <span class="bst-cast-name">${escapeHtml(person.name)}</span>
                    <span class="bst-cast-role">${escapeHtml(person.role)}</span>
                </a>`;
        }).join('');

        return `
            <div class="bst-cast-section">
                <div class="bst-cast-scroll">${cardsHtml}</div>
            </div>`;
    }

    function renderRelatedRow(list, heading) {
        const items = Array.isArray(list) ? list : ((list && (list.results || list.Results)) || []);
        if (!Array.isArray(items) || !items.length) {
            return '';
        }
        const cards = items.slice(0, 12).map(function (item) {
            const id = item.id || item.Id;
            const name = escapeHtml(item.title || item.name || 'Title');
            const type = item.mediaType || item.media_type || (item.firstAirDate || item.first_air_date ? 'tv' : 'movie');
            if (!id) {
                return '';
            }
            const posterPath = item.posterPath || item.poster_path || '';
            const posterSrc = posterPath ? tmdbImage(posterPath, 'w185') : '';
            const posterHtml = posterSrc
                ? `<img src="${escapeHtml(posterSrc)}" alt="" />`
                : `<span class="bst-related-poster-empty" aria-hidden="true"></span>`;
            return `<button type="button" class="bst-related-card" data-related-id="${id}" data-related-type="${type}">
                <span class="bst-related-poster">${posterHtml}</span>
                <span class="bst-related-name">${name}</span>
            </button>`;
        }).join('');
        if (!cards) {
            return '';
        }
        return `<div class="bst-related-section"><h3 class="bst-related-heading">${escapeHtml(heading)}</h3><div class="bst-related-scroll">${cards}</div></div>`;
    }

    function renderDetails(data, mediaId, mediaType) {
        const title = data.title || data.name || 'Details';
        const overview = data.overview || '';
        const year = (data.releaseDate || data.firstAirDate || '').substring(0, 4);
        const rating = data.voteAverage != null ? data.voteAverage : data.vote_average;
        const voteCount = data.voteCount != null ? data.voteCount : data.vote_count;
        const backdrop = resolveImageUrl(data.backdropUrl || data.backdrop_url || '') || tmdbImage(data.backdropPath || data.backdrop_path, 'original');
        const runtimeMinutes = data.runtime || (data.episodeRunTime && data.episodeRunTime[0]);
        const runtime = formatRuntime(runtimeMinutes);
        const endsAt = formatEndsAt(runtimeMinutes);
        const language = (data.originalLanguage || data.original_language || '').toUpperCase();
        const releaseLabel = formatReleaseDate(data.releaseDate || data.firstAirDate);
        const certification = getCertification(data, mediaType);
        const genres = data.genres || [];
        const cast = getCast(data);
        const trailerKey = getTrailerKey(data);
        const tmdbId = data.id;
        const imdbId = data.externalIds && (data.externalIds.imdbId || data.externalIds.imdb_id);
        const logoUrl = getLogoImageUrl(data);
        const requestState = getRequestButtonState(data, false);
        const request4kState = getRequestButtonState(data, true);
        const posterPath = data.posterPath || data.poster_path;
        const posterUrl = posterPath ? tmdbImage(posterPath, 'w342') : '';
        const browseUrl = getJellyseerrBrowseUrl();

        return `
            <div class="bst-popout-wrapper">
                <div class="bst-popout-backdrop"></div>
                <div class="bst-popout-center">
                    <div class="bst-aether-card">
                        <div class="bst-aether-card-inner">
                            <button type="button" class="bst-modal-close" aria-label="Close">${CLOSE_ICON}</button>
                            <div class="bst-modal-scroll">
                                <div class="bst-modal-layout">
                                    <div class="bst-hero">
                                        ${backdrop ? `<div class="bst-hero-backdrop" style="background-image: url(&quot;${backdrop}&quot;)"></div>` : ''}
                                        ${posterUrl ? `<img class="bst-hero-poster" alt="" src="${escapeHtml(posterUrl)}" />` : ''}
                                        <div class="bst-hero-title-wrap${posterUrl ? ' has-poster' : ''}">
                                            ${logoUrl
                                                ? `<img class="bst-hero-logo" alt="${escapeHtml(title)}" src="${logoUrl}" data-fallback-title="${escapeHtml(title)}" />`
                                                : `<h1 class="bst-hero-title-fallback">${escapeHtml(title)}</h1>`}
                                            ${rating || year ? `
                                                <div class="bst-hero-meta">
                                                    ${rating ? `
                                                        <div class="bst-tmdb-rating">
                                                            ${TMDB_LOGO_SVG}
                                                            <span class="bst-meta-emphasis">${Number(rating).toFixed(1)}</span>
                                                            ${voteCount ? `<span class="bst-vote-muted">(${Number(voteCount).toLocaleString()})</span>` : ''}
                                                        </div>` : ''}
                                                    ${year ? `${rating ? '<span class="bst-dot">•</span>' : ''}<span class="bst-meta-emphasis">${escapeHtml(year)}</span>` : ''}
                                                </div>` : ''}
                                        </div>
                                    </div>
                                    <div class="bst-content">
                                        <div class="bst-actions-row">
                                            <div class="bst-actions-left">
                                                <button type="button" class="bst-btn-success" data-action="play" hidden>Play</button>
                                                <button type="button" class="bst-btn-request" data-action="request"${requestState.requested ? ' disabled' : ''}>${escapeHtml(requestState.label)}</button>
                                                ${getRequestModalAdvanced().showRequest4kButton !== false
                                                    ? `<button type="button" class="bst-btn-request-4k" data-action="request-4k"${request4kState.requested ? ' disabled' : ''}>${escapeHtml(request4kState.label)}</button>`
                                                    : ''}
                                                ${trailerKey
                                                    ? `<button type="button" class="bst-btn-trailer" data-action="trailer" data-trailer-key="${escapeHtml(trailerKey)}">Trailer</button>`
                                                    : ''}
                                                <button type="button" class="bst-btn-ghost" data-action="watchlist" data-watchlisted="${isOnWatchlist(data) ? 'true' : 'false'}">${isOnWatchlist(data) ? 'Remove from watchlist' : 'Watchlist'}</button>
                                                ${browseUrl && tmdbId
                                                    ? `<button type="button" class="bst-btn-ghost" data-action="open-seerr">Open in Seerr</button>`
                                                    : ''}
                                                ${data.mediaInfo || data.media_info || requestState.requested
                                                    ? `<button type="button" class="bst-btn-ghost" data-action="issue">Report issue</button>`
                                                    : ''}
                                                ${renderRequestLifecycleButtons(data, mediaType)}
                                            </div>
                                        </div>
                                        ${renderActiveRequestSummaries(data, mediaType)}
                                        ${renderDetailsStatusNotice(data)}
                                        <div class="bst-details-layout">
                                            <div class="bst-details-main">
                                                <p class="bst-overview">${escapeHtml(overview)}</p>
                                                <div class="bst-genres">${genres.map(function (g, i) {
                                                    return `<span class="bst-genre-pill" style="animation-delay:${i * 60}ms">${escapeHtml(g.name || g)}</span>`;
                                                }).join('')}</div>
                                            </div>
                                            <div class="bst-sidebar" data-quality-slot>
                                                <div class="bst-sidebar-lines">
                                                    ${runtime ? `<div><span class="bst-label">Runtime:</span> ${escapeHtml(runtime)}${endsAt ? ` <span class="bst-runtime-sep">•</span> Ends at ${escapeHtml(endsAt)}` : ''}</div>` : ''}
                                                    ${language ? `<div><span class="bst-label">Language:</span> ${escapeHtml(language)}</div>` : ''}
                                                    ${releaseLabel ? `<div><span class="bst-label">Release Date:</span> ${escapeHtml(releaseLabel)}</div>` : ''}
                                                    ${certification ? `<div><span class="bst-label">Rating:</span> ${escapeHtml(certification)}</div>` : ''}
                                                    ${tmdbId ? `<div><span class="bst-label">ID:</span> ${escapeHtml(String(tmdbId))}</div>` : ''}
                                                </div>
                                                ${tmdbId || imdbId ? `
                                                    <div class="bst-external-links">
                                                        ${tmdbId ? `
                                                            <a class="bst-external-link tmdb" href="https://www.themoviedb.org/${mediaType === 'tv' ? 'tv' : 'movie'}/${tmdbId}"
                                                                target="_blank" rel="noopener noreferrer" title="View on TMDB" style="animation-delay:60ms">
                                                                ${TMDB_LOGO_SVG}
                                                            </a>` : ''}
                                                        ${imdbId ? `
                                                            <a class="bst-external-link imdb" href="https://www.imdb.com/title/${imdbId}"
                                                                target="_blank" rel="noopener noreferrer" title="View on IMDb" style="animation-delay:120ms">
                                                                ${IMDB_ICON}
                                                            </a>` : ''}
                                                    </div>` : ''}
                                            </div>
                                        </div>
                                        ${cast.length ? renderCast(cast) : ''}
                                        ${renderRelatedRow(data.similar || data.Similar, 'Similar')}
                                        ${renderRelatedRow(data.recommendations || data.Recommendations, 'Recommendations')}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>`;
    }

    function bindDetailsModal(root, data, mediaId, mediaType) {
        const title = data.title || data.name || 'Details';
        const trailerKey = getTrailerKey(data);
        const tmdbId = data.id;

        root.querySelector('.bst-popout-backdrop').addEventListener('click', closeDetailsModal);
        root.querySelector('.bst-modal-close').addEventListener('click', closeDetailsModal);

        const logoImg = root.querySelector('.bst-hero-logo');
        if (logoImg) {
            logoImg.addEventListener('error', function () {
                const fallbackTitle = logoImg.getAttribute('data-fallback-title') || title;
                logoImg.insertAdjacentHTML('afterend', `<h1 class="bst-hero-title-fallback">${escapeHtml(fallbackTitle)}</h1>`);
                logoImg.remove();
            });
        }

        const requestBtn = root.querySelector('[data-action="request"]');
        if (requestBtn && !requestBtn.disabled) {
            requestBtn.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                openQualityModal(mediaId, mediaType, title, function () {
                    return reloadDetailsModal(mediaId, mediaType);
                });
            });
        }

        const request4kBtn = root.querySelector('[data-action="request-4k"]');
        if (request4kBtn && !request4kBtn.disabled) {
            request4kBtn.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                openQualityModal(mediaId, mediaType, title, function () {
                    return reloadDetailsModal(mediaId, mediaType);
                }, true);
            });
        }

        const playBtn = root.querySelector('[data-action="play"]');
        if (playBtn && mediaId) {
            lookupJellyfinPlayItem(mediaId, mediaType).then(function (item) {
                if (!item || !playBtn.isConnected) {
                    return;
                }
                const type = String(item.Type || item.type || '').toLowerCase();
                if (type && ((mediaType === 'tv' && type !== 'series') || (mediaType !== 'tv' && type !== 'movie'))) {
                    return;
                }
                playBtn.hidden = false;
                playBtn.addEventListener('click', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    closeDetailsModal();
                    navigateToJellyfinItem(item);
                });
            });
        }

        const openSeerrBtn = root.querySelector('[data-action="open-seerr"]');
        if (openSeerrBtn) {
            openSeerrBtn.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                openJellyseerrManage(tmdbId || mediaId, mediaType);
            });
        }

        const trailerBtn = root.querySelector('[data-action="trailer"]');
        if (trailerBtn && trailerKey) {
            trailerBtn.addEventListener('click', function () {
                window.open(`https://www.youtube.com/watch?v=${trailerKey}`, '_blank', 'noopener,noreferrer');
            });
        }

        const watchlistBtn = root.querySelector('[data-action="watchlist"]');
        if (watchlistBtn) {
            watchlistBtn.addEventListener('click', function () {
                const listed = watchlistBtn.getAttribute('data-watchlisted') === 'true';
                const req = listed
                    ? {
                        url: ApiClient.getUrl('JellySeerr/watchlist/' + mediaId, { mediaType: mediaType }),
                        type: 'DELETE'
                    }
                    : {
                        url: ApiClient.getUrl('JellySeerr/watchlist'),
                        type: 'POST',
                        data: JSON.stringify({ MediaType: mediaType, MediaId: mediaId }),
                        contentType: 'application/json'
                    };
                ApiClient.ajax(req).then(function () {
                    watchlistBtn.setAttribute('data-watchlisted', listed ? 'false' : 'true');
                    watchlistBtn.textContent = listed ? 'Watchlist' : 'Remove from watchlist';
                    notifyUser(listed ? 'Removed from Seerr watchlist' : 'Added to Seerr watchlist');
                }).catch(function () {
                    notifyUser(listed ? 'Could not remove from watchlist' : 'Could not add to watchlist');
                });
            });
        }

        root.querySelectorAll('[data-action="change-request"]').forEach(function (btn) {
            btn.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                const requestId = btn.getAttribute('data-request-id');
                if (!requestId) {
                    return;
                }
                const is4k = btn.getAttribute('data-is-4k') === '1';
                openQualityModal(mediaId, mediaType, title, function () {
                    return reloadDetailsModal(mediaId, mediaType);
                }, is4k, undefined, requestId);
            });
        });

        root.querySelectorAll('[data-action="cancel-request"], [data-action="approve-request"], [data-action="decline-request"], [data-action="retry-request"]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                const action = btn.getAttribute('data-action');
                const requestId = btn.getAttribute('data-request-id');
                if (!requestId) {
                    return;
                }
                if (action === 'cancel-request' && shouldConfirmCancel() && !window.confirm('Cancel this request? Monitoring in Radarr/Sonarr will also be stopped.')) {
                    return;
                }
                let url;
                let method = 'POST';
                if (action === 'cancel-request') {
                    url = ApiClient.getUrl('JellySeerr/request/' + requestId);
                    method = 'DELETE';
                } else if (action === 'approve-request') {
                    url = ApiClient.getUrl('JellySeerr/request/' + requestId + '/approve');
                } else if (action === 'decline-request') {
                    url = ApiClient.getUrl('JellySeerr/request/' + requestId + '/decline');
                } else if (action === 'retry-request') {
                    url = ApiClient.getUrl('JellySeerr/request/' + requestId + '/retry');
                }
                if (!url) {
                    return;
                }
                btn.disabled = true;
                const actionPromise = action === 'cancel-request'
                    ? unmonitorTitle(mediaType, mediaId, getRequestSeasons(data)).then(function () {
                        return { unmonitored: true, unmonitorMsg: '' };
                    }).catch(function (unmonitorErr) {
                        return readAjaxErrorMessage(unmonitorErr, '').then(function (msg) {
                            return { unmonitored: false, unmonitorMsg: msg || '' };
                        });
                    }).then(function (unmonitorResult) {
                        return ApiClient.ajax({ url: url, type: method }).then(function () {
                            pendingRequestContext = null;
                            if (unmonitorResult.unmonitored) {
                                return 'Request cancelled and monitoring stopped.';
                            }
                            return unmonitorResult.unmonitorMsg
                                ? 'Request cancelled. Could not unmonitor: ' + unmonitorResult.unmonitorMsg
                                : 'Request cancelled.';
                        });
                    })
                    : ApiClient.ajax({ url: url, type: method }).then(function () {
                        if (pendingRequestContext) {
                            if (action === 'approve-request' || action === 'decline-request') {
                                pendingRequestContext.isPending = false;
                            }
                            if (action === 'retry-request') {
                                pendingRequestContext.isFailed = false;
                            }
                        }
                        return 'Updated request';
                    });

                actionPromise.then(function (message) {
                    return reloadDetailsModal(mediaId, mediaType).then(function () {
                        notifyUser(message || 'Updated request');
                    });
                }).catch(function (err) {
                    log.error('request action failed', err);
                    btn.disabled = false;
                    const status = err && err.status;
                    if (action === 'cancel-request' && (status === 403 || status === 404)) {
                        notifyUser('Seerr could not cancel this request. If it is already approved, use Unmonitor instead.');
                        return;
                    }
                    return readAjaxErrorMessage(err, 'That request action failed.').then(notifyUser);
                });
            });
        });

        const unmonitorBtn = root.querySelector('[data-action="unmonitor"]');
        if (unmonitorBtn) {
            unmonitorBtn.addEventListener('click', function () {
                if (!window.confirm('Stop monitoring this title in Radarr/Sonarr? Existing files stay on disk.')) {
                    return;
                }
                unmonitorBtn.disabled = true;
                unmonitorTitle(mediaType, mediaId, getRequestSeasons(data)).then(function (result) {
                    notifyUser((result && (result.message || result.Message)) || 'Unmonitored');
                    if (typeof window.__jellySeerrRequestsEnsureMounted === 'function') {
                        window.__jellySeerrRequestsEnsureMounted({ tabShown: true });
                    }
                    return reloadDetailsModal(mediaId, mediaType);
                }).catch(function (err) {
                    log.error('unmonitor failed', err);
                    unmonitorBtn.disabled = false;
                    return readAjaxErrorMessage(err, 'Could not unmonitor this title in Radarr/Sonarr.').then(notifyUser);
                });
            });
        }

        const issueBtn = root.querySelector('[data-action="issue"]');
        if (issueBtn) {
            issueBtn.addEventListener('click', function () {
                const message = window.prompt('Describe the issue with this title');
                if (!message) {
                    return;
                }
                const mediaInfo = data.mediaInfo || data.media_info || {};
                const seerrMediaId = mediaInfo.id || mediaInfo.Id || mediaId;
                ApiClient.ajax({
                    url: ApiClient.getUrl('JellySeerr/issue'),
                    type: 'POST',
                    data: JSON.stringify({ IssueType: 1, Message: message, MediaId: seerrMediaId }),
                    contentType: 'application/json'
                }).then(function () {
                    notifyUser('Issue reported');
                }).catch(function () {
                    notifyUser('Could not report issue');
                });
            });
        }

        root.querySelectorAll('[data-related-id]').forEach(function (btn) {
            btn.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                const relatedId = parseInt(btn.getAttribute('data-related-id'), 10);
                const relatedType = btn.getAttribute('data-related-type') || 'movie';
                closeDetailsModal();
                openDetailsModal(relatedId, relatedType);
            });
        });

        const settings = window.jellySeerrPlugin && window.jellySeerrPlugin._displaySettings;
        const showQualityRecommendations = !settings || settings.QualityRecommendations !== false;
        if (showQualityRecommendations && tmdbId) {
            const qualitySlot = root.querySelector('[data-quality-slot]');
            const qualityLines = buildJustWatchQualityLines(tmdbId, mediaType);
            qualitySlot.insertBefore(qualityLines, qualitySlot.firstChild);
        }
    }

    function buildDetailsDom(data, mediaId, mediaType) {
        const root = mountFromHtml(renderDetails(data, mediaId, mediaType));
        bindDetailsModal(root, data, mediaId, mediaType);
        return root;
    }

    function renderDetailsLoading() {
        return `
            <div class="bst-popout-wrapper">
                <div class="bst-popout-backdrop"></div>
                <div class="bst-popout-center">
                    <div class="bst-aether-card">
                        <div class="bst-modal-loading">Loading…</div>
                    </div>
                </div>
            </div>`;
    }

    function openDetailsModal(mediaId, mediaType, context) {
        closeDetailsModal();
        pendingRequestContext = context && context.requestId ? context : null;
        log.info('opening details modal for ' + mediaType + '/' + mediaId);

        document.body.insertAdjacentHTML('beforeend', renderDetailsLoading());
        activeDetailsRoot = document.body.lastElementChild;
        document.body.style.overflow = 'hidden';

        loadModalDetails(mediaId, mediaType).then(function (data) {
            const dom = buildDetailsDom(data, mediaId, mediaType);
            activeDetailsRoot.replaceWith(dom);
            activeDetailsRoot = dom;
            log.info('details modal ready for ' + mediaType + '/' + mediaId);

            escapeHandler = function (e) {
                if (e.key === 'Escape') {
                    if (activeQualityRoot) {
                        closeQualityModal();
                    } else if (activeSeasonRoot) {
                        closeSeasonModal();
                    } else {
                        closeDetailsModal();
                    }
                }
            };
            document.addEventListener('keydown', escapeHandler);
        }).catch(function (err) {
            log.error('details modal failed for ' + mediaType + '/' + mediaId, err);
            closeDetailsModal();
            Dashboard.alert('Failed to load details');
        });
    }

    window.jellySeerrModal = {
        open: openDetailsModal,
        close: closeDetailsModal,
        openQualityPicker: openQualityModal
    };
})();
