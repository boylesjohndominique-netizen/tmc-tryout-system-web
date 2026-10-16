/* Student ID photo capture: camera or gallery, live preview, and quality checks.
   Flags blurry photos and black-and-white/filtered photos before the form is sent. */
(function () {
    'use strict';

    var MAX_BYTES = 5 * 1024 * 1024;
    var MIN_SHORT_SIDE = 320;
    var MIN_LONG_SIDE = 480;
    var BLUR_VARIANCE_LIMIT = 20;   // Laplacian variance on a ~320px copy
    var GRAYSCALE_CHANNEL_LIMIT = 2; // mean |R-G|,|G-B|,|R-B|; a real photo always exceeds this

    var accepted = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];

    function setupWidget(widget) {
        var input = widget.querySelector('.id-capture-input');
        var preview = widget.querySelector('[data-capture-preview]');
        var previewImg = preview ? preview.querySelector('img') : null;
        if (!input) return;

        widget.dataset.state = 'empty';

        widget.querySelectorAll('[data-capture]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                // "capture=environment" makes phones open the rear camera; without it,
                // the browser offers the gallery / file picker instead.
                if (btn.getAttribute('data-capture') === 'camera') {
                    input.setAttribute('capture', 'environment');
                } else {
                    input.removeAttribute('capture');
                }
                input.click();
            });
        });

        input.addEventListener('change', function () {
            if (!input.files || input.files.length === 0) {
                setState(widget, 'empty', '');
                return;
            }
            var file = input.files[0];

            if (accepted.indexOf(file.type) === -1) {
                setState(widget, 'invalid', 'Use a JPG, PNG or WEBP photo.');
                return;
            }
            if (file.size > MAX_BYTES) {
                setState(widget, 'invalid', 'That photo is larger than 5 MB. Retake it at a lower resolution.');
                return;
            }

            var url = URL.createObjectURL(file);
            if (previewImg) previewImg.src = url;
            if (preview) preview.hidden = false;
            setState(widget, 'checking', 'Checking the photo quality...');

            checkPhoto(file, url).then(function (result) {
                if (!result.ok) {
                    setState(widget, 'invalid', result.message);
                    return;
                }
                setState(widget, 'valid', result.warning || 'Looks good — sharp and unfiltered.');
            }).catch(function () {
                // If the browser cannot read the pixels, let the server's checks decide.
                setState(widget, 'valid', 'Photo attached.');
            });
        });
    }

    function setState(widget, state, message) {
        widget.dataset.state = state;
        var note = widget.querySelector('[data-capture-note]');
        if (note) {
            note.textContent = message || '';
            note.className = 'id-capture-note id-capture-note-' + state;
        }
        var preview = widget.querySelector('[data-capture-preview]');
        if (preview && state !== 'empty') preview.hidden = false;
        refreshSubmit();
    }

    function refreshSubmit() {
        var form = document.querySelector('[data-id-form]');
        if (!form) return;
        var states = Array.prototype.map.call(form.querySelectorAll('[data-id-capture]'), function (w) {
            return w.dataset.state || 'empty';
        });
        // Only a photo that failed a check (or is still being checked) blocks sending.
        // Missing photos are left to the server-side "required" validation.
        var blocked = states.some(function (s) { return s === 'invalid' || s === 'checking'; });
        var submit = form.querySelector('[data-id-submit]');
        if (submit) submit.disabled = blocked;
    }

    function checkPhoto(file, url) {
        return new Promise(function (resolve, reject) {
            var img = new Image();
            img.onload = function () {
                try {
                    resolve(measure(img));
                } catch (e) {
                    reject(e);
                }
            };
            img.onerror = function () { reject(new Error('decode')); };
            img.src = url;
        });
    }

    function measure(img) {
        var width = img.naturalWidth || img.width;
        var height = img.naturalHeight || img.height;
        var shortSide = Math.min(width, height);
        var longSide = Math.max(width, height);
        if (shortSide < MIN_SHORT_SIDE || longSide < MIN_LONG_SIDE) {
            return {
                ok: false,
                message: 'That photo is only ' + width + 'x' + height + 'px, which would be too blurry to read. ' +
                         'Take a closer, sharper photo of at least ' + MIN_SHORT_SIDE + 'x' + MIN_LONG_SIDE + 'px.'
            };
        }

        // Work from a small copy so the maths stays quick on phones.
        var scale = Math.min(1, 320 / longSide);
        var w = Math.max(2, Math.round(width * scale));
        var h = Math.max(2, Math.round(height * scale));

        var canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        var ctx = canvas.getContext('2d');
        if (!ctx) return { ok: true, warning: null };

        ctx.drawImage(img, 0, 0, w, h);
        var data = ctx.getImageData(0, 0, w, h).data;

        var gray = new Float32Array(w * h);
        var channelDiff = 0;
        for (var i = 0, p = 0; i < gray.length; i++, p += 4) {
            var r = data[p], g = data[p + 1], b = data[p + 2];
            gray[i] = 0.299 * r + 0.587 * g + 0.114 * b;
            channelDiff += (Math.abs(r - g) + Math.abs(g - b) + Math.abs(r - b)) / 3;
        }
        channelDiff /= gray.length;

        // Variance of the Laplacian: low values mean there is very little edge detail.
        var sum = 0, sumSq = 0, count = 0;
        for (var y = 1; y < h - 1; y++) {
            for (var x = 1; x < w - 1; x++) {
                var idx = y * w + x;
                var lap = 4 * gray[idx] - gray[idx - 1] - gray[idx + 1] - gray[idx - w] - gray[idx + w];
                sum += lap;
                sumSq += lap * lap;
                count++;
            }
        }
        var mean = count ? sum / count : 0;
        var variance = count ? (sumSq / count) - mean * mean : 0;

        if (variance < BLUR_VARIANCE_LIMIT) {
            return {
                ok: false,
                message: 'That photo looks blurry. Hold the phone steady, tap the ID on screen to focus, and retake it.'
            };
        }

        if (channelDiff < GRAYSCALE_CHANNEL_LIMIT) {
            return {
                ok: false,
                message: 'That photo looks black-and-white or filtered. Turn off any filter and take a normal colour photo.'
            };
        }

        return {
            ok: true,
            warning: variance < BLUR_VARIANCE_LIMIT * 3
                ? 'Sharp enough, but hold extra steady for the clearest result.'
                : null
        };
    }

    function init() {
        document.querySelectorAll('[data-id-capture]').forEach(setupWidget);
        refreshSubmit();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
