// Clicking the footer mascot pops a little heart above it - purely
// decorative. Delegated listener reading a class off the target, the same
// pattern as confirm-delete.js/print-button.js, since the site's
// Content-Security-Policy (script-src 'self', no 'unsafe-inline') blocks an
// inline onclick="" attribute outright.
(function () {
    "use strict";

    document.addEventListener("click", function (event) {
        var button = event.target.closest(".footer-mascot-button");
        if (!button) return;

        var heart = button.querySelector(".footer-mascot-heart");
        if (!heart) return;

        // Restart the CSS animation even on rapid repeat clicks: removing
        // the class, forcing a reflow, then re-adding it restarts it from
        // 0% instead of the click being ignored mid-animation.
        heart.classList.remove("footer-mascot-heart--pop");
        void heart.offsetWidth;
        heart.classList.add("footer-mascot-heart--pop");
    });
})();
