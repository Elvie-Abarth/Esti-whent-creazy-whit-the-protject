// Auto-submits a cart line's quantity form as soon as it changes - typing a
// new number and tabbing away, or clicking the spinner arrows, both fire the
// native "change" event, so the separate "Update" button stops being
// something you have to remember to click. The button itself stays in the
// markup (Index.cshtml) as a fallback for anyone without JS.
(function () {
    "use strict";

    document.addEventListener("change", function (event) {
        var input = event.target;
        if (input.matches('.cart-table input[type="number"][name="quantity"]')) {
            input.form.requestSubmit();
        }
    });
})();
