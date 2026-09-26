(function () {
    'use strict';

    // ------------------------------------------------------------------
    // Delegated handlers (document level) — these survive DOM swaps.
    // ------------------------------------------------------------------
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form && form.getAttribute && form.getAttribute('data-confirm')) {
            if (!window.confirm(form.getAttribute('data-confirm'))) e.preventDefault();
        }
    });

    // ------------------------------------------------------------------
    // Per-page widgets. Runs once on load and again after every real-time
    // content swap (all matched elements are fresh at that point).
    // ------------------------------------------------------------------
    function initPage() {
        // Show / hide password
        document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var input = document.querySelector(btn.getAttribute('data-toggle-password'));
                if (!input) return;
                var show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                var icon = btn.querySelector('i');
                if (icon) icon.className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
            });
        });

        // Disable submit buttons after a valid submit to prevent double registrations
        document.querySelectorAll('form[data-once]').forEach(function (form) {
            form.addEventListener('submit', function () {
                if (window.jQuery && window.jQuery(form).valid && !window.jQuery(form).valid()) return;
                var btn = form.querySelector('button[type=submit]');
                if (btn) {
                    setTimeout(function () { btn.disabled = true; }, 0);
                }
            });
        });

        // Sport -> schedule dependent dropdown on the registration form
        var sportSelect = document.querySelector('[data-sport-select]');
        var scheduleSelect = document.querySelector('[data-schedule-select]');
        if (sportSelect && scheduleSelect) {
            var refresh = function () {
                var sportId = sportSelect.value;
                var hasVisibleSelected = false;
                Array.prototype.forEach.call(scheduleSelect.options, function (opt) {
                    if (!opt.value) return;
                    var match = opt.getAttribute('data-sport') === sportId;
                    opt.hidden = !match;
                    opt.disabled = !match;
                    if (match && opt.selected) hasVisibleSelected = true;
                });
                if (!hasVisibleSelected) scheduleSelect.value = '';
                var hint = document.getElementById('scheduleHint');
                if (hint) {
                    var any = Array.prototype.some.call(scheduleSelect.options, function (o) { return o.value && !o.disabled; });
                    hint.textContent = !sportId ? 'Choose a sport first to see its tryout dates.'
                        : (any ? 'Optional. You can also pick a date later.' : 'No upcoming schedule for this sport yet. You can still register.');
                }
            };
            sportSelect.addEventListener('change', refresh);
            refresh();
        }

        // Live overall score on the evaluation form
        var evalForm = document.querySelector('[data-eval-form]');
        if (evalForm) {
            var required = parseFloat(evalForm.getAttribute('data-required')) || 0;
            var inputs = evalForm.querySelectorAll('[data-criterion]');
            var totalEl = document.getElementById('liveTotal');
            var verdictEl = document.getElementById('liveVerdict');
            var update = function () {
                var sum = 0;
                inputs.forEach(function (i) { sum += parseFloat(i.value) || 0; });
                var avg = sum / 7;
                if (totalEl) totalEl.textContent = avg.toFixed(1);
                if (verdictEl) {
                    var ok = avg >= required;
                    verdictEl.className = 'badge-soft ' + (ok ? 'badge-soft-success' : 'badge-soft-warning');
                    verdictEl.innerHTML = ok ? '<i class="bi bi-check-circle-fill"></i>Meets the qualifying score'
                                             : '<i class="bi bi-hourglass-split"></i>Below the qualifying score';
                }
            };
            evalForm.querySelectorAll('[data-criterion-row]').forEach(function (row) {
                var range = row.querySelector('input[type=range]');
                var num = row.querySelector('input[type=number]');
                range.addEventListener('input', function () { num.value = range.value; update(); });
                num.addEventListener('input', function () {
                    var v = Math.max(0, Math.min(100, parseInt(num.value, 10) || 0));
                    range.value = v; update();
                });
            });
            update();
        }

        // Select-all + compare button on the screening table
        var compareForm = document.getElementById('compareForm');
        if (compareForm) {
            var boxes = compareForm.querySelectorAll('input[name=ids]');
            var btn = document.getElementById('compareBtn');
            var label = document.getElementById('compareCount');
            var sync = function () {
                var checked = compareForm.querySelectorAll('input[name=ids]:checked').length;
                if (label) label.textContent = checked;
                if (btn) btn.disabled = checked < 2 || checked > 4;
                boxes.forEach(function (b) { if (!b.checked) b.disabled = checked >= 4; });
            };
            boxes.forEach(function (b) { b.addEventListener('change', sync); });
            sync();
        }

        // Selection modal: fill in the applicant details
        var decideModal = document.getElementById('decideModal');
        if (decideModal) {
            decideModal.addEventListener('show.bs.modal', function (event) {
                var trigger = event.relatedTarget;
                if (!trigger) return;
                decideModal.querySelector('[name=id]').value = trigger.getAttribute('data-id');
                decideModal.querySelector('#decideName').textContent = trigger.getAttribute('data-name');
                decideModal.querySelector('#decideSub').textContent = trigger.getAttribute('data-sub');
                decideModal.querySelector('[name=remarks]').value = trigger.getAttribute('data-remarks') || '';
                var current = trigger.getAttribute('data-status');
                decideModal.querySelectorAll('input[name=status]').forEach(function (r) { r.checked = (r.value === current); });
            });
        }

        // Bottom fade hint depends on content height; re-measure after swaps.
        updateFade();
    }

    window.TmcPageInit = initPage;
    document.addEventListener('tmc:content', initPage);

    // ------------------------------------------------------------------
    // Bottom fade hint: visible only when the page can still scroll down.
    // The scroller element persists across swaps, so bind once.
    // ------------------------------------------------------------------
    var fadeWrap = document.querySelector('.content-fade');
    var scroller = document.querySelector('.content');
    function updateFade() {
        if (!fadeWrap || !scroller) return;
        var distance = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight;
        fadeWrap.classList.toggle('rt-can-scroll', distance > 4);
    }
    if (fadeWrap && scroller) {
        scroller.addEventListener('scroll', updateFade, { passive: true });
        window.addEventListener('resize', updateFade);
    }

    // ------------------------------------------------------------------
    // Sidebar labels: slide in on touch devices on first tap (CSS handles hover/focus)
    // ------------------------------------------------------------------
    if (window.matchMedia && window.matchMedia('(hover: none)').matches) {
        document.querySelectorAll('.side-link, .side-user-btn').forEach(function (el) {
            el.addEventListener('touchstart', function handler() {
                var label = el.querySelector('.side-label');
                if (label) {
                    label.style.opacity = '1';
                    label.style.transform = 'translateY(-50%) translateX(0)';
                    setTimeout(function () {
                        label.style.opacity = '';
                        label.style.transform = '';
                    }, 1200);
                }
                el.removeEventListener('touchstart', handler);
            }, { passive: true });
        });
    }

    initPage();
})();
