// Live strength meter for any <input data-password-meter> - no server
// round-trip, just a quick heuristic so the user gets feedback while
// typing. The server's own [StringLength(MinimumLength = 8)] check (see
// Register/Profile/ResetPassword InputModel) is still what's actually
// enforced - this is guidance, not validation.
//
// What it shows, in the shop's own terms rather than a generic bar:
// a guinea pig walks along a track toward a carrot, one step further for
// every thing the password gets right, and the track fills in behind it.
// Under the track, the five things it's looking for are listed as chips
// that tick off as they're met - so the meter doesn't just say "weak", it
// says what to add. Reaching "strong" lets the guinea pig eat the carrot.
//
// Everything is built with createElement/textContent and positioned through
// element.style - no innerHTML with user input, and nothing the page's
// Content-Security-Policy (no inline styles or scripts) would block.
(function () {
    "use strict";

    var SVG_NS = "http://www.w3.org/2000/svg";

    var LEVELS = [
        { da: "Meget svag", en: "Very weak" },
        { da: "Svag", en: "Weak" },
        { da: "Rimelig", en: "Fair" },
        { da: "God", en: "Good" },
        { da: "Stærk", en: "Strong" }
    ];

    var TITLE = { da: "Adgangskodens styrke", en: "Password strength" };

    // Shown in place of a level while the field is still empty.
    var EMPTY = { da: "Skriv en adgangskode", en: "Type a password" };

    // Each rule is one point. Any four of the five makes "strong" - a long
    // passphrase doesn't have to contain a symbol as well.
    var RULES = [
        { da: "Mindst 8 tegn", en: "At least 8 characters", test: function (p) { return p.length >= 8; } },
        { da: "12 tegn eller flere", en: "12 or more characters", test: function (p) { return p.length >= 12; } },
        { da: "Store og små bogstaver", en: "Upper and lower case", test: function (p) { return /[a-z]/.test(p) && /[A-Z]/.test(p); } },
        { da: "Et tal", en: "A number", test: function (p) { return /[0-9]/.test(p); } },
        { da: "Et tegn som ! eller ?", en: "A symbol like ! or ?", test: function (p) { return /[^A-Za-z0-9]/.test(p); } }
    ];

    // Weak to strong reads the way everyone expects: red, orange, a
    // yellow-green, then a clear green for "strong". (An empty field gets no
    // colour at all - see update.) Real hex rather than CSS custom
    // properties, since they're blended channel by channel here; all four
    // are dark enough to read as text on the card's pale background.
    var ANCHORS = ["#C62828", "#D9730D", "#8A9A1B", "#2E8B3A"].map(function (hex) {
        return [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];
    });

    function colorAt(t) {
        var span = ANCHORS.length - 1;
        var pos = Math.max(0, Math.min(1, t)) * span;
        var i = Math.min(Math.floor(pos), span - 1);
        var local = pos - i;
        var a = ANCHORS[i], b = ANCHORS[i + 1];
        return "rgb(" +
            Math.round(a[0] + (b[0] - a[0]) * local) + "," +
            Math.round(a[1] + (b[1] - a[1]) * local) + "," +
            Math.round(a[2] + (b[2] - a[2]) * local) + ")";
    }

    function lang() {
        return document.documentElement.lang === "en" ? "en" : "da";
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text) node.textContent = text;
        return node;
    }

    function svg(className, viewBox, shapes) {
        var root = document.createElementNS(SVG_NS, "svg");
        root.setAttribute("class", className);
        root.setAttribute("viewBox", viewBox);
        root.setAttribute("aria-hidden", "true");
        shapes.forEach(function (shape) {
            var node = document.createElementNS(SVG_NS, shape[0]);
            Object.keys(shape[1]).forEach(function (name) { node.setAttribute(name, shape[1][name]); });
            root.appendChild(node);
        });
        return root;
    }

    // The same round little guinea pig as the footer's mascot - cream body,
    // pink ear - with a blushed cheek, a shine in the eye and a smile.
    // Drawn facing right, the way it walks.
    function pig() {
        return svg("password-meter-pig", "0 0 60 40", [
            ["ellipse", { cx: 27, cy: 23, rx: 24, ry: 12.5, fill: "#F0E7D2", stroke: "#D8C9A6", "stroke-width": 1 }],
            ["ellipse", { cx: 45.5, cy: 20, rx: 12, ry: 10.5, fill: "#F0E7D2", stroke: "#D8C9A6", "stroke-width": 1 }],
            ["ellipse", { cx: 22, cy: 24, rx: 9, ry: 7, fill: "#B5643C" }],
            ["ellipse", { cx: 46.5, cy: 10.5, rx: 4.4, ry: 3, transform: "rotate(28 46.5 10.5)", fill: "#B83C6E" }],
            ["circle", { cx: 50, cy: 23.5, r: 2.4, fill: "#E8A9B9", opacity: 0.8 }],
            ["circle", { cx: 51.5, cy: 18.5, r: 1.8, fill: "#23301F" }],
            ["circle", { cx: 52.1, cy: 17.9, r: 0.6, fill: "#fff" }],
            ["path", { d: "M52.5 24 q2.2 1.6 4 -0.4", fill: "none", stroke: "#23301F", "stroke-width": 1.1, "stroke-linecap": "round" }],
            ["ellipse", { cx: 16, cy: 35, rx: 4.5, ry: 2.2, fill: "#D8C9A6" }],
            ["ellipse", { cx: 38, cy: 35, rx: 4.5, ry: 2.2, fill: "#D8C9A6" }]
        ]);
    }

    function carrot() {
        return svg("password-meter-carrot", "0 0 28 28", [
            ["path", { d: "M16 3 q-3 3 -2 8", fill: "none", stroke: "#74964A", "stroke-width": 2.4, "stroke-linecap": "round" }],
            ["path", { d: "M20 4 q-1 4 -4 7", fill: "none", stroke: "#74964A", "stroke-width": 2.4, "stroke-linecap": "round" }],
            ["path", { d: "M24 8 q-4 1 -7 4", fill: "none", stroke: "#74964A", "stroke-width": 2.4, "stroke-linecap": "round" }],
            ["path", { d: "M18.5 10.5 C22 14 12 23 4 25.5 C6 17.5 14.5 7 18.5 10.5 Z", fill: "#E07B2A" }],
            ["path", { d: "M11 16.5 l2.2 1.6 M8.5 20 l1.8 1.3", fill: "none", stroke: "#B85F18", "stroke-width": 1.2, "stroke-linecap": "round" }]
        ]);
    }

    function build(input) {
        // Always visible, from the moment the page loads: the checklist is
        // most useful *before* the first character is typed.
        var wrap = el("div", "password-meter");

        var head = el("div", "password-meter-head");
        var title = el("span", "password-meter-title");
        var level = el("strong", "password-meter-level");
        // Announced when it changes, without stealing focus from the field.
        level.setAttribute("aria-live", "polite");
        head.appendChild(title);
        head.appendChild(level);

        var track = el("div", "password-meter-track");
        var fill = el("div", "password-meter-fill");
        var walker = el("div", "password-meter-walker");
        var heart = el("span", "password-meter-heart", "♥");
        heart.setAttribute("aria-hidden", "true");
        walker.appendChild(pig());
        walker.appendChild(heart);
        var goal = carrot();
        track.appendChild(fill);
        track.appendChild(goal);
        track.appendChild(walker);

        var rules = el("ul", "password-meter-rules");
        var chips = RULES.map(function () {
            var chip = el("li");
            rules.appendChild(chip);
            return chip;
        });

        wrap.appendChild(head);
        wrap.appendChild(track);
        wrap.appendChild(rules);
        input.insertAdjacentElement("afterend", wrap);

        return { wrap: wrap, title: title, level: level, fill: fill, walker: walker, goal: goal, chips: chips, lastScore: -1 };
    }

    function update(meter, password) {
        var language = lang();
        var met = RULES.map(function (rule) { return rule.test(password); });
        var score = Math.min(met.filter(Boolean).length, 4);
        var t = score / 4;
        var color = colorAt(t);

        // An empty field isn't "very weak" yet - it's just not started: the
        // guinea pig waits at the start and the level reads as a prompt.
        meter.title.textContent = TITLE[language];
        meter.level.textContent = password ? LEVELS[score][language] : EMPTY[language];
        meter.level.style.color = password ? color : "";
        meter.wrap.classList.toggle("is-empty", !password);

        var strong = score === 4;

        // Up to "good", the walker's left edge travels toward a point just
        // short of the carrot, and the fill reaches the middle of the guinea
        // pig. At "strong" the carrot is gone, so the guinea pig walks on to
        // the very end and the bar fills completely - a strong password
        // shouldn't leave a stretch of empty track behind it.
        if (strong) {
            meter.walker.style.left = "calc(100% - 3.2rem)";
            meter.fill.style.width = "100%";
        } else {
            meter.walker.style.left = "calc(" + t + " * (100% - 5.4rem))";
            meter.fill.style.width = "calc(" + t + " * (100% - 5.4rem) + 1.4rem)";
        }
        meter.fill.style.background = color;

        meter.wrap.classList.toggle("is-strong", strong);
        // The little celebration plays when "strong" is reached, not on every
        // keystroke typed after that.
        meter.walker.classList.toggle("is-celebrating", strong && meter.lastScore !== 4);

        meter.chips.forEach(function (chip, i) {
            chip.textContent = RULES[i][language];
            chip.classList.toggle("is-met", met[i]);
        });

        meter.lastScore = score;
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("input[data-password-meter]").forEach(function (input) {
            var meter = build(input);
            update(meter, input.value);
            input.addEventListener("input", function () { update(meter, input.value); });

            // The texts here are written by this script, not by the page's
            // own DA/EN swap, so they're redrawn when the language button is
            // used (after its own click handler has switched the language).
            var toggle = document.getElementById("lang-toggle");
            if (toggle) {
                toggle.addEventListener("click", function () {
                    setTimeout(function () { update(meter, input.value); }, 0);
                });
            }
        });
    });
})();
