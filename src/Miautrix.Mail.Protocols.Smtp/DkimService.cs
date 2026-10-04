using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Miautrix.Mail.Protocols.Smtp;

public record DkimKeyPair(string PrivateKeyPem, string PublicKeyBase64, string PublicKeyPem);

public interface IDkimService
{
    DkimKeyPair GenerateKeyPair(int keySize = 2048);
    string SignMessage(string rawMessage, string domain, string selector, string privateKeyPem);
    bool VerifyMessage(string rawSignedMessage, string publicKeyBase64);
}

public sealed partial class DkimService : IDkimService
{
    [GeneratedRegex(@"\r?\n", RegexOptions.Compiled)]
    private static partial Regex NewLineRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"DKIM-Signature:[ \t]*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DkimSignatureHeaderRegex();

    public DkimKeyPair GenerateKeyPair(int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var privateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks)}\n-----END PRIVATE KEY-----";

        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);
        var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(publicKeyBytes, Base64FormattingOptions.InsertLineBreaks)}\n-----END PUBLIC KEY-----";

        return new DkimKeyPair(privateKeyPem, publicKeyBase64, publicKeyPem);
    }

    public string SignMessage(string rawMessage, string domain, string selector, string privateKeyPem)
    {
        var (headers, body) = SplitMessage(rawMessage);
        var canonicalBody = CanonicalizeBodyRelaxed(body);
        var bodyHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalBody));
        var bodyHashBase64 = Convert.ToBase64String(bodyHashBytes);

        var dkimHeaderWithoutSig = $"v=1; a=rsa-sha256; d={domain}; s={selector}; c=relaxed/relaxed; q=dns/txt; h=from:to:subject:date; bh={bodyHashBase64}; b=";
        var canonicalHeaders = CanonicalizeHeadersRelaxed(headers, "from:to:subject:date");
        var dkimHeaderCanonical = $"dkim-signature:{dkimHeaderWithoutSig}";

        var dataToSign = canonicalHeaders + dkimHeaderCanonical;

        using var rsa = RSA.Create();
        ImportPrivateKey(rsa, privateKeyPem);

        var signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(dataToSign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        var fullDkimHeader = $"DKIM-Signature: {dkimHeaderWithoutSig}{signatureBase64}\r\n";
        return fullDkimHeader + rawMessage;
    }

    public bool VerifyMessage(string rawSignedMessage, string publicKeyBase64)
    {
        try
        {
            var (headers, body) = SplitMessage(rawSignedMessage);
            var dkimHeaderLine = headers.FirstOrDefault(h => h.StartsWith("DKIM-Signature:", StringComparison.OrdinalIgnoreCase));
            if (dkimHeaderLine == null)
            {
                return false;
            }

            var dkimValue = DkimSignatureHeaderRegex().Replace(dkimHeaderLine, "").Trim();
            var tags = ParseTags(dkimValue);

            if (!tags.TryGetValue("b", out var signatureBase64) ||
                !tags.TryGetValue("bh", out var expectedBh) ||
                !tags.TryGetValue("h", out var signedHeadersList))
            {
                return false;
            }

            // Verify body hash
            var canonicalBody = CanonicalizeBodyRelaxed(body);
            var actualBhBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalBody));
            var actualBh = Convert.ToBase64String(actualBhBytes);

            if (actualBh != expectedBh)
            {
                return false;
            }

            // Reconstruct dkim header without signature
            int bIdx = dkimHeaderLine.IndexOf("b=", StringComparison.OrdinalIgnoreCase);
            if (bIdx < 0) return false;
            var headerWithoutSigValue = dkimHeaderLine[..(bIdx + 2)];
            int colon = headerWithoutSigValue.IndexOf(':');
            var dkimValueWithoutSig = MultiSpaceRegex().Replace(headerWithoutSigValue[(colon + 1)..].Trim(), " ");
            var canonicalDkimHeader = $"dkim-signature:{dkimValueWithoutSig}";

            var canonicalHeaders = CanonicalizeHeadersRelaxed(headers, signedHeadersList);
            var dataToVerify = canonicalHeaders + canonicalDkimHeader;

            using var rsa = RSA.Create();
            var pubBytes = Convert.FromBase64String(publicKeyBase64);
            rsa.ImportSubjectPublicKeyInfo(pubBytes, out _);

            var sigBytes = Convert.FromBase64String(signatureBase64);
            return rsa.VerifyData(Encoding.UTF8.GetBytes(dataToVerify), sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private static (List<string> Headers, string Body) SplitMessage(string rawMessage)
    {
        var lines = NewLineRegex().Split(rawMessage);
        var headers = new List<string>();
        int i = 0;
        for (; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
                break;
            }
            headers.Add(lines[i]);
        }

        var body = string.Join("\r\n", lines.Skip(i));
        return (headers, body);
    }

    private static string CanonicalizeBodyRelaxed(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return "\r\n";
        }

        var lines = NewLineRegex().Split(body);
        var canonicalLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = MultiSpaceRegex().Replace(line, " ").TrimEnd();
            canonicalLines.Add(trimmed);
        }

        // Remove empty lines at the end
        while (canonicalLines.Count > 0 && string.IsNullOrEmpty(canonicalLines[^1]))
        {
            canonicalLines.RemoveAt(canonicalLines.Count - 1);
        }

        return canonicalLines.Count == 0 ? "\r\n" : string.Join("\r\n", canonicalLines) + "\r\n";
    }

    private static string CanonicalizeHeadersRelaxed(List<string> headers, string headerList)
    {
        var targetHeaders = headerList.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new StringBuilder();

        foreach (var headerName in targetHeaders)
        {
            var match = headers.FirstOrDefault(h => h.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                int colon = match.IndexOf(':');
                var name = match[..colon].Trim().ToLowerInvariant();
                var value = MultiSpaceRegex().Replace(match[(colon + 1)..].Trim(), " ");
                sb.Append($"{name}:{value}\r\n");
            }
        }

        return sb.ToString();
    }

    private static Dictionary<string, string> ParseTags(string dkimValue)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = dkimValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
            {
                tags[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }
        }
        return tags;
    }

    private static void ImportPrivateKey(RSA rsa, string pem)
    {
        var rawBase64 = pem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();
        var keyBytes = Convert.FromBase64String(rawBase64);
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);
    }
}

public static class DnsAuthHelper
{
    public static string GenerateDkimDnsRecord(string publicKeyBase64)
    {
        return $"v=DKIM1; k=rsa; p={publicKeyBase64}";
    }

    public static string GenerateSpfRecord(string[] allowedIps, string[]? includeDomains = null)
    {
        var parts = new List<string> { "v=spf1" };
        foreach (var ip in allowedIps)
        {
            parts.Add($"ip4:{ip}");
        }
        if (includeDomains != null)
        {
            foreach (var inc in includeDomains)
            {
                parts.Add($"include:{inc}");
            }
        }
        parts.Add("~all");
        return string.Join(" ", parts);
    }

    public static string GenerateDmarcRecord(string ruaEmail, string policy = "quarantine")
    {
        return $"v=DMARC1; p={policy}; rua=mailto:{ruaEmail}; adkim=s; aspf=s";
    }
}
