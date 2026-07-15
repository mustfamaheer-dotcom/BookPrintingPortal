(function () {
    'use strict';

    document.addEventListener('contextmenu', function (e) {
        e.preventDefault();
    });

    document.addEventListener('keydown', function (e) {
        var ctrl = e.ctrlKey || e.metaKey;
        var key = (e.key || '').toLowerCase();
        if (ctrl && (key === 's' || key === 'p')) { e.preventDefault(); e.stopPropagation(); return; }
    }, true);

    document.addEventListener('dragstart', function (e) {
        if (e.target && typeof e.target.closest === 'function' && e.target.closest('.pdf-container')) e.preventDefault();
    });
    document.addEventListener('selectstart', function (e) {
        if (e.target && typeof e.target.closest === 'function' && e.target.closest('.pdf-container')) e.preventDefault();
    });

    var pdfDoc = null;
    var container = null;
    var loadingEl = null;
    var renderedPages = {};
    var pendingRender = {};
    var observer = null;

    window.initPdfViewer = function (bookId) {
        container = document.getElementById('pdfViewer');
        loadingEl = document.getElementById('pdfLoading');
        if (!container) return;

        wireCopiesControls();

        loadPdfJs(function () {
            loadSecurePdf(bookId);
        });
    };

    function wireCopiesControls() {
        var input = document.getElementById('copiesInput');
        var inc = document.getElementById('copiesInc');
        var dec = document.getElementById('copiesDec');
        if (!input || !inc || !dec) return;

        inc.addEventListener('click', function () {
            var v = parseInt(input.value, 10) || 1;
            if (v < 100) input.value = v + 1;
        });
        dec.addEventListener('click', function () {
            var v = parseInt(input.value, 10) || 1;
            if (v > 1) input.value = v - 1;
        });
        input.addEventListener('change', function () {
            var v = parseInt(input.value, 10) || 1;
            if (v < 1) v = 1;
            if (v > 100) v = 100;
            input.value = v;
        });
    }

    function loadPdfJs(callback) {
        if (window.pdfjsLib) {
            callback();
            return;
        }
        var script = document.createElement('script');
        script.src = '/js/pdf.min.js';
        script.onload = function () {
            pdfjsLib.GlobalWorkerOptions.workerSrc = '/js/pdf.worker.min.js';
            callback();
        };
        script.onerror = function () {
            if (loadingEl) loadingEl.innerHTML = '<span style="color:red">Failed to load PDF viewer. Please refresh the page.</span>';
        };
        document.head.appendChild(script);
    }

    async function loadSecurePdf(bookId) {
        loadingEl.style.display = 'flex';

        try {
            var response = await fetch('/api/pdf/view-secure/' + bookId, {
                method: 'GET',
                credentials: 'include'
            });

            if (response.status === 401 || response.status === 403) {
                throw new Error('Access Denied: You are not authorized to view this book.');
            }

            if (!response.ok) {
                throw new Error('HTTP Error: ' + response.status);
            }

            var data = await response.json();

            var binaryString = atob(data.pdfData);
            var len = binaryString.length;
            var bytes = new Uint8Array(len);
            for (var i = 0; i < len; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }

            var loadingTask = pdfjsLib.getDocument({ data: bytes });

            loadingTask.onProgress = function (progress) {
                var pct = Math.min(Math.round((progress.loaded / progress.total) * 100), 100);
                var text = loadingEl.querySelector('.pdf-loading-text');
                var bar = loadingEl.querySelector('.pdf-loading-bar span');
                if (text) text.textContent = 'Loading... ' + pct + '%';
                if (bar) bar.style.width = pct + '%';
            };

            pdfDoc = await loadingTask.promise;

            loadingEl.style.display = 'none';
            container.innerHTML = '';
            container.style.textAlign = 'center';

            for (var i = 1; i <= pdfDoc.numPages; i++) {
                var placeholder = document.createElement('div');
                placeholder.className = 'pdf-page-placeholder';
                placeholder.dataset.pageNum = i;
                placeholder.style.height = '10px';
                container.appendChild(placeholder);
            }

            observer = new IntersectionObserver(onPageVisible, {
                root: container.parentElement || container,
                rootMargin: '200px 0px',
                threshold: 0.01
            });

            document.querySelectorAll('.pdf-page-placeholder').forEach(function (el) {
                observer.observe(el);
            });

        } catch (error) {
            console.error(error);
            if (loadingEl) loadingEl.innerText = error.message;
        }
    }

    function onPageVisible(entries) {
        entries.forEach(function (entry) {
            if (!entry.isIntersecting) return;
            var placeholder = entry.target;
            var pageNum = parseInt(placeholder.dataset.pageNum, 10);

            observer.unobserve(placeholder);

            if (renderedPages[pageNum]) return;
            if (pendingRender[pageNum]) return;
            pendingRender[pageNum] = true;

            renderPageAsync(pageNum, placeholder);
        });
    }

    function renderPageAsync(pageNum, placeholder) {
        pdfDoc.getPage(pageNum).then(function (page) {
            var viewport = page.getViewport({ scale: 1.5 });

            var canvas = document.createElement('canvas');
            canvas.className = 'pdf-page-canvas';
            canvas.height = viewport.height;
            canvas.width = viewport.width;
            canvas.dataset.pageNum = pageNum;

            placeholder.parentNode.replaceChild(canvas, placeholder);

            var ctx = canvas.getContext('2d');
            return page.render({ canvasContext: ctx, viewport: viewport }).promise;
        }).then(function () {
            renderedPages[pageNum] = true;
            delete pendingRender[pageNum];
        }).catch(function (err) {
            delete pendingRender[pageNum];
        });
    }

    function showPrintModal(success, message, reason) {
        var modalEl = document.getElementById('printResultModal');
        if (!modalEl) return;

        var progress = document.getElementById('printModalStateProgress');
        var successState = document.getElementById('printModalStateSuccess');
        var errorState = document.getElementById('printModalStateError');

        function show(state) {
            [progress, successState, errorState].forEach(function (el) {
                if (el) el.classList.add('d-none');
            });
            if (state) state.classList.remove('d-none');
        }

        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);

        if (success) {
            show(successState);
        } else {
            var reasonEl = document.getElementById('printErrorReason');
            if (reasonEl) reasonEl.textContent = message || reason || 'Unknown error';
            show(errorState);
        }

        modal.show();
    }

    window.handlePrint = async function (event, bookId) {
        var modalEl = document.getElementById('printResultModal');
        if (modalEl) {
            bootstrap.Modal.getOrCreateInstance(modalEl).show();
            {
                var p = document.getElementById('printModalStateProgress');
                var s = document.getElementById('printModalStateSuccess');
                var e = document.getElementById('printModalStateError');
                if (p) p.classList.remove('d-none');
                if (s) s.classList.add('d-none');
                if (e) e.classList.add('d-none');
            }
        }

        var printBtn = document.getElementById('printBtn');
        if (printBtn) printBtn.disabled = true;

        try {
            var copiesInput = document.getElementById('copiesInput');
            var copies = copiesInput ? parseInt(copiesInput.value, 10) || 1 : 1;

            var serverResponse = await fetch('/api/pdf/process-print', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ bookId: bookId, copies: copies })
            });

            var serverData = await serverResponse.json().catch(function () {
                return { success: false, error: 'HTTP ' + serverResponse.status };
            });

            if (!serverResponse.ok || !serverData.success) {
                throw new Error(serverData.error || serverData.message || 'Server returned ' + serverResponse.status);
            }

            var jobId = serverData.jobId;

            var agentClaimed = false;
            for (var i = 0; i < 10; i++) {
                await new Promise(function (r) { setTimeout(r, 2000); });
                try {
                    var check = await fetch('/api/pdf/print-agent/pending');
                    var checkData = await check.json();
                    if (checkData.jobs && !checkData.jobs.includes(jobId)) {
                        agentClaimed = true;
                        break;
                    }
                } catch (_) { }
            }

            if (agentClaimed) {
                showPrintModal(true, 'The printing will be done shortly.');
            } else {
                throw new Error('Local printer agent not detected. Make sure the agent is running and try again.');
            }

        } catch (error) {
            showPrintModal(false, error.message, error.message);
        } finally {
            if (printBtn) printBtn.disabled = false;
        }
    };

    var retryBtn = document.getElementById('printRetryBtn');
    if (retryBtn) {
        retryBtn.addEventListener('click', function () {
            var modalEl = document.getElementById('printResultModal');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();
        });
    }
})();
