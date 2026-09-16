// Switches the Admin/Stock page between its Accessories and Guinea pigs
// tables - both stay in the DOM (so a slow connection doesn't need a second
// round-trip to see the other one), only [hidden] toggles.
(function () {
    "use strict";

    var tabs = document.querySelectorAll(".stock-tab");
    var panels = document.querySelectorAll("[data-tab-panel]");
    if (!tabs.length || !panels.length) return;

    tabs.forEach(function (tab) {
        tab.addEventListener("click", function () {
            var target = tab.dataset.tab;

            tabs.forEach(function (t) {
                var active = t === tab;
                t.classList.toggle("is-active", active);
                t.setAttribute("aria-selected", String(active));
            });
            panels.forEach(function (panel) {
                panel.hidden = panel.dataset.tabPanel !== target;
            });
        });
    });
})();
