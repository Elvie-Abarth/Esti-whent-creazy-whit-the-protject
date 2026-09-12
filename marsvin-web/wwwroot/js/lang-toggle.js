// Client-side DA/EN toggle. Static markup carries the Danish text plus a
// data-en (and, for attributes, data-en-aria-label) alternative; this just
// swaps between them and remembers the choice per browser.
(function () {
    "use strict";

    var STORAGE_KEY = "marsvin-lang";

    function apply(lang) {
        document.documentElement.lang = lang === "en" ? "en" : "da";

        document.querySelectorAll("[data-en]").forEach(function (el) {
            if (el.dataset.da === undefined) el.dataset.da = el.textContent;
            el.textContent = lang === "en" ? el.dataset.en : el.dataset.da;
        });

        document.querySelectorAll("[data-en-aria-label]").forEach(function (el) {
            if (el.dataset.daAriaLabel === undefined) {
                el.dataset.daAriaLabel = el.getAttribute("aria-label") || "";
            }
            el.setAttribute(
                "aria-label",
                lang === "en" ? el.dataset.enAriaLabel : el.dataset.daAriaLabel
            );
        });

        document.querySelectorAll("[data-en-alt]").forEach(function (el) {
            if (el.dataset.daAlt === undefined) {
                el.dataset.daAlt = el.getAttribute("alt") || "";
            }
            el.setAttribute("alt", lang === "en" ? el.dataset.enAlt : el.dataset.daAlt);
        });

        var toggle = document.getElementById("lang-toggle");
        if (toggle) {
            toggle.textContent = lang === "en" ? "DA" : "EN";
            toggle.setAttribute(
                "aria-label",
                lang === "en" ? "Skift til dansk" : "Switch to English"
            );
        }
    }

    function currentLang() {
        try {
            return localStorage.getItem(STORAGE_KEY) || "da";
        } catch (e) {
            return "da";
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        apply(currentLang());

        var toggle = document.getElementById("lang-toggle");
        if (!toggle) return;

        toggle.addEventListener("click", function () {
            var next = currentLang() === "en" ? "da" : "en";
            try {
                localStorage.setItem(STORAGE_KEY, next);
            } catch (e) {
                /* private browsing etc. - toggle still works for this page view */
            }
            apply(next);
        });
    });
})();
