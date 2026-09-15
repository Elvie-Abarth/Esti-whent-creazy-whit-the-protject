// Delegated click handler for the "Print / save as PDF" buttons - an
// onclick="window.print()" attribute is an inline script, which the site's
// Content-Security-Policy (script-src 'self', no 'unsafe-inline') silently
// blocks: the button rendered fine, but clicking it did nothing at all,
// with no visible error unless you happened to check the console. Reading
// the same intent from a data-print attribute instead runs as ordinary
// same-origin script, which the policy already allows.
(function () {
    "use strict";

    document.addEventListener("click", function (event) {
        var target = event.target.closest("[data-print]");
        if (target) window.print();
    });
})();
