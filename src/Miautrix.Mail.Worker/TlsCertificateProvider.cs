using System.Security.Cryptography.X509Certificates;

namespace Miautrix.Mail.Worker;

public sealed class TlsCertificateProvider
{
    public TlsCertificateProvider(X509Certificate2? certificate)
    {
        Certificate = certificate;
    }

    public X509Certificate2? Certificate { get; }
}
