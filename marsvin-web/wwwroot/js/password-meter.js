// Live strength meter for any <input data-password-meter> - no server
// round-trip, just a quick heuristic so the user gets feedback while
// typing. The server's own [StringLength(MinimumLength = 8)] check (see
// Register/Profile/ResetPassword InputModel) is still what's actually
// enforced - this is guidance, not validation.
//
// Styled to match the rest of the site rather than a generic progress bar:
// the four segments are little paw-print "footsteps" (the same oval-feet
// shape _Cavy.cshtml draws under every guinea pig portrait) walking in a
// trail, and the label carries a tiny cavy silhouette built the same way
// _Cavy.cshtml's own placeholder drawing is - two overlapping ellipses.
(function () {
    "use strict";

    var LEVELS = [
        { da: "Meget svag", en: "Very weak", color: "var(--fleece)" },
        { da: "Svag", en: "Weak", color: "var(--fleece)" },
        { da: "Rimelig", en: "Fair", color: "var(--bark)" },
        { da: "God", en: "Good", color: "var(--grass)" },
        { da: "Stærk", en: "Strong", color: "var(--meadow)" }
    ];

    var MASCOT_SVG =
        '<svg class="password-meter-mascot" viewBox="0 0 32 20" aria-hidden="true">' +
        '<ellipse cx="18" cy="12.5" rx="13" ry="7"/>' +
        '<ellipse cx="7" cy="9" rx="6.5" ry="6"/>' +
        '<circle class="password-meter-mascot-eye" cx="4.3" cy="8" r="1.1"/>' +
        "</svg>";

    function score(password) {
        var s = 0;
        if (password.length >= 8) s++;
        if (password.length >= 12) s++;
        if (/[a-z]/.test(password) && /[A-Z]/.test(password)) s++;
        if (/[0-9]/.test(password)) s++;
        if (/[^A-Za-z0-9]/.test(password)) s++;
        return Math.min(s, 4);
    }

    function build(input) {
        var wrap = document.createElement("div");
        wrap.className = "password-meter";
        wrap.hidden = true;

        var bar = document.createElement("div");
        bar.className = "password-meter-bar";
        for (var i = 0; i < 4; i++) {
            var step = document.createElement("span");
            step.className = "password-meter-step";
            bar.appendChild(step);
        }

        var label = document.createElement("span");
        label.className = "password-meter-label";
        label.innerHTML = MASCOT_SVG + '<span class="password-meter-text"></span>';

        wrap.appendChild(bar);
        wrap.appendChild(label);
        input.insertAdjacentElement("afterend", wrap);

        return {
            wrap: wrap,
            steps: bar.children,
            label: label,
            text: label.querySelector(".password-meter-text"),
            mascot: label.querySelector(".password-meter-mascot"),
            lastScore: -1
        };
    }

    function update(meter, password) {
        if (!password) {
            meter.wrap.hidden = true;
            meter.lastScore = -1;
            return;
        }
        meter.wrap.hidden = false;

        var s = score(password);
        var level = LEVELS[s];

        for (var i = 0; i < meter.steps.length; i++) {
            var step = meter.steps[i];
            var filled = i < s;
            step.style.background = filled ? level.color : "var(--line)";
            // Only the step that just filled in hops - re-triggering the
            // animation on every step on every keystroke reads as jittery
            // rather than a little walk forward.
            step.classList.toggle("is-new", filled && i === s - 1 && s > meter.lastScore);
        }

        var lang = document.documentElement.lang === "en" ? "en" : "da";
        meter.text.textContent = level[lang];
        meter.label.style.color = level.color;
        meter.mascot.classList.toggle("is-chuffed", s === 4);

        meter.lastScore = s;
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("input[data-password-meter]").forEach(function (input) {
            var meter = build(input);
            input.addEventListener("input", function () { update(meter, input.value); });
        });
    });
})();
