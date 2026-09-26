(function () {
    'use strict';

    if (typeof window.signalR === 'undefined') return;

    // ------------------------------------------------------------------
    // Page refresh scope: views opt in with <body data-rt-scope="...">;
    // falls back to a controller-based guess so every page is covered.
    // ------------------------------------------------------------------
    var bodyScope = document.body.getAttribute('data-rt-scope');
    var path = window.location.pathname.toLowerCase();
    var segment = (path.split('/').filter(Boolean)[0] || 'home');
    var SCOPE_BY_CONTROLLER = {
        home: 'dashboard', applicants: 'applicants', evaluations: 'evaluations',
        screening: 'screening', selection: 'selection', schedules: 'schedules',
        sports: 'sports', explore: 'explore', registration: 'registrations',
        users: 'users', reports: 'reports'
    };
    var myScope = bodyScope || SCOPE_BY_CONTROLLER[segment] || 'dashboard';

    // ------------------------------------------------------------------
    // Skeleton shimmer: kept for the full-reload fallback path. Flagged in
    // sessionStorage so the incoming page shows it before first paint.
    // ------------------------------------------------------------------
    var SKEL_KEY = 'tmc-rt-skeleton';
    var skeleton = document.getElementById('rtSkeleton');
    var skeletonTimer = null;

    function hideSkeleton() {
        if (!skeleton || skeleton.hidden) return;
        skeleton.classList.add('rt-fade');
        setTimeout(function () { skeleton.hidden = true; }, 320);
    }

    function showSkeleton() {
        if (!skeleton) return;
        skeleton.classList.remove('rt-fade');
        skeleton.hidden = false;
        clearTimeout(skeletonTimer);
        skeletonTimer = setTimeout(hideSkeleton, 6000);   // safety net
    }

    if (skeleton && sessionStorage.getItem(SKEL_KEY) === '1') {
        sessionStorage.removeItem(SKEL_KEY);
        showSkeleton();
        var settle = function () { setTimeout(hideSkeleton, 300); };
        if (document.readyState === 'complete') settle();
        else window.addEventListener('load', settle);
    }

    // ------------------------------------------------------------------
    // Connection
    // ------------------------------------------------------------------
    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/tryout')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    var pill = document.getElementById('rtPill');
    function setPill(state) {
        if (!pill) return;
        pill.classList.remove('rt-on', 'rt-off');
        pill.classList.add(state);
        pill.title = state === 'rt-on' ? 'Live updates connected' : 'Reconnecting\u2026';
    }

    // ------------------------------------------------------------------
    // Partial refresh: swap only the list/table card via fetch, keeping
    // scroll position, filters in the URL, and page state. Falls back to a
    // skeleton full reload when the structure does not match.
    // ------------------------------------------------------------------
    var swapTimer = null;
    var isFetching = false;
    var pendingRefresh = false;
    var lastSwap = 0;
    var MIN_SWAP_MS = 2500;      // calm-down window between swaps

    function canSwap() {
        if (document.body.hasAttribute('data-rt-keep')) return false;
        if (document.querySelector('.modal.show')) return false;
        var ae = document.activeElement;
        var typing = ae && (ae.tagName === 'INPUT' || ae.tagName === 'TEXTAREA' || ae.tagName === 'SELECT');
        if (typing) return false;                     // user is filling a form
        return true;
    }

    function isListPage() {
        return !!document.querySelector('.content > .card-x .table-responsive-x, .content > .card-x .schedule-item');
    }

    function currentListUrl() {
        try {
            var u = new URL(window.location.href);
            u.searchParams.delete('page');
            return u.toString();
        } catch (e) { return window.location.href; }
    }

    function firstListCard(doc) {
        var el = doc.querySelector('.content > .card-x .table-responsive-x, .content > .card-x .schedule-item');
        if (!el) return null;
        var node = el;
        while (node && !(node.classList && node.classList.contains('card-x'))) node = node.parentNode;
        return node || null;
    }

    function rerunScripts(container) {
        container.querySelectorAll('script').forEach(function (old) {
            var s = document.createElement('script');
            if (old.src) s.src = old.src;
            s.text = old.textContent;
            old.parentNode.replaceChild(s, old);
        });
    }

    function applyFetchedDoc(html) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        var localCard = firstListCard(document);
        var remoteCard = firstListCard(doc);
        if (!localCard || !remoteCard) return false;

        // Never yank the card out from under an active interaction.
        if (localCard.contains(document.activeElement)) return false;

        // Preserve where the user was looking inside the table region.
        var inner = localCard.querySelector('.table-responsive-x');
        var innerTop = inner ? inner.scrollTop : 0;
        var windowGap = inner ? (inner.scrollHeight - inner.scrollTop - inner.clientHeight) : 0;
        var wasAtBottom = windowGap <= 4;             // pinned to newest rows

        localCard.parentNode.replaceChild(remoteCard, localCard);
        rerunScripts(remoteCard);

        var newInner = remoteCard.querySelector('.table-responsive-x');
        if (newInner) newInner.scrollTop = wasAtBottom ? newInner.scrollHeight : innerTop;

        // Sync subtitle counts and the tab title from the fresh markup.
        var localSub = document.querySelector('.content > .page-head p');
        var remoteSub = doc.querySelector('.content > .page-head p');
        if (localSub && remoteSub && localSub.textContent !== remoteSub.textContent) {
            localSub.textContent = remoteSub.textContent;
        }
        var t = doc.querySelector('title');
        if (t) document.title = t.textContent;

        // Re-init per-page widgets on the fresh nodes (also re-measures fade).
        if (window.TmcPageInit) window.TmcPageInit();
        return true;
    }

    // ------------------------------------------------------------------
    // Scroll preservation for the full-reload fallback: the in-place fetch
    // swap keeps scroll naturally, but when we must navigate (forms, fetch
    // errors) we stash offsets and restore them on the incoming page.
    // ------------------------------------------------------------------
    var SCROLL_KEY = 'tmc-rt-scroll';
    var scroller = document.querySelector('.content');

    function saveScrollState() {
        var state = {
            url: window.location.pathname + window.location.search,
            y: window.pageYOffset | 0,
            content: scroller ? scroller.scrollTop : 0,
            table: 0
        };
        var inner = document.querySelector('.content .table-responsive-x');
        if (inner) state.table = inner.scrollTop;
        try { sessionStorage.setItem(SCROLL_KEY, JSON.stringify(state)); } catch (e) { /* private mode */ }
    }

    function restoreScrollState() {
        var raw = null;
        try { raw = sessionStorage.getItem(SCROLL_KEY); } catch (e) { /* ignore */ }
        if (!raw) return;
        try { sessionStorage.removeItem(SCROLL_KEY); } catch (e) { /* ignore */ }
        var st;
        try { st = JSON.parse(raw); } catch (e) { return; }
        if (!st || st.url !== window.location.pathname + window.location.search) return; // different page now
        var apply = function () {
            if (scroller && st.content) scroller.scrollTop = st.content;
            if (st.table) {
                var t = document.querySelector('.content .table-responsive-x');
                if (t) t.scrollTop = st.table;
            }
            if (st.y) window.scrollTo(0, st.y);
        };
        if (document.readyState === 'complete') requestAnimationFrame(apply);
        else window.addEventListener('load', function () { requestAnimationFrame(apply); });
    }

    function doFullReload() {
        saveScrollState();
        if (skeleton) {
            try { sessionStorage.setItem(SKEL_KEY, '1'); } catch (e) { /* private mode */ }
            showSkeleton();
        }
        window.location.assign(window.location.href);
    }

    function fetchSwap() {
        if (isFetching) { pendingRefresh = true; return; }
        isFetching = true;
        fetch(currentListUrl(), { credentials: 'same-origin' })
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.text();
            })
            .then(function (html) {
                lastSwap = Date.now();
                if (!applyFetchedDoc(html)) doFullReload();   // structure mismatch
            })
            .catch(function () { doFullReload(); })
            .then(function () {
                isFetching = false;
                if (pendingRefresh) { pendingRefresh = false; scheduleRefresh(); }
            });
    }

    function scheduleRefresh() {
        if (!canSwap()) return;
        if (swapTimer) clearTimeout(swapTimer);
        swapTimer = setTimeout(function () {
            if (!canSwap()) return;
            if (!isListPage()) { doFullReload(); return; }    // forms/details: full reload
            if (Date.now() - lastSwap < MIN_SWAP_MS) {        // too soon: try again shortly
                setTimeout(scheduleRefresh, 900);
                return;
            }
            fetchSwap();
        }, 1000);
    }

    // ------------------------------------------------------------------
    // Event wiring
    // ------------------------------------------------------------------
    connection.on('dataChanged', function () {
        scheduleRefresh();
    });

    connection.on('notifyUser', function (msg) {
        if (!msg) return;
        pushBell(msg.title, msg.message, msg.icon);
        var scopes = (msg && msg.scopes) || [];
        if (scopes.indexOf('all') !== -1 || scopes.indexOf(myScope) !== -1) scheduleRefresh();
    });

    connection.onreconnecting(function () { setPill('rt-off'); });
    connection.onreconnected(function () { setPill('rt-on'); scheduleRefresh(); });
    connection.onclose(function () {
        setTimeout(function () { connection.start().catch(function () {}); }, 15000);
        setPill('rt-off');
    });

    connection.start()
        .then(function () { setPill('rt-on'); })
        .catch(function () { setPill('rt-off'); });

    // ------------------------------------------------------------------
    // Notification bell + toasts
    // ------------------------------------------------------------------
    var bellList = document.getElementById('rtBellList');
    var bellDot = document.getElementById('rtBellDot');
    var toasts = document.getElementById('rtToasts');

    function pushBell(title, message, icon) {
        if (bellDot) bellDot.hidden = false;
        if (!bellList) return;
        var empty = bellList.querySelector('.rt-bell-empty');
        if (empty) empty.remove();
        var item = document.createElement('div');
        item.className = 'rt-bell-item';
        item.innerHTML =
            '<i class="bi bi-' + (icon || 'bell-fill') + '"></i>' +
            '<div><strong></strong><span></span><small>' + new Date().toLocaleTimeString() + '</small></div>';
        item.querySelector('strong').textContent = title || 'Update';
        item.querySelector('span').textContent = message || '';
        bellList.prepend(item);
        while (bellList.children.length > 15) bellList.removeChild(bellList.lastChild);
    }

    function showToast(title, message, icon) {
        if (!toasts) return;
        var t = document.createElement('div');
        t.className = 'rt-toast';
        t.innerHTML = '<i class="bi bi-"></i><div><strong></strong><span></span></div>';
        t.querySelector('i').className = 'bi bi-' + (icon || 'bell-fill');
        t.querySelector('strong').textContent = title || '';
        t.querySelector('span').textContent = message || '';
        toasts.appendChild(t);
        requestAnimationFrame(function () { t.classList.add('show'); });
        setTimeout(function () {
            t.classList.remove('show');
            setTimeout(function () { t.remove(); }, 400);
        }, 6000);
    }

    // Public hook: other scripts can push toasts/bells.
    window.TmcRealtime = {
        toast: showToast,
        bell: pushBell,
        scope: myScope,
        refresh: scheduleRefresh
    };

    // Last: put the user back where they were after a fallback full reload.
    restoreScrollState();
})();
