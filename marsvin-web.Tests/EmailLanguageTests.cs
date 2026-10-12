using System.Reflection;
using System.Security.Claims;
using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Pages.Account;
using MarsvinWebExample.Tests.Data;
using MarsvinWebExample.Tests.Pages.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ScheduleIndexModel = MarsvinWebExample.Pages.Admin.Schedule.IndexModel;

namespace MarsvinWebExample.Tests;

/// <summary>
/// Every email follows the language of the site. Which language that is
/// depends on who caused the mail: the recipient's own browser (the
/// "marsvin-lang" cookie) when they triggered it themselves, and the
/// language stored on their account when someone - or nothing - else did.
/// The donation and order mails have their own tests next to those pages;
/// this covers the account, staff and background mails, the footer, and how
/// the account's stored language is kept up to date.
/// </summary>
[Collection("SqlCatalog collection")]
public class EmailLanguageTests(SqlCatalogFixture fixture)
{
    private const string Password = "OriginalPass123!";

    private readonly SqlUserAccountStore _users = new(fixture.ConnectionString);
    private readonly SqlPendingLoginStore _pendingLogins = new(fixture.ConnectionString);
    private readonly RecordingEmailSender _email = new();

    private ApplicationUser NewUser(
        UserRole role = UserRole.Customer, bool prefersEnglish = false,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var email = $"{caller}-{Guid.NewGuid():N}@example.com".ToLowerInvariant();
        _users.CreateUser(email, new PasswordHasher<ApplicationUser>().HashPassword(null!, Password), caller, role);
        var user = _users.FindByEmail(email)!;
        if (prefersEnglish) _users.SetPrefersEnglish(user.UserId, true);
        return _users.FindById(user.UserId)!;
    }

    // A request from a browser with the site in the given language ("en"/"da"),
    // or with no language cookie at all (null). Signed in when a user is given.
    private static PageContext Browser(string? language, ApplicationUser? signedInAs = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new RecordingAuthenticationService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        if (language is not null)
            httpContext.Request.Headers.Cookie = $"{SiteLanguage.CookieName}={language}";
        if (signedInAs is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, signedInAs.UserId.ToString()),
                    new Claim(ClaimTypes.Role, signedInAs.Role.ToString())
                ],
                authenticationType: "Test"));
        }
        return new PageContext { HttpContext = httpContext };
    }

    private (string Subject, string Body, bool English) MailTo(string address)
    {
        var index = _email.Sent.FindIndex(m => string.Equals(m.ToEmail, address, StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0, $"No mail was sent to {address}.");
        return (_email.Sent[index].Subject, _email.Sent[index].Body, _email.SentInEnglish[index]);
    }

    // ---- Mails the recipient triggers themselves: the browser's language ----

    [Theory]
    [InlineData("en", "Confirm your login to Marsvin", "Hello ")]
    [InlineData("da", "Bekræft login til Marsvin", "Hej ")]
    [InlineData(null, "Bekræft login til Marsvin", "Hej ")]
    public async Task LoginConfirmation_FollowsTheBrowser(string? language, string expectedSubject, string expectedGreeting)
    {
        var user = NewUser();
        var model = new LoginModel(_users, _pendingLogins, _email, new AlwaysPassRecaptchaVerifier(), new LoginLockoutTracker())
        {
            PageContext = Browser(language),
            Input = new LoginModel.InputModel { Email = user.Email, Password = Password }
        };

        await model.OnPostAsync(returnUrl: null);

        var mail = MailTo(user.Email);
        Assert.Equal(expectedSubject, mail.Subject);
        Assert.StartsWith(expectedGreeting, mail.Body);
        Assert.Equal(language == "en", mail.English);
        Assert.Contains("/Account/ConfirmLogin?token=", mail.Body);
    }

    [Fact]
    public async Task Registering_InEnglish_SendsAnEnglishWelcome_AndTheAccountStartsOutEnglish()
    {
        var address = $"register-en-{Guid.NewGuid():N}@example.com";
        var model = new RegisterModel(_users, _pendingLogins, _email, new AlwaysPassRecaptchaVerifier())
        {
            PageContext = Browser("en"),
            Input = new RegisterModel.InputModel
            {
                Email = address, DisplayName = "English Customer", Password = "SomePass123!", ConfirmPassword = "SomePass123!"
            }
        };

        await model.OnPostAsync(returnUrl: null);

        var mail = MailTo(address);
        Assert.Equal("Confirm your account at Marsvin", mail.Subject);
        Assert.Contains("Welcome to Marsvin!", mail.Body);
        Assert.True(mail.English);
        Assert.True(_users.FindByEmail(address)!.PrefersEnglish);
    }

    [Fact]
    public async Task Registering_InDanish_KeepsTheDanishWelcome_AndTheAccountStaysDanish()
    {
        var address = $"register-da-{Guid.NewGuid():N}@example.com";
        var model = new RegisterModel(_users, _pendingLogins, _email, new AlwaysPassRecaptchaVerifier())
        {
            PageContext = Browser("da"),
            Input = new RegisterModel.InputModel
            {
                Email = address, DisplayName = "Dansk Kunde", Password = "SomePass123!", ConfirmPassword = "SomePass123!"
            }
        };

        await model.OnPostAsync(returnUrl: null);

        Assert.Equal("Bekræft din konto hos Marsvin", MailTo(address).Subject);
        Assert.False(_users.FindByEmail(address)!.PrefersEnglish);
    }

    [Fact]
    public async Task ForgotPassword_InEnglish_SendsTheResetLinkInEnglish()
    {
        var user = NewUser();
        var model = new ForgotPasswordModel(_users, _pendingLogins, _email, new AlwaysPassRecaptchaVerifier())
        {
            PageContext = Browser("en"),
            Input = new ForgotPasswordModel.InputModel { Email = user.Email }
        };

        await model.OnPostAsync();

        var mail = MailTo(user.Email);
        Assert.Equal("Reset your Marsvin password", mail.Subject);
        Assert.Contains("/Account/ResetPassword?token=", mail.Body);
        Assert.True(mail.English);
    }

    [Fact]
    public async Task ResetPassword_InEnglish_ConfirmsTheChangeInEnglish()
    {
        var user = NewUser();
        var token = _pendingLogins.Create(user.UserId, returnUrl: null, TimeSpan.FromMinutes(15));
        var model = new ResetPasswordModel(_users, _pendingLogins, new LoginLockoutTracker(), _email)
        {
            PageContext = Browser("en"),
            Token = token,
            Input = new ResetPasswordModel.InputModel { NewPassword = "BrandNewPass456!", ConfirmNewPassword = "BrandNewPass456!" }
        };

        await model.OnPostAsync();

        var mail = MailTo(user.Email);
        Assert.Equal("Your Marsvin password has been changed", mail.Subject);
        Assert.Contains("forgot password", mail.Body);
        Assert.True(mail.English);
    }

    [Fact]
    public async Task ChangingEmailAndPasswordOnTheProfile_InEnglish_SendsBothNoticesInEnglish()
    {
        var user = NewUser();
        var newAddress = $"moved-{Guid.NewGuid():N}@example.com";
        var model = new ProfileModel(
            _users, new SqlOrderStore(fixture.ConnectionString), new SqlCartStore(fixture.ConnectionString),
            new SqlCatalog(fixture.ConnectionString), new SqlShiftStore(fixture.ConnectionString),
            new SqlTimeOffRequestStore(fixture.ConnectionString), _email)
        {
            PageContext = Browser("en", signedInAs: user),
            Input = new ProfileModel.InputModel
            {
                DisplayName = user.DisplayName, Email = newAddress, CurrentPassword = Password,
                NewPassword = "BrandNewPass456!", ConfirmNewPassword = "BrandNewPass456!"
            }
        };

        await model.OnPostAsync();

        // The email notice goes to the old address, the password notice to the new one.
        var emailNotice = MailTo(user.Email);
        Assert.Equal("Your email has been changed at Marsvin", emailNotice.Subject);
        Assert.Contains(newAddress, emailNotice.Body);
        Assert.True(emailNotice.English);
        var passwordNotice = MailTo(newAddress);
        Assert.Equal("Your Marsvin password has been changed", passwordNotice.Subject);
        Assert.True(passwordNotice.English);
    }

    // ---- Mails someone else triggers: the recipient's stored language ----

    [Theory]
    [InlineData(true, "Suspicious activity on your Marsvin account")]
    [InlineData(false, "Mistænkelig aktivitet på din Marsvin-konto")]
    public async Task TheFailedLoginWarning_IsInTheAccountOwnersLanguage_NotTheGuessersBrowser(bool ownerPrefersEnglish, string expectedSubject)
    {
        var owner = NewUser(prefersEnglish: ownerPrefersEnglish);
        var lockout = new LoginLockoutTracker();
        // Whoever is guessing has their browser in the *other* language.
        var guessersLanguage = ownerPrefersEnglish ? "da" : "en";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var model = new LoginModel(_users, _pendingLogins, _email, new AlwaysPassRecaptchaVerifier(), lockout)
            {
                PageContext = Browser(guessersLanguage),
                Input = new LoginModel.InputModel { Email = owner.Email, Password = "WrongPassword!" }
            };
            await model.OnPostAsync(returnUrl: null);
            ExpireLockoutDelay(lockout, owner.Email);
        }

        var mail = MailTo(owner.Email);
        Assert.Equal(expectedSubject, mail.Subject);
        Assert.Equal(ownerPrefersEnglish, mail.English);
    }

    // The tracker makes each retry wait longer than the last, so three
    // attempts back to back would only ever count as one. Rewinding the
    // delay stands in for the seconds a real person would have to wait
    // (the same approach LoginModelTests takes).
    private static void ExpireLockoutDelay(LoginLockoutTracker tracker, string email)
    {
        var entries = (System.Collections.IDictionary)typeof(LoginLockoutTracker)
            .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(tracker)!;
        var entry = entries[email]!;
        entry.GetType().GetField("DelayedUntil")!.SetValue(entry, DateTime.UtcNow.AddSeconds(-1));
    }

    [Fact]
    public async Task AShiftChange_ReachesAnEnglishEmployeeInEnglish_EvenFromADanishAdmin()
    {
        var admin = NewUser(UserRole.Admin);
        var employee = NewUser(UserRole.Employee, prefersEnglish: true);
        var shifts = new SqlShiftStore(fixture.ConnectionString);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        ScheduleIndexModel MakeModel() => new(shifts, _users, new SqlTimeOffRequestStore(fixture.ConnectionString), _email)
        {
            PageContext = Browser("da", signedInAs: admin)
        };

        await MakeModel().OnPostCreateAsync(employee.UserId, date, new TimeOnly(14, 0), new TimeOnly(18, 0), note: null);

        var added = MailTo(employee.Email);
        Assert.Equal("Your schedule has been updated", added.Subject);
        Assert.Contains("A new shift has been added", added.Body);
        Assert.True(added.English);

        _email.Sent.Clear();
        _email.SentInEnglish.Clear();
        await MakeModel().OnPostDeleteAsync(shifts.GetForUser(employee.UserId).Single().ShiftId);

        var removed = MailTo(employee.Email);
        Assert.Contains("A shift has been removed", removed.Body);
        Assert.True(removed.English);
    }

    [Fact]
    public async Task AShiftChange_ReachesADanishEmployeeInDanish_EvenFromAnEnglishAdmin()
    {
        var admin = NewUser(UserRole.Admin, prefersEnglish: true);
        var employee = NewUser(UserRole.Employee);
        var model = new ScheduleIndexModel(
            new SqlShiftStore(fixture.ConnectionString), _users, new SqlTimeOffRequestStore(fixture.ConnectionString), _email)
        {
            PageContext = Browser("en", signedInAs: admin)
        };

        await model.OnPostCreateAsync(
            employee.UserId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), new TimeOnly(14, 0), new TimeOnly(18, 0), note: null);

        var mail = MailTo(employee.Email);
        Assert.Equal("Din vagtplan er blevet opdateret", mail.Subject);
        Assert.False(mail.English);
    }

    [Fact]
    public async Task ADayOffRequest_ReachesEachAdminInTheirOwnLanguage()
    {
        var englishAdmin = NewUser(UserRole.Admin, prefersEnglish: true);
        var danishAdmin = NewUser(UserRole.Admin);
        var employee = NewUser(UserRole.Employee);
        var model = new ProfileModel(
            _users, new SqlOrderStore(fixture.ConnectionString), new SqlCartStore(fixture.ConnectionString),
            new SqlCatalog(fixture.ConnectionString), new SqlShiftStore(fixture.ConnectionString),
            new SqlTimeOffRequestStore(fixture.ConnectionString), _email)
        {
            PageContext = Browser("da", signedInAs: employee)
        };
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        await model.OnPostRequestTimeOffAsync(start, start.AddDays(2), "Tandlæge");

        var english = MailTo(englishAdmin.Email);
        Assert.StartsWith("New day-off request from ", english.Subject);
        Assert.Contains("Reason: Tandlæge", english.Body);      // the employee's own words are passed on as written
        Assert.True(english.English);
        var danish = MailTo(danishAdmin.Email);
        Assert.StartsWith("Ny ferieanmodning fra ", danish.Subject);
        Assert.False(danish.English);
    }

    [Theory]
    [InlineData(true, "Your Marsvin account will be deleted in about two months unless you log in")]
    [InlineData(false, "Din Marsvin-konto slettes om ca. to måneder, medmindre du logger ind")]
    public async Task TheInactivityWarning_IsInTheLanguageTheAccountLastUsed(bool prefersEnglish, string expectedSubject)
    {
        // Nobody is at a browser when this goes out - a background job sends it.
        var customer = NewUser(prefersEnglish: prefersEnglish);
        // 45 days short of two years: inside the two-month warning, not yet the one-month one.
        BackdateLastActiveAt(customer.UserId, DateTime.UtcNow - InactiveAccountCleanupService.InactivityThreshold + TimeSpan.FromDays(45));
        using var service = new InactiveAccountCleanupService(
            fixture.ConnectionString, "https://marsvin.example", _email, NullLogger<InactiveAccountCleanupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        for (var waited = 0; waited < 100 && !_email.Sent.Any(m => m.ToEmail == customer.Email); waited++)
            await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        var mail = MailTo(customer.Email);
        Assert.Equal(expectedSubject, mail.Subject);
        Assert.Equal(prefersEnglish, mail.English);
        Assert.Contains("https://marsvin.example/Account/Login", mail.Body);
    }

    private void BackdateLastActiveAt(int userId, DateTime lastActiveAt)
    {
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var command = new SqlCommand(
            "UPDATE dbo.Users SET LastActiveAt = @LastActiveAt WHERE UserId = @UserId;", connection);
        command.Parameters.AddWithValue("@LastActiveAt", lastActiveAt);
        command.Parameters.AddWithValue("@UserId", userId);
        command.ExecuteNonQuery();
    }

    // ---- Keeping the account's stored language up to date ----

    private async Task RevalidateWithLanguageCookie(ApplicationUser user, string? cookieValue)
    {
        var services = new ServiceCollection()
            .AddSingleton<IUserAccountStore>(_users)
            .AddSingleton<IAuthenticationService>(new RecordingAuthenticationService())
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        if (cookieValue is not null)
            httpContext.Request.Headers.Cookie = $"{SiteLanguage.CookieName}={cookieValue}";
        var context = new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(
                AuthCookiePrincipal.Build(user, DateTimeOffset.UtcNow), CookieAuthenticationDefaults.AuthenticationScheme));

        await AuthCookiePrincipal.RevalidateAsync(context);

        Assert.NotNull(context.Principal);      // the language never ends a session
    }

    [Fact]
    public async Task ASignedInRequest_WithTheSiteInEnglish_IsRememberedOnTheAccount_AndSwitchesBackWithIt()
    {
        var user = NewUser();

        await RevalidateWithLanguageCookie(user, "en");
        Assert.True(_users.FindById(user.UserId)!.PrefersEnglish);

        await RevalidateWithLanguageCookie(_users.FindById(user.UserId)!, "da");
        Assert.False(_users.FindById(user.UserId)!.PrefersEnglish);
    }

    [Theory]
    [InlineData(null)]                                  // no cookie: a browser that hasn't loaded a page with the script yet
    [InlineData("fr")]
    [InlineData("EN")]                                  // only the exact values the script writes count
    [InlineData("en; DROP TABLE Users")]
    [InlineData("<script>alert(1)</script>")]
    public async Task ACookieThatIsMissingOrNotOneOfTheTwoValues_LeavesTheStoredLanguageAlone(string? cookieValue)
    {
        var user = NewUser(prefersEnglish: true);

        await RevalidateWithLanguageCookie(user, cookieValue);

        Assert.True(_users.FindById(user.UserId)!.PrefersEnglish);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("da", false)]
    [InlineData("EN", null)]
    [InlineData("english", null)]
    [InlineData("", null)]
    public void SiteLanguage_ReadsOnlyTheTwoExactValues(string cookieValue, bool? expected)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{SiteLanguage.CookieName}={cookieValue}";

        Assert.Equal(expected, SiteLanguage.Read(httpContext));
        Assert.Equal(expected == true, SiteLanguage.IsEnglish(httpContext));
    }

    [Fact]
    public void SiteLanguage_WithNoRequestAtAll_IsDanish()
    {
        Assert.Null(SiteLanguage.Read(null));
        Assert.False(SiteLanguage.IsEnglish(null));
        Assert.False(SiteLanguage.IsEnglish(new DefaultHttpContext()));
    }

    // ---- The branded HTML frame SmtpEmailSender wraps every mail in ----

    private static string BuildHtmlBody(string plainTextBody, bool english)
    {
        // Private in SmtpEmailSender: the only other way to see it is to send
        // a real email through a real mailbox.
        var method = typeof(SmtpEmailSender).GetMethod("BuildHtmlBody", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, [plainTextBody, null, english])!;
    }

    [Fact]
    public void TheHtmlFrame_HasADanishFooter_ForADanishMail()
    {
        var html = BuildHtmlBody("Hej Kunde,\n\nTak for din ordre.", english: false);

        Assert.Contains("""<html lang="da">""", html);
        Assert.Contains("Torsdag og fredag", html);
        Assert.Contains("Eksempelprojekt, ikke en registreret virksomhed.", html);
        Assert.DoesNotContain("Thursday", html);
    }

    [Fact]
    public void TheHtmlFrame_HasAnEnglishFooter_ForAnEnglishMail()
    {
        var html = BuildHtmlBody("Hello Customer,\n\nThank you for your order.", english: true);

        Assert.Contains("""<html lang="en">""", html);
        Assert.Contains("Thursday and Friday", html);
        Assert.Contains("Example project, not a registered company.", html);
        Assert.DoesNotContain("Torsdag", html);
        Assert.Contains("Havnegade 12, 6700 Esbjerg", html);
    }

    [Fact]
    public void TheHtmlFrame_EncodesWhatAVisitorTyped_AndTurnsLinksIntoRealLinks()
    {
        // A display name ends up in most mails - it must arrive as text, not as markup.
        var html = BuildHtmlBody(
            "Hej <img src=x onerror=alert(1)>,\n\nÅbn linket: http://localhost/Account/ConfirmLogin?token=abc", english: false);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("""<a href="http://localhost/Account/ConfirmLogin?token=abc" """, html);
        Assert.DoesNotContain("<img", html);        // no logo was passed in, so there is no image at all
    }
}
