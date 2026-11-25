namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>Canonical paths shared by discovery controllers and static frontend routing.</summary>
public static class DiscoveryRoutes
{
    public const string WellKnownAutoconfig = "/.well-known/autoconfig/mail/config-v1.1.xml";
    public const string MailAutoconfig = "/mail/config-v1.1.xml";
    public const string OutlookAutodiscover = "/autodiscover/autodiscover.xml";
    public const string MtaStsPolicy = "/.well-known/mta-sts.txt";

    public static bool IsDiscoveryPath(PathString path) =>
        string.Equals(path.Value, WellKnownAutoconfig, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path.Value, MailAutoconfig, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path.Value, OutlookAutodiscover, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path.Value, MtaStsPolicy, StringComparison.OrdinalIgnoreCase);
}
