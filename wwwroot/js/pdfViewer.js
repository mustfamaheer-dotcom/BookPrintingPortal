// Secure PDF Viewer - PDF.js Canvas Rendering + Security

(function () {
    'use strict';

    // ─── Security: Block right-click everywhere ─────────────────────────
    document.addEventListener('contextmenu', function (e) {
        e.preventDefault();
    });

    // ─── Security: Block keyboard shortcuts ─────────────────────────────
    document.addEventListener('keydown', function (e) {
        var ctrl = e.ctrlKey || e.metaKey;
        var key = e.key.toLowerCase();

        // Block Save (Ctrl+S)
        if (ctrl && key === 's') { e.preventDefault(); e.stopPropagation(); return; }
        // Block Print (Ctrl+P)
        if (ctrl && key === 'p') { e.preventDefault(); e.stopPropagation(); return; }
        // Block View Source (Ctrl+U)
        if (ctrl && key === 'u') { e.preventDefault(); return; }
        // Block DevTools (F12)
        if (e.key === 'F12') { e.preventDefault(); return; }
        // Block Ctrl+Shift+I
        if (ctrl && e.shiftKey && key === 'i') { e.preventDefault(); return; }
        // Block Ctrl+Shift+J
        if (ctrl && e.shiftKey && key === 'j') { e.preventDefault(); return; }
        // Block Ctrl+C in viewer
        if (ctrl && key === 'c' && e.target.closest('#pdfViewer')) { e.preventDefault(); return; }
        // Block Ctrl+Shift+C
        if (ctrl && e.shiftKey && key === 'c') { e.preventDefault(); return; }
    }, true);

    // ─── Security: DevTools detection via window size ───────────────────
    var devToolsOpen = false;
    var threshold = 160;

    setInterval(function () {
        var w = window.outerWidth - window.innerWidth > threshold;
        var h = window.outerHeight - window.innerHeight > threshold;
        if (w || h) {
            if (!devToolsOpen) {
                devToolsOpen = true;
                document.body.innerHTML = '<div style="padding:40px;text-align:center;"><h1>Access Denied</h1><p>Developer tools detected. Please close them and reload the page.</p></div>';
            }
        } else {
            devToolsOpen = false;
        }
    }, 1000);

    // ─── Security: Disable drag & selection ─────────────────────────────
    document.addEventListener('dragstart', function (e) {
        if (e.target.closest('.pdf-container')) e.preventDefault();
    });
    document.addEventListener('selectstart', function (e) {
        if (e.target.closest('.pdf-container')) e.preventDefault();
    });

    // ─── Security: Override console ─────────────────────────────────────
    if (window.console) {
        window.console.log = window.console.info = window.console.warn = window.console.dir = function () { };
    }

    // ─── PDF.js Viewer ──────────────────────────────────────────────────
    window.initPdfViewer = function (pdfUrl) {
        var container = document.getElementById('pdfViewer');
        var loadingEl = document.getElementById('pdfLoading');
        if (!container) return;

        loadPdfJs(function () {
            renderPdf(pdfUrl, container, loadingEl);
        });
    };

    function loadPdfJs(callback) {
        if (window.pdfjsLib) {
            callback();
            return;
        }
        var script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.0.379/pdf.min.js';
        script.integrity = 'sha512-RV8E2GDM7W0pvJN+5jWfa+0tV6B88ZPX2JEJxohQxP2dW3ahehRTvRj3RX6Gq7bVgDy3F7lx/8AjRD4gPp2Tow==';
        script.crossOrigin = 'anonymous';
        script.onload = function () {
            pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.0.379/pdf.worker.min.js';
            callback();
        };
        script.onerror = function () {
            if (loadingEl) loadingEl.innerHTML = '<span style="color:red">Failed to load PDF viewer. Please refresh the page.</span>';
        };
        document.head.appendChild(script);
    }

    function renderPdf(url, container, loadingEl) {
        var loadingTask = pdfjsLib.getDocument(url);
        loadingTask.onProgress = function (progress) {
            if (loadingEl) {
                var pct = Math.round((progress.loaded / progress.total) * 100);
                loadingEl.innerHTML = '<div class="loading-spinner mb-2"></div><span>Loading... ' + pct + '%</span>';
            }
        };

        loadingTask.promise.then(function (pdf) {
            if (loadingEl) loadingEl.remove();
            container.innerHTML = '';
            container.style.textAlign = 'center';

            var pageNum = 1;

            function renderPage() {
                if (pageNum > pdf.numPages) return;
                pdf.getPage(pageNum).then(function (page) {
                    var viewport = page.getViewport({ scale: 1.5 });
                    var canvas = document.createElement('canvas');
                    canvas.className = 'pdf-page-canvas';
                    canvas.height = viewport.height;
                    canvas.width = viewport.width;

                    container.appendChild(canvas);

                    var ctx = canvas.getContext('2d');
                    var renderTask = page.render({ canvasContext: ctx, viewport: viewport });
                    renderTask.promise.then(function () {
                        pageNum++;
                        renderPage();
                    });
                });
            }
            renderPage();
        }).catch(function (err) {
            if (loadingEl) loadingEl.innerHTML = '<span style="color:red">Failed to load PDF: ' + err.message + '</span>';
        });
    }

    // ─── Print Handler ──────────────────────────────────────────────────
    window.handlePrint = function (event, bookId) {
        var btn = document.getElementById('printBtn');
        if (!btn) return;
        btn.disabled = true;
        btn.innerHTML = '<span class="loading-spinner me-1"></span>Preparing...';

        var copiesInput = document.getElementById('copiesInput');
        var copies = copiesInput ? parseInt(copiesInput.value, 10) || 1 : 1;

        // 1. Log the print
        fetch('/api/pdf/log-print/' + bookId, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ copies: copies }),
            credentials: 'same-origin'
        }).catch(function () { /* best-effort */ });

        // 2. Try local print agent first
        fetch('http://localhost:8080/api/print', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ bookId: bookId, copies: copies }),
            credentials: 'omit'
        }).then(function (response) {
            if (response.ok) {
                btn.disabled = false;
                btn.innerHTML = 'Print';
            } else {
                fallbackPrint(bookId, btn);
            }
        }).catch(function () {
            fallbackPrint(bookId, btn);
        });
    };

    function fallbackPrint(bookId, btn) {
        // Fallback: get a print token, open watermarked PDF in new window
        fetch('/api/pdf/print-token/' + bookId, {
            credentials: 'same-origin'
        }).then(function (r) { return r.json(); }).then(function (data) {
            var w = window.open('/api/pdf/print/' + bookId + '?token=' + data.token, '_blank');
            if (!w) {
                alert('Please allow popups to print the document.');
            }
            btn.disabled = false;
            btn.innerHTML = 'Print';
        }).catch(function () {
            var w = window.open('/api/pdf/print/' + bookId, '_blank');
            if (!w) {
                alert('Please allow popups to print the document.');
            }
            btn.disabled = false;
            btn.innerHTML = 'Print';
        });
    }
})();
