// Auto-submits a cart line's quantity form as soon as it changes - typing a
// new number and tabbing away, or clicking the spinner arrows, both fire the
// native "change" event. There's no separate "Update" button any more (see
// Index.cshtml) - without JS, the lone number field still submits on Enter
// per the browser's own implicit-submission behaviour for a single-field form.
(function () {
    "use strict";

    document.addEventListener("change", function (event) {
        var input = event.target;
        if (input.matches('.cart-table input[type="number"][name="quantity"]')) {
            input.form.requestSubmit();
        }
    });
})();
