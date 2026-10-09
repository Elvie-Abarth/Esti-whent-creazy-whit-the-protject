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

    // The "i" button swaps the conversation for a page of what Pip can be
    // asked. The conversation isn't cleared - "back" (or asking one of the
    // examples) shows it again exactly as it was.
    var panel = root.querySelector(".chat-panel");
    var info = root.querySelector(".chat-info");
    var help = root.querySelector(".chat-help");

    function showHelp(show) {
        help.hidden = !show;
        panel.classList.toggle("is-help", show);
        info.setAttribute("aria-expanded", show ? "true" : "false");
        if (show) help.scrollTop = 0;
        else input.focus();
    }
    info.addEventListener("click", function () { showHelp(help.hidden); });
    help.querySelector(".chat-help-back").addEventListener("click", function () { showHelp(false); });

    // A link can open Pip directly: .../#pip opens the chat, .../#pip-help
    // opens it on the help page.
    function openFromLink() {
        if (location.hash !== "#pip" && location.hash !== "#pip-help") return;
        root.open = true;
        showHelp(location.hash === "#pip-help");
    }
    openFromLink();
    window.addEventListener("hashchange", openFromLink);

    // Minimise: the header's button, or Escape while the chat has focus.
    // Only the <details> is closed - the messages stay in the log, so
    // opening it again (clicking Pip) picks the conversation back up.
    function minimise() {
        root.open = false;
        root.querySelector("summary").focus();
    }
    root.querySelector(".chat-close").addEventListener("click", minimise);
    root.addEventListener("keydown", function (event) {
        if (event.key !== "Escape" || !root.open) return;
        // Escape closes the help page first, the chat itself second.
        if (!help.hidden) showHelp(false);
        else minimise();
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

    // The ready-made questions under the log: a click asks the question as
    // if it had been typed, in whichever language the button is showing.
    root.querySelectorAll(".chat-suggestions button").forEach(function (button) {
        button.addEventListener("click", function () {
            if (send.disabled) return;
            if (!help.hidden) showHelp(false);
            input.value = button.textContent;
            form.requestSubmit();
        });
    });

    // The visitor's last question, sent along with the next one so a
    // follow-up like "what does she cost?" can be understood. Kept only in
    // this page's memory - gone on reload.
    var lastQuestion = "";

    // "Did you mean" buttons under an answer Pip didn't have: one click asks.
    function offer(suggestions) {
        if (!suggestions || !suggestions.length) return;
        var row = document.createElement("div");
        row.className = "chat-suggestions chat-suggestions--inline";
        suggestions.forEach(function (text) {
            var button = document.createElement("button");
            button.type = "button";
            button.textContent = text;
            button.addEventListener("click", function () {
                if (send.disabled) return;
                input.value = text;
                form.requestSubmit();
            });
            row.appendChild(button);
        });
        log.appendChild(row);
    }

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        var text = input.value.trim();
        if (!text || send.disabled) return;

        // Once a conversation is going, the ready-made questions make room for it.
        panel.classList.add("has-chat");
        add("user", text);
        input.value = "";
        send.disabled = true;
        var pending = add("bot", "…");

        var data = new FormData(form);
        data.set("message", text);
        data.set("lang", english() ? "en" : "da");
        if (lastQuestion) data.set("previous", lastQuestion);
        lastQuestion = text;

        fetch(form.action, { method: "POST", body: data, credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) throw response.status;
                return response.json();
            })
            .then(function (result) {
                pending.textContent = result.answer;
                offer(result.suggestions);
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
