using System.Security.Claims;
using MarsvinWebExample.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace MarsvinWebExample.Data;

/// <summary>
/// The small JSON API behind JWT bearer authentication - see JWT-NOTES.txt.
/// Two kinds of endpoint:
///   POST /api/auth/token  accepts the website's login COOKIE and hands out a JWT
///   everything else       accepts only a JWT ("Authorization: Bearer ...")
/// The two never mix: the pages ignore bearer tokens (the cookie scheme is the
/// default one), and /api ignores the cookie (so a forged form post from
/// another site can't act through a visitor's session here).
/// </summary>
public static class ApiEndpoints
{
    // One policy per role set, each pinned to the bearer scheme - without
    // AuthenticationSchemes an [Authorize] would try the default (cookie) one.
    public const string AnyUserPolicy = "ApiAnyUser";
    public const string StaffPolicy = "ApiStaff";
    public const string AdminPolicy = "ApiAdmin";

    public static void AddApiPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(AnyUserPolicy, policy => policy
            .AddAuthenticationSchemes(JwtTokens.Scheme).RequireAuthenticatedUser());
        options.AddPolicy(StaffPolicy, policy => policy
            .AddAuthenticationSchemes(JwtTokens.Scheme).RequireRole(nameof(UserRole.Admin), nameof(UserRole.Employee)));
        options.AddPolicy(AdminPolicy, policy => policy
            .AddAuthenticationSchemes(JwtTokens.Scheme).RequireRole(nameof(UserRole.Admin)));
    }

    public static void MapApi(this WebApplication app)
    {
        // Only a person who is signed in on the website (cookie) can get a
        // token - so the email-link step and the authenticator code have
        // already happened, and the API is no way round them. SameSite=Lax on
        // the cookie keeps another site from triggering this POST, and the
        // answer can't be read cross-origin anyway.
        app.MapPost("/api/auth/token", (HttpContext http, IUserAccountStore users, JwtSettings settings, TimeProvider time) =>
        {
            var user = int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? users.FindById(userId)
                : null;
            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var (token, expiresAt) = JwtTokens.Create(user, settings, time.GetUtcNow());
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token, tokenType = "Bearer", expiresAt });
        })
        .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme })
        .RequireRateLimiting("auth");

        var api = app.MapGroup("/api");

        // Any signed-in role: who the token says you are.
        api.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.FindFirstValue(JwtTokens.SubjectClaim),
            name = user.FindFirstValue(JwtTokens.NameClaim),
            email = user.FindFirstValue(JwtTokens.EmailClaim),
            role = user.FindFirstValue(JwtTokens.RoleClaim)
        })).RequireAuthorization(AnyUserPolicy);

        // The caller's OWN orders: the user id comes from the verified token,
        // never from the URL or the query string - there is no way to ask for
        // someone else's (same rule as IOrderStore.FindForUser on the website).
        api.MapGet("/orders", (ClaimsPrincipal user, IOrderStore orders) =>
            Results.Ok(orders.GetOrdersForUser(CallerId(user)).Select(OrderSummary)))
            .RequireAuthorization(AnyUserPolicy);

        api.MapGet("/orders/{id:int}", (int id, ClaimsPrincipal user, IOrderStore orders) =>
            orders.FindForUser(id, CallerId(user)) is { } order
                ? Results.Ok(OrderSummary(order))
                : Results.NotFound())
            .RequireAuthorization(AnyUserPolicy);

        // Staff: every order. Admin: also the account list.
        api.MapGet("/admin/orders", (IOrderStore orders) =>
            Results.Ok(orders.GetAllOrders().Select(OrderSummary)))
            .RequireAuthorization(StaffPolicy);

        api.MapGet("/admin/users", (IUserAccountStore users) =>
            Results.Ok(users.GetAll().Select(u => new
            {
                id = u.UserId,
                name = u.DisplayName,
                email = u.Email,
                role = u.Role.ToString(),
                active = u.IsActive
            })))
            .RequireAuthorization(AdminPolicy);
    }

    private static int CallerId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue(JwtTokens.SubjectClaim)!);

    private static object OrderSummary(Order order) => new
    {
        id = order.OrderId,
        status = order.Status.ToString(),
        total = order.TotalPrice,
        placed = order.CreatedAt,
        delivery = order.DeliveryMethod.ToString(),
        items = order.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice })
    };
}
