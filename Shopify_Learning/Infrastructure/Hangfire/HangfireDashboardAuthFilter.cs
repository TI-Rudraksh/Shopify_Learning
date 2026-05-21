using Hangfire.Dashboard;

namespace ShopifyIntegration.Infrastructure.Hangfire;

/// <summary>
/// Protects the Hangfire dashboard with a secret key passed as a query-string parameter
/// or via a cookie set after a successful key check.
///
/// Usage:
///   Development : /hangfire?key=your-secret   (cookie is set for the session)
///   Production  : configure Hangfire:DashboardKey in appsettings / env vars
///
/// Replace with ASP.NET Core Identity / JWT auth in a real multi-user setup.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private const string CookieName = "HangfireAuth";

    private readonly string _expectedKey;

    public HangfireDashboardAuthFilter(IConfiguration configuration)
    {
        _expectedKey = configuration["Hangfire:DashboardKey"]
            ?? throw new InvalidOperationException(
                "Hangfire:DashboardKey is not configured. " +
                "Add it to appsettings.json or as an environment variable.");
    }

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        // 1. Already authenticated via cookie
        if (http.Request.Cookies.TryGetValue(CookieName, out var cookie)
            && cookie == _expectedKey)
            return true;

        // 2. Key supplied in query string → set cookie and allow
        if (http.Request.Query.TryGetValue("key", out var queryKey)
            && queryKey == _expectedKey)
        {
            http.Response.Cookies.Append(CookieName, _expectedKey, new CookieOptions
            {
                HttpOnly = true,
                Secure   = !http.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
                SameSite = SameSiteMode.Strict,
                MaxAge   = TimeSpan.FromHours(8),
            });
            return true;
        }

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return false;
    }
}
