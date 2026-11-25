using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Miautrix.Mail.Web.Controllers;
using Miautrix.Mail.Web.Infrastructure;

namespace Miautrix.Mail.IntegrationTests.Api;

public sealed class AnonymousActionAllowlistTests
{
    [Fact]
    public void Every_controller_action_is_protected_or_on_the_reviewed_allowlist()
    {
        var controllerTypes = typeof(AuthController).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));
        var actions = controllerTypes.SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .SelectMany(http => ControllerRoutes(controller).Select(route => (Controller: controller, Method: method,
                    Template: Combine(route, http.Template), Methods: http.HttpMethods))))).ToArray();
        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var isAllowlisted = IsReviewedAnonymous(action.Template, action.Method.GetCustomAttributes<HttpMethodAttribute>(true)
                .Where(http => ControllerRoutes(action.Controller).Any(route => Combine(route, http.Template) == action.Template))
                .SelectMany(http => http.HttpMethods));
            var isMarked = action.Method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null ||
                           action.Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
            Assert.Equal(isAllowlisted, isMarked);
        }
    }

    private static IEnumerable<string?> ControllerRoutes(Type controller) =>
        controller.GetCustomAttributes<RouteAttribute>(inherit: true).Select(route => route.Template).DefaultIfEmpty(null);

    private static string Combine(string? controller, string? action)
    {
        if (!string.IsNullOrWhiteSpace(action) && action.StartsWith('/')) return action;
        var joined = string.Join('/', new[] { controller?.Trim('/'), action?.Trim('/') }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return "/" + joined;
    }

    private static bool IsReviewedAnonymous(string route, IEnumerable<string> methods) =>
        (route.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("/api/v1/inbound/cloudflare", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("/api/v1/public/calendar/invitations/{token}", StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals("/api/v1/public/calendar/invitations/{token}/proposal", StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals("/api/v1/public/calendar/invitations/{token}/respond", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals(DiscoveryRoutes.WellKnownAutoconfig, StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals(DiscoveryRoutes.MailAutoconfig, StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals(DiscoveryRoutes.OutlookAutodiscover, StringComparison.OrdinalIgnoreCase) && (methods.Contains("GET") || methods.Contains("POST"))) ||
        (route.Equals(DiscoveryRoutes.MtaStsPolicy, StringComparison.OrdinalIgnoreCase) && methods.Contains("GET"));
}
