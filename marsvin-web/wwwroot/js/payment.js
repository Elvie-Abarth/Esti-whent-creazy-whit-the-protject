// Small comforts for the payment form (Cart/Payment.cshtml). Everything
// here is a convenience on top of a form that works without it - the server
// checks every field itself and accepts the card fields with or without the
// spaces and slash this adds.
(function () {
    "use strict";

    var form = document.querySelector("form.payment-card");
    if (!form) return;

    function english() {
        return document.documentElement.lang === "en";
    }

    function field(name) {
        return form.querySelector("[name='Input." + name + "']");
    }

    var number = field("CardNumber");
    var expiry = field("Expiry");
    var cvc = field("Cvc");
    var holder = field("CardHolder");

    // "4242424242424242" becomes "4242 4242 4242 4242" while typing.
    if (number) {
        number.addEventListener("input", function () {
            var digits = number.value.replace(/\D/g, "").slice(0, 19);
            number.value = digits.replace(/(.{4})(?=.)/g, "$1 ");
        });
    }

    // "1229" becomes "12/29" - the slash doesn't have to be typed.
    if (expiry) {
        expiry.addEventListener("input", function () {
            var digits = expiry.value.replace(/\D/g, "").slice(0, 4);
            expiry.value = digits.length > 2 ? digits.slice(0, 2) + "/" + digits.slice(2) : digits;
        });
    }

    if (cvc) {
        cvc.addEventListener("input", function () {
            cvc.value = cvc.value.replace(/\D/g, "").slice(0, 4);
        });
    }

    // The payment is a demo, so a test card is one click away. The well-known
    // 4242... test number - never a real card.
    var demo = form.querySelector("[data-demo-card]");
    if (demo && number && expiry && cvc && holder) {
        demo.hidden = false;
        demo.addEventListener("click", function () {
            var guestName = field("GuestName");
            if (!holder.value) holder.value = (guestName && guestName.value) || "Demo Kunde";
            number.value = "4242 4242 4242 4242";
            expiry.value = "12/29";
            cvc.value = "123";
            var card = form.querySelector("[name='Input.PaymentMethod'][value='Card']");
            if (card) card.checked = true;
        });
    }

    // Back from the server with something to fix: go straight to the first
    // field that has a message, instead of landing at the top of the page.
    var firstError = Array.prototype.filter.call(form.querySelectorAll(".field-error"), function (span) {
        return span.textContent.trim() !== "";
    })[0];
    if (firstError) {
        var label = firstError.closest("label");
        var input = label && label.querySelector("input");
        (label || firstError).scrollIntoView({ block: "center" });
        if (input) input.focus({ preventScroll: true });
    }

    // One order per click: the button says what's happening and can't be pressed twice.
    form.addEventListener("submit", function () {
        var button = form.querySelector("button[type=submit]");
        if (!button) return;
        // Disabled a moment later, so the click that started this submit still goes through.
        setTimeout(function () { button.disabled = true; }, 0);
        button.textContent = english() ? "Processing ..." : "Behandler ...";
    });

    // Coming back with the browser's Back button must not leave the button stuck.
    window.addEventListener("pageshow", function (event) {
        var button = form.querySelector("button[type=submit]");
        if (event.persisted && button) {
            button.disabled = false;
            button.textContent = english() ? "Pay now (demo)" : "Betal nu (demo)";
        }
    });
})();
