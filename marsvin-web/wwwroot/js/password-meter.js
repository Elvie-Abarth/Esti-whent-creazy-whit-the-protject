// Live strength meter for any <input data-password-meter> - no server
// round-trip, just a quick heuristic so the user gets feedback while
// typing. The server's own [StringLength(MinimumLength = 8)] check (see
// Register/Profile/ResetPassword InputModel) is still what's actually
// enforced - this is guidance, not validation.
//
// Styled to match the rest of the site rather than a generic progress bar:
// the trail is little paw-print "footsteps" (the same oval-feet shape
// _Cavy.cshtml draws under every guinea pig portrait), each one coloured
// by its position along a weak -> strong gradient rather than jumping
// between four flat colours, and the label carries a tiny cavy silhouette
// built the same way _Cavy.cshtml's own placeholder drawing is - two
// overlapping ellipses.
(function () {
    "use strict";

    // var(--bark) reads as a muddy, indistinct middle step here (it's a
    // muted brown meant for body text, not a signal colour) - a real amber
    // between the site's pink and green makes weak -> strong read as a
    // clear traffic-light gradient instead of two clear ends with a fuzzy
    // middle. One-off hex rather than a new :root token since nothing else
    // on the site needs an amber.
    var LABELS = [
        { da: "Meget svag", en: "Very weak" },
        { da: "Svag", en: "Weak" },
        { da: "Rimelig", en: "Fair" },
        { da: "God", en: "Good" },
        { da: "Stærk", en: "Strong" }
    ];

    // Anchor colours for the gradient the steps walk through - pink to
    // amber to grass to dark meadow-green. Real hex rather than the site's
    // CSS custom properties, since these get mixed channel-by-channel in
    // JS (a var(--x) string can't be interpolated).
    var ANCHORS = ["#B83C6E", "#C98A2E", "#74964A", "#2C4327"].map(function (hex) {
        return [
            parseInt(hex.slice(1, 3), 16),
            parseInt(hex.slice(3, 5), 16),
            parseInt(hex.slice(5, 7), 16)
        ];
    });

    // Colour at position t (0..1) along the full weak -> strong gradient,
    // linearly interpolated across the anchors above. More steps than
    // score levels is what turns 4 flat colour blocks into an actual
    // gradient the paw trail walks through.
    function colorAt(t) {
        var span = ANCHORS.length - 1;
        var pos = Math.max(0, Math.min(1, t)) * span;
        var i = Math.min(Math.floor(pos), span - 1);
        var localT = pos - i;
        var a = ANCHORS[i], b = ANCHORS[i + 1];
        var r = Math.round(a[0] + (b[0] - a[0]) * localT);
        var g = Math.round(a[1] + (b[1] - a[1]) * localT);
        var bl = Math.round(a[2] + (b[2] - a[2]) * localT);
        return "rgb(" + r + "," + g + "," + bl + ")";
    }

    var STEP_COUNT = 8;

    // Dark eye/mouth tone matches _Cavy.cshtml's own placeholder drawing
    // (#23301F) exactly, rather than the pale "eye" dot the first version
    // used - a real dark pupil plus a small white sparkle and blushed
    // cheek is what makes a face read as cute instead of just a shape with
    // a hole in it.
    var MASCOT_SVG =
        '<svg class="password-meter-mascot" viewBox="0 0 34 22" aria-hidden="true">' +
        '<ellipse cx="19" cy="13.5" rx="14" ry="7.5"/>' +
        '<ellipse cx="7.6" cy="9.3" rx="7.3" ry="6.9"/>' +
        '<ellipse class="password-meter-mascot-ear" cx="5.1" cy="4" rx="3.1" ry="2.2" transform="rotate(-25 5.1 4)"/>' +
        '<circle class="password-meter-mascot-cheek" cx="9.3" cy="11.8" r="1.7"/>' +
        '<circle class="password-meter-mascot-eye" cx="4.5" cy="8.7" r="1.3"/>' +
        '<circle class="password-meter-mascot-eye-shine" cx="4.95" cy="8.25" r="0.42"/>' +
        '<path class="password-meter-mascot-mouth" d="M2.5 10.7 q1.9 1.7 3.8 0.2"/>' +
        "</svg>";

    // Each "footstep" is a real paw print (pad + three toes), not a plain
    // dot - the toes are what makes it recognisable as a paw rather than
    // just a coloured blob walking across the field.
    var PAW_SVG =
        '<svg class="password-meter-step" viewBox="0 0 20 20" aria-hidden="true">' +
        '<ellipse class="password-meter-step-pad" cx="10" cy="13.6" rx="6" ry="4.3"/>' +
        '<circle cx="4.2" cy="6.3" r="1.9"/>' +
        '<circle cx="10" cy="3.5" r="2.1"/>' +
        '<circle cx="15.8" cy="6.3" r="1.9"/>' +
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
        bar.innerHTML = new Array(STEP_COUNT + 1).join(PAW_SVG);

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
            lastFilled: -1
        };
    }

    function update(meter, password) {
        if (!password) {
            meter.wrap.hidden = true;
            meter.lastFilled = -1;
            return;
        }
        meter.wrap.hidden = false;

        var s = score(password);
        var t = s / 4; // 0..1 across the five score levels
        var filledCount = Math.round(t * STEP_COUNT);

        for (var i = 0; i < meter.steps.length; i++) {
            var step = meter.steps[i];
            var filled = i < filledCount;
            step.style.color = filled ? colorAt(i / (STEP_COUNT - 1)) : "var(--line)";
            step.classList.toggle("is-filled", filled);
            // Every step that just filled in hops - jumping several score
            // points in one keystroke (e.g. typing the char that both
            // crosses 12 characters and adds the first digit) fills more
            // than one paw at once, and all of them should hop together.
            step.classList.toggle("is-new", filled && i >= meter.lastFilled);
        }

        var lang = document.documentElement.lang === "en" ? "en" : "da";
        var levelColor = colorAt(t);
        meter.text.textContent = LABELS[s][lang];
        meter.label.style.color = levelColor;
        meter.mascot.classList.toggle("is-chuffed", s === 4);

        meter.lastFilled = filledCount;
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("input[data-password-meter]").forEach(function (input) {
            var meter = build(input);
            input.addEventListener("input", function () { update(meter, input.value); });
        });
    });
})();
