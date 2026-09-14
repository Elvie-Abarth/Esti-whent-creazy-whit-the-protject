// Delegated confirm() for destructive forms - see the data-confirm attribute.
// A plain onsubmit="confirm('...')" with a name/title interpolated into it is
// unsafe even though Razor HTML-encodes the value: the browser decodes the
// attribute back to raw characters *before* parsing it as JS, so an admin-
// or customer-supplied name containing something like x'); fetch(...); //
// can still close the string and run arbitrary script in whoever clicks the
// button's session. Reading the same value from a data attribute instead
// never has it parsed as code - dataset returns it as a plain string.
(function () {
    "use strict";

    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        var message = form.dataset.confirm;
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });
})();
