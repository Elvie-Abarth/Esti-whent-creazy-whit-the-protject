using MarsvinWebExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace MarsvinWebExample.Pages;

// The endpoint behind the chat bubble in _Layout.cshtml (wwwroot/js/chat.js
// posts the bubble's own form here and shows the JSON answer). The answer
// comes from a keyword lookup inside this app (see KeywordChatAssistant) -
// no outside service, nothing to pay for. Public, so still:
// - its own rate limit ("chat" in Program.cs), so it can't be hammered;
// - the antiforgery token every Razor Pages POST already requires;
// - a hard cap on the question's length, checked here, not just via
//   maxlength in the markup.
[EnableRateLimiting("chat")]
public class ChatModel(IChatAssistant assistant) : PageModel
{
    public const int MaxQuestionLength = 200;

    // Nothing to see on a GET - the chat lives in the bubble, not on a page.
    public IActionResult OnGet() => RedirectToPage("/Faq");

    public IActionResult OnPost(string? message, string? lang, string? previous = null)
    {
        var english = lang == "en";
        var question = message?.Trim() ?? "";
        if (question.Length is 0 or > MaxQuestionLength)
        {
            return new JsonResult(new
            {
                answer = english
                    ? $"Write a question of at most {MaxQuestionLength} characters."
                    : $"Skriv et spørgsmål på højst {MaxQuestionLength} tegn."
            });
        }

        // The visitor's own previous question, sent along by the page so a
        // follow-up ("what does she cost?") makes sense. Same length cap;
        // anything longer is simply left out.
        var before = previous?.Trim();
        if (before is { Length: 0 or > MaxQuestionLength }) before = null;

        var reply = assistant.Reply(question, english, before);
        return new JsonResult(new { answer = reply.Answer, suggestions = reply.Suggestions });
    }
}
