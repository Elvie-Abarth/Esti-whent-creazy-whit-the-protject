// Live strength meter for any <input data-password-meter> - no server
// round-trip, just a quick heuristic so the user gets feedback while
// typing. The server's own [StringLength(MinimumLength = 8)] check (see
// Register/Profile/ResetPassword InputModel) is still what's actually
// enforced - this is guidance, not validation.
(function () {
    "use strict";

    var LEVELS = [
        { da: "Meget svag", en: "Very weak", color: "var(--fleece)" },
        { da: "Svag", en: "Weak", color: "var(--fleece)" },
        { da: "Rimelig", en: "Fair", color: "var(--bark)" },
        { da: "God", en: "Good", color: "var(--grass)" },
        { da: "Stærk", en: "Strong", color: "var(--meadow)" }
    ];

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
        for (var i = 0; i < 4; i++) bar.appendChild(document.createElement("span"));

        var label = document.createElement("span");
        label.className = "password-meter-label";

        wrap.appendChild(bar);
        wrap.appendChild(label);
        input.insertAdjacentElement("afterend", wrap);

        return { wrap: wrap, segments: bar.children, label: label };
    }

    function update(meter, password) {
        if (!password) {
            meter.wrap.hidden = true;
            return;
        }
        meter.wrap.hidden = false;

        var s = score(password);
        var level = LEVELS[s];
        for (var i = 0; i < meter.segments.length; i++) {
            meter.segments[i].style.background = i < s ? level.color : "var(--line)";
        }

        var lang = document.documentElement.lang === "en" ? "en" : "da";
        meter.label.textContent = level[lang];
        meter.label.style.color = level.color;
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("input[data-password-meter]").forEach(function (input) {
            var meter = build(input);
            input.addEventListener("input", function () { update(meter, input.value); });
        });
    });
})();
