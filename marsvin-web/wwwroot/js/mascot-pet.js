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
    //
    // A single clean oscillator reads as a video-game blip, not an animal -
    // a real guinea pig "wheek" has a breathy, rasped quality on top of the
    // rising pitch. Band-pass-filtered white noise gives that rasp; a faint
    // sine underneath gives the pitch some body without the whole thing
    // sounding like a synth tone on its own.
    function playSqueak() {
        var Ctx = window.AudioContext || window.webkitAudioContext;
        if (!Ctx) return;

        try {
            var ctx = new Ctx();
            var now = ctx.currentTime;
            var duration = 0.18;

            var bufferSize = Math.ceil(ctx.sampleRate * duration);
            var noiseBuffer = ctx.createBuffer(1, bufferSize, ctx.sampleRate);
            var data = noiseBuffer.getChannelData(0);
            for (var i = 0; i < bufferSize; i++) data[i] = Math.random() * 2 - 1;

            var noise = ctx.createBufferSource();
            noise.buffer = noiseBuffer;

            var filter = ctx.createBiquadFilter();
            filter.type = "bandpass";
            filter.Q.value = 5;
            filter.frequency.setValueAtTime(900, now);
            filter.frequency.exponentialRampToValueAtTime(2200, now + duration * 0.45);
            filter.frequency.exponentialRampToValueAtTime(1100, now + duration);

            var noiseGain = ctx.createGain();
            noiseGain.gain.setValueAtTime(0.0001, now);
            noiseGain.gain.exponentialRampToValueAtTime(0.3, now + 0.015);
            noiseGain.gain.exponentialRampToValueAtTime(0.0001, now + duration);

            noise.connect(filter);
            filter.connect(noiseGain);
            noiseGain.connect(ctx.destination);

            var tone = ctx.createOscillator();
            tone.type = "sine";
            tone.frequency.setValueAtTime(900, now);
            tone.frequency.exponentialRampToValueAtTime(2000, now + duration * 0.45);
            tone.frequency.exponentialRampToValueAtTime(1000, now + duration);

            var toneGain = ctx.createGain();
            toneGain.gain.setValueAtTime(0.0001, now);
            toneGain.gain.exponentialRampToValueAtTime(0.07, now + 0.015);
            toneGain.gain.exponentialRampToValueAtTime(0.0001, now + duration);

            tone.connect(toneGain);
            toneGain.connect(ctx.destination);

            noise.start(now);
            noise.stop(now + duration);
            tone.start(now);
            tone.stop(now + duration);
            tone.onended = function () {
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
