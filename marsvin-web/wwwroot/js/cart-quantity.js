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

    // The +/- stepper buttons (type="button", so they never submit the form
    // on their own) nudge the number field and fire "change" themselves -
    // plain HTML doesn't dispatch that event for a script-driven value
    // change, only for direct user interaction with the field.
    document.addEventListener("click", function (event) {
        var step = event.target.closest(".qty-step");
        if (!step) return;

        var input = step.parentElement.querySelector('input[type="number"][name="quantity"]');
        if (!input) return;

        var min = parseInt(input.min, 10) || 1;
        var next = (parseInt(input.value, 10) || 0) + parseInt(step.dataset.step, 10);
        input.value = Math.max(min, next);
        input.dispatchEvent(new Event("change", { bubbles: true }));
    });
})();
