// Secure PDF Viewer - Security Measures

// Block right-click context menu
document.addEventListener('contextmenu', function (e) {
    e.preventDefault();
    return false;
});

// Block common keyboard shortcuts for saving, printing, and developer tools
document.addEventListener('keydown', function (e) {
    // Ctrl+S (Save), Ctrl+Shift+S (Save As)
    if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S')) {
        e.preventDefault();
        return false;
    }
    // Ctrl+P (Print) - handled by our custom button
    if ((e.ctrlKey || e.metaKey) && (e.key === 'p' || e.key === 'P')) {
        e.preventDefault();
        return false;
    }
    // Ctrl+U (View Source)
    if ((e.ctrlKey || e.metaKey) && (e.key === 'u' || e.key === 'U')) {
        e.preventDefault();
        return false;
    }
    // F12 (DevTools)
    if (e.key === 'F12') {
        e.preventDefault();
        return false;
    }
    // Ctrl+Shift+I (DevTools)
    if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'i' || e.key === 'I')) {
        e.preventDefault();
        return false;
    }
    // Ctrl+Shift+J (Console)
    if ((e.ctrlKey || e.metaKey) && e.shiftKey && (e.key === 'j' || e.key === 'J')) {
        e.preventDefault();
        return false;
    }
    // Ctrl+C (Copy) - block only in viewer context
    if ((e.ctrlKey || e.metaKey) && (e.key === 'c' || e.key === 'C')) {
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

// Disable drag and drop (prevent dragging PDF out)
document.addEventListener('dragstart', function (e) {
    e.preventDefault();
    return false;
});

// Disable selection
document.addEventListener('selectstart', function (e) {
    if (e.target.closest('#pdfViewer') || e.target.closest('.pdf-viewer')) {
        e.preventDefault();
        return false;
    }
});

// Prevent the PDF from being opened in a new tab via middle-click
document.addEventListener('mousedown', function (e) {
    if (e.button === 1) {
        if (e.target.closest('#pdfViewer') || e.target.closest('a[href*="/api/pdf/"]')) {
            e.preventDefault();
            return false;
        }
    }
});

// Blazor interop: print function called from C#
window.printPdf = function () {
    const iframe = document.getElementById('pdfViewer');
    if (iframe && iframe.contentWindow) {
        try {
            iframe.contentWindow.focus();
            iframe.contentWindow.print();
        } catch (e) {
            window.print();
        }
    } else {
        window.print();
    }
};

// Override console methods to reduce information leakage
if (window.console) {
    window.console.log = function () { };
    window.console.info = function () { };
    window.console.warn = function () { };
    window.console.dir = function () { };
}
