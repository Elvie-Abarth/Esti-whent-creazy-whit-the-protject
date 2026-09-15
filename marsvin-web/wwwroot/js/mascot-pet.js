// Clicking the footer mascot pops a little heart above it and plays a short
// squeak - purely decorative. Delegated listener reading a class off the
// target, the same pattern as confirm-delete.js/print-button.js, since the
// site's Content-Security-Policy (script-src 'self', no 'unsafe-inline')
// blocks an inline onclick="" attribute outright.
(function () {
    "use strict";

    // Synthesised with the Web Audio API rather than an audio file - no
    // asset to fetch (nothing for the CSP to worry about either), and it's
    // only ever built inside a real click handler, so the "audio needs a
    // user gesture first" browser restriction is already satisfied.
    function playSqueak() {
        var Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return;

        try {
            var ctx = new Ctx();
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();

            osc.type = "sawtooth";
            osc.frequency.setValueAtTime(650, ctx.currentTime);
            osc.frequency.exponentialRampToValueAtTime(1500, ctx.currentTime + 0.08);
            osc.frequency.exponentialRampToValueAtTime(550, ctx.currentTime + 0.22);

            gain.gain.setValueAtTime(0.0001, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.22);

            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.25);
            osc.onended = function () {
                ctx.close();
            };
        } catch (e) {
            // Web Audio blocked or unsupported - the heart still pops, so
            // failing silently here just means no sound, not a broken click.
        }
    }

    document.addEventListener("click", function (event) {
        var button = event.target.closest(".footer-mascot-button");
        if (!button) return;

        var heart = button.querySelector(".footer-mascot-heart");
        if (heart) {
            // Restart the CSS animation even on rapid repeat clicks: removing
            // the class, forcing a reflow, then re-adding it restarts it from
            // 0% instead of the click being ignored mid-animation.
            heart.classList.remove("footer-mascot-heart--pop");
            void heart.offsetWidth;
            heart.classList.add("footer-mascot-heart--pop");
        }

        playSqueak();
    });
})();
