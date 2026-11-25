using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Miautrix.Mail.Web.Controllers;

namespace Miautrix.Mail.IntegrationTests.Api;

public sealed class AnonymousActionAllowlistTests
{
    [Fact]
    public void Every_controller_action_is_protected_or_on_the_reviewed_allowlist()
    {
        var controllerTypes = typeof(AuthController).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));
        var actions = controllerTypes.SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(method => (Controller: controller, Method: method,
                Template: Combine(controller.GetCustomAttribute<RouteAttribute>()?.Template, method.GetCustomAttributes<HttpMethodAttribute>(true).First().Template)))).ToArray();
        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var isAllowlisted = IsReviewedAnonymous(action.Template, action.Method.GetCustomAttributes<HttpMethodAttribute>(true).First().HttpMethods);
            var isMarked = action.Method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null ||
                           action.Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
            Assert.Equal(isAllowlisted, isMarked);
        }
    }

    private static string Combine(string? controller, string? action) =>
        string.Join('/', new[] { controller?.Trim('/'), action?.Trim('/') }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static bool IsReviewedAnonymous(string route, IEnumerable<string> methods) =>
        (route.Equals("api/v1/auth/login", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("api/v1/inbound/cloudflare", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST")) ||
        (route.Equals("api/v1/public/calendar/invitations/{token}", StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals("api/v1/public/calendar/invitations/{token}/proposal", StringComparison.OrdinalIgnoreCase) && methods.Contains("GET")) ||
        (route.Equals("api/v1/public/calendar/invitations/{token}/respond", StringComparison.OrdinalIgnoreCase) && methods.Contains("POST"));
}
