// The support chat bubble (markup in _Layout.cshtml). Posts the bubble's own
// form - antiforgery token included - to /Chat and shows the JSON answer.
// Every message is written with textContent, never innerHTML: an answer (or
// a visitor's own text) can't turn into markup or script on the page.
(function () {
    "use strict";

    var root = document.getElementById("chat");
    if (!root) return;

    var form = root.querySelector("form");
    var log = root.querySelector(".chat-log");
    var input = form.querySelector("input[name=message]");
    var send = form.querySelector("button[type=submit]");

    // Hidden until here, so a browser without JavaScript never shows a chat that can't work.
    root.hidden = false;

    // Minimise: the header's button, or Escape while the chat has focus.
    // Only the <details> is closed - the messages stay in the log, so
    // opening it again (clicking Pip) picks the conversation back up.
    function minimise() {
        root.open = false;
        root.querySelector("summary").focus();
    }
    root.querySelector(".chat-close").addEventListener("click", minimise);
    root.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && root.open) minimise();
    });

    // Opening puts the cursor straight in the question box.
    root.addEventListener("toggle", function () {
        if (root.open) input.focus();
    });

    function english() {
        return document.documentElement.lang === "en";
    }

    function add(kind, text) {
        var p = document.createElement("p");
        p.className = "chat-msg chat-msg--" + kind;
        p.textContent = text;
        log.appendChild(p);
        log.scrollTop = log.scrollHeight;
        return p;
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        var text = input.value.trim();
        if (!text || send.disabled) return;

        add("user", text);
        input.value = "";
        send.disabled = true;
        var pending = add("bot", "…");

        var data = new FormData(form);
        data.set("message", text);
        data.set("lang", english() ? "en" : "da");

        fetch(form.action, { method: "POST", body: data, credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) throw response.status;
                return response.json();
            })
            .then(function (result) {
                pending.textContent = result.answer;
            })
            .catch(function (status) {
                pending.textContent = status === 429
                    ? (english() ? "That's a lot of questions at once - wait a minute and try again."
                                 : "Det var mange spørgsmål på én gang - vent et minut, og prøv igen.")
                    : (english() ? "Something went wrong. Try again, or write to us on the contact page."
                                 : "Noget gik galt. Prøv igen, eller skriv til os på kontaktsiden.");
            })
            .then(function () {
                send.disabled = false;
                log.scrollTop = log.scrollHeight;
                input.focus();
            });
    });
})();
