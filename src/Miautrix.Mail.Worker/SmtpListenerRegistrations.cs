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
               hostname: options.Hostname)
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
               hostname: options.Hostname)
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
               hostname: options.Hostname)
    {
    }
}

/// <summary>Shared configuration for all listeners in this worker.</summary>
public sealed class WorkerHostOptions
{
    public string Hostname { get; init; } = "mail.miautrix.tech";
}
