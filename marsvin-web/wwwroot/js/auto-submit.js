// A <form data-auto-submit> sends itself as soon as one of its <select>s
// changes, so picking a sort order doesn't need a second click. Without
// JavaScript the form's own submit button does the same job; with it, the
// button is hidden (the class is added here, not in the markup, so the
// button never disappears for someone who needs it).
(function () {
    "use strict";

    document.querySelectorAll("form[data-auto-submit]").forEach(function (form) {
        form.classList.add("is-auto");
        form.addEventListener("change", function (event) {
            if (event.target.tagName === "SELECT") form.submit();
        });
    });
})();
