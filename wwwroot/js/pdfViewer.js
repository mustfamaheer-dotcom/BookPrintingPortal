(function () {
    'use strict';

    document.addEventListener('contextmenu', function (e) {
        e.preventDefault();
    });

    document.addEventListener('keydown', function (e) {
        var ctrl = e.ctrlKey || e.metaKey;
        var key = e.key.toLowerCase();

        if (ctrl && key === 's') { e.preventDefault(); e.stopPropagation(); return; }
        if (ctrl && key === 'p') { e.preventDefault(); e.stopPropagation(); return; }
        if (ctrl && key === 'u') { e.preventDefault(); return; }
        if (e.key === 'F12') { e.preventDefault(); return; }
        if (ctrl && e.shiftKey && key === 'i') { e.preventDefault(); return; }
        if (ctrl && e.shiftKey && key === 'j') { e.preventDefault(); return; }
        if (ctrl && key === 'c' && e.target.closest('#pdfViewer')) { e.preventDefault(); return; }
        if (ctrl && e.shiftKey && key === 'c') { e.preventDefault(); return; }
    }, true);

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

    document.addEventListener('dragstart', function (e) {
        if (e.target.closest('.pdf-container')) e.preventDefault();
    });
    document.addEventListener('selectstart', function (e) {
        if (e.target.closest('.pdf-container')) e.preventDefault();
    });

    if (window.console) {
        window.console.log = window.console.info = window.console.warn = window.console.dir = function () { };
    }

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

        loadPdfJs(function () {
            loadSecurePdf(bookId);
        });
    };

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
                var pct = Math.round((progress.loaded / progress.total) * 100);
                loadingEl.innerHTML = '<div class="loading-spinner mb-2"></div><span>Loading... ' + pct + '%</span>';
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

    window.handlePrint = async function (event, bookId) {
        var btn = document.getElementById('printBtn');
        if (!btn) return;
        btn.disabled = true;
        btn.innerHTML = '<span class="loading-spinner me-1"></span>Preparing...';

        var copiesInput = document.getElementById('copiesInput');
        var copies = copiesInput ? parseInt(copiesInput.value, 10) || 1 : 1;

        try {
            var response = await fetch('/api/pdf/process-print', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ bookId: bookId, copies: copies })
            });

            if (response.status === 401 || response.status === 403) {
                alert('Access Denied: You are not authorized to print this book.');
                btn.disabled = false;
                btn.innerHTML = 'Print';
                return;
            }

            var data = await response.json();

            if (data.success) {
                if (data.password) {
                    alert('Print job ' + data.jobId + ' initiated.\n\nPassword for this print file: ' + data.password + '\n\nIf you save as PDF, this password will be required to open it.');
                } else {
                    alert('Print job ' + data.jobId + ' initiated.');
                }

                try {
                    fetch('http://localhost:8080/api/print', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ bookId: bookId, copies: copies, jobId: data.jobId, password: data.password }),
                        credentials: 'omit'
                    }).catch(function () { });
                } catch (e) { }

                window.print();
            } else {
                alert('Failed to initiate print job: ' + (data.error || 'Unknown error'));
            }
        } catch (error) {
            console.error(error);
            alert('Print failed. Please try again.');
        }

        btn.disabled = false;
        btn.innerHTML = 'Print';
    };
})();
