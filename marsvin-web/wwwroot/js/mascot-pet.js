// Clicking the footer mascot, or a guinea pig's photo/drawing on the home
// page's "looking for a home" section, pops a little heart and plays a
// short squeak - purely decorative. Delegated listener reading a class off
// the target, the same pattern as confirm-delete.js/print-button.js, since
// the site's Content-Security-Policy (script-src 'self', no
// 'unsafe-inline') blocks an inline onclick="" attribute outright.
(function () {
    "use strict";

    // Synthesised with the Web Audio API rather than an audio file - no
    // asset to fetch (nothing for the CSP to worry about either), and it's
    // only ever called from a real click/keydown handler, so the "audio
    // needs a user gesture first" browser restriction is already satisfied.
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

    // Restarts the CSS pop animation even on rapid repeat clicks: removing
    // the class, forcing a reflow, then re-adding it restarts it from 0%
    // instead of the click being ignored mid-animation.
    function popHeart(heart, popClass) {
        if (!heart) return;
        heart.classList.remove(popClass);
        void heart.offsetWidth;
        heart.classList.add(popClass);
    }

    function pet(footerButton, cavyWrap) {
        if (footerButton) {
            popHeart(footerButton.querySelector(".footer-mascot-heart"), "footer-mascot-heart--pop");
        }
        if (cavyWrap) {
            popHeart(cavyWrap.querySelector(".cavy-heart"), "cavy-heart--pop");
        }
        playSqueak();
    }

    document.addEventListener("click", function (event) {
        var footerButton = event.target.closest(".footer-mascot-button");
        if (footerButton) {
            pet(footerButton, null);
            return;
        }

        // A guinea pig's photo/drawing sits inside the <a class="cavy"> link
        // to its profile - stop that navigation here so clicking the photo
        // itself just pets it, while the rest of the card (name, text)
        // still goes to the profile as normal.
        var cavyWrap = event.target.closest(".cavy-photo-wrap");
        if (cavyWrap) {
            event.preventDefault();
            event.stopPropagation();
            pet(null, cavyWrap);
        }
    });

    // .cavy-photo-wrap is role="button" tabindex="0" - Enter/Space should
    // activate it the same way a real <button> would, since it can't
    // actually be one (a <button> can't nest inside the <a> it sits in).
    document.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " ") return;

        var cavyWrap = event.target.closest(".cavy-photo-wrap");
        if (!cavyWrap) return;

        event.preventDefault();
        pet(null, cavyWrap);
    });
})();
