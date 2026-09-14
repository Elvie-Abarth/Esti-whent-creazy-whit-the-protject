// Opening the confirmation link usually opens a *new* tab, leaving this one
// sitting on "check your email" - annoying, since the natural next step
// (browse the site, signed in) is back here, not in the new tab. Cookies are
// shared across every tab of the same browser though, so once the new tab
// confirms and signs in, this tab can find out just by asking the server
// again - no cross-tab messaging needed, and it works the instant the other
// tab succeeds rather than requiring a manual switch-back-and-refresh.
(function () {
    "use strict";

    var container = document.querySelector("[data-check-email]");
    if (!container) return;

    var returnUrl = container.dataset.returnUrl || "/";
    var pollIntervalMs = 2000;
    var maxPolls = 480; // ~16 minutes - a little past the link's own 15-minute expiry

    function isNowSignedIn() {
        return fetch("/Account/Profile", { method: "GET", redirect: "manual", credentials: "same-origin" })
            .then(function (response) {
                // With redirect: "manual", a same-origin fetch that the server
                // tried to redirect (Profile requires being signed in, and
                // sends an anonymous request to /Account/Login) resolves as an
                // opaque, unfollowed response rather than a normal one. A real
                // 200 here means this browser is authenticated now - only
                // possible if the confirmation link was opened successfully,
                // in this tab or another one.
                return response.type !== "opaqueredirect" && response.ok;
            })
            .catch(function () {
                return false;
            });
    }

    var pollsSoFar = 0;
    var timer = window.setInterval(function () {
        pollsSoFar += 1;
        if (pollsSoFar > maxPolls) {
            window.clearInterval(timer);
            return;
        }
        isNowSignedIn().then(function (signedIn) {
            if (signedIn) {
                window.clearInterval(timer);
                window.location.href = returnUrl;
            }
        });
    }, pollIntervalMs);
})();
