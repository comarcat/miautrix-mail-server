using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Miautrix.Mail.Worker;

/// <summary>
/// Port 587 — STARTTLS submission.
/// Concrete subclass so that three separate listener types can be registered
/// as independent <c>IHostedService</c> instances via the DI container.
/// </summary>
public sealed class SmtpSubmissionStartTlsListener : SmtpListenerService
{
    public SmtpSubmissionStartTlsListener(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpListenerService> logger,
        TlsCertificateProvider tlsCertificateProvider,
        WorkerHostOptions options)
        : base(scopeFactory, logger,
               port: 587,
               mode: SmtpListenerMode.SubmissionStartTls,
               tlsCert: tlsCertificateProvider.Certificate,
               hostname: options.Hostname,
               options: options.Smtp)
    {
    }
}

/// <summary>
/// Port 465 — implicit TLS submission (SMTPS).
/// </summary>
public sealed class SmtpSubmissionImplicitTlsListener : SmtpListenerService
{
    public SmtpSubmissionImplicitTlsListener(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpListenerService> logger,
        TlsCertificateProvider tlsCertificateProvider,
        WorkerHostOptions options)
        : base(scopeFactory, logger,
               port: 465,
               mode: SmtpListenerMode.SubmissionImplicitTls,
               tlsCert: tlsCertificateProvider.Certificate,
               hostname: options.Hostname,
               options: options.Smtp)
    {
    }
}

/// <summary>
/// Port 25 — inbound MX. Does not require SMTP AUTH; accepts mail for local domains.
/// </summary>
public sealed class SmtpInboundMxListener : SmtpListenerService
{
    public SmtpInboundMxListener(
        IServiceScopeFactory scopeFactory,
        ILogger<SmtpListenerService> logger,
        TlsCertificateProvider tlsCertificateProvider,
        WorkerHostOptions options)
        : base(scopeFactory, logger,
               port: 25,
               mode: SmtpListenerMode.Inbound,
               tlsCert: tlsCertificateProvider.Certificate,
               hostname: options.Hostname,
               options: options.Smtp)
    {
    }
}

/// <summary>Shared configuration for all listeners in this worker.</summary>
public sealed class WorkerHostOptions
{
    public string Hostname { get; init; } = "mail.miautrix.tech";
    public SmtpListenerOptions Smtp { get; init; } = SmtpListenerOptions.Default;
}

/// <summary>Resource limits and per-command idle deadlines shared by SMTP listeners.</summary>
public sealed record SmtpListenerOptions(int MaximumMessageSize, int MaximumRecipients, TimeSpan IdleTimeout, TimeSpan DataTimeout)
{
    public static SmtpListenerOptions Default { get; } = new(26_214_400, 100, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

    public static SmtpListenerOptions FromEnvironment()
    {
        static int ReadPositiveInt(string name, int defaultValue, int minimum = 1, int maximum = int.MaxValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (!int.TryParse(value, out var parsed) || parsed < minimum || parsed > maximum) throw new InvalidOperationException($"{name} must be an integer between {minimum} and {maximum}.");
            return parsed;
        }

        return new SmtpListenerOptions(
            ReadPositiveInt("MIAUTRIX_SMTP_MAX_MESSAGE_SIZE", Default.MaximumMessageSize),
            ReadPositiveInt("MIAUTRIX_SMTP_MAX_RECIPIENTS", Default.MaximumRecipients, 100),
            TimeSpan.FromSeconds(ReadPositiveInt("MIAUTRIX_SMTP_IDLE_TIMEOUT_SECONDS", (int)Default.IdleTimeout.TotalSeconds, maximum: int.MaxValue / 1000)),
            TimeSpan.FromSeconds(ReadPositiveInt("MIAUTRIX_SMTP_DATA_TIMEOUT_SECONDS", (int)Default.DataTimeout.TotalSeconds, maximum: int.MaxValue / 1000)));
    }
}
