// Secure PDF Viewer - Security Measures

// Block right-click context menu everywhere
document.addEventListener('contextmenu', function (e) {
    e.preventDefault();
    return false;
});

// Block ALL keyboard shortcuts for saving, downloading, printing, devtools
document.addEventListener('keydown', function (e) {
    if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === 'p' || e.key === 'P')) {
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === 'u' || e.key === 'U')) {
        e.preventDefault();
        return false;
    }
    if (e.key === 'F12') {
        e.preventDefault();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'i' || e.key === 'I')) {
        e.preventDefault();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'j' || e.key === 'J')) {
        e.preventDefault();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === 'c' || e.key === 'C')) {
        e.preventDefault();
        return false;
    }
    if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'c' || e.key === 'C')) {
        e.preventDefault();
        return false;
    }
});

// Detect DevTools opening via window size change
let devToolsOpen = false;
const threshold = 160;

setInterval(function () {
    const widthThreshold = window.outerWidth - window.innerWidth > threshold;
    const heightThreshold = window.outerHeight - window.innerHeight > threshold;
    if (widthThreshold || heightThreshold) {
        if (!devToolsOpen) {
            devToolsOpen = true;
            document.body.innerHTML = '<div style="padding: 40px; text-align: center;"><h1>Access Denied</h1><p>Developer tools detected. Please close them and reload the page.</p></div>';
        }
    } else {
        devToolsOpen = false;
    }
}, 1000);

// Disable drag and drop
document.addEventListener('dragstart', function (e) {
    e.preventDefault();
    return false;
});

// Disable selection on the PDF viewer area
document.addEventListener('selectstart', function (e) {
    if (e.target.closest('.pdf-container')) {
        e.preventDefault();
        return false;
    }
});

// PDF overlay: blocks right-click, forwards scroll to iframe
(function () {
    const overlay = document.getElementById('pdfOverlay');
    const iframe = document.getElementById('pdfViewer');
    if (!overlay || !iframe) return;

    overlay.addEventListener('wheel', function (e) {
        if (iframe && iframe.contentWindow) {
            iframe.contentWindow.scrollBy(0, e.deltaY);
        }
    }, { passive: true });

    overlay.addEventListener('contextmenu', function (e) {
        e.preventDefault();
        return false;
    });
})();

// Handle print from native button click (preserves user gesture)
window.handlePrint = function (event, bookId) {
    var btn = event.currentTarget;
    btn.disabled = true;
    btn.innerHTML = '<span class="loading-spinner me-1"></span>Logging...';

    // Read copies from the input field
    var copiesInput = document.querySelector('input[type="number"]');
    var copies = copiesInput ? parseInt(copiesInput.value, 10) || 1 : 1;

    // Log print via API (best-effort), then print
    fetch('/api/pdf/log-print/' + bookId, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ copies: copies }),
        credentials: 'same-origin'
    }).catch(function () { /* best-effort */ });

    window.print();

    // Restore button after print dialog closes
    window.onafterprint = function () {
        btn.disabled = false;
        btn.innerHTML = 'Print';
        window.onafterprint = null;
    };
};

// Override console methods
if (window.console) {
    window.console.log = function () { };
    window.console.info = function () { };
    window.console.warn = function () { };
    window.console.dir = function () { };
}
