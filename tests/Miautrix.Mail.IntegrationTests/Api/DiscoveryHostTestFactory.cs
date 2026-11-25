using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Miautrix.Mail.IntegrationTests.Api;

internal static class DiscoveryHostTestFactory
{
    internal static WebApplicationFactory<Miautrix.Mail.Web.Program> WithPublicMailHosts(
        this WebApplicationFactory<Miautrix.Mail.Web.Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MIAUTRIX_PUBLIC_IMAP_HOST"] = "imap.mailhost.test",
                ["MIAUTRIX_PUBLIC_SMTP_HOST"] = "smtp.mailhost.test"
            })));
}
