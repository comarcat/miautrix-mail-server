using System.Net.Mail;
using System.Text;

namespace Miautrix.Mail.Application.Mail.Mime;

internal static class MimeEncoders
{
    private const int MaxHeaderLineBytes = 78;
    private const int MaxMimeLineBytes = 76;

    public static string NormalizeToCrlf(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    public static string FormatAddress(string raw)
    {
        RejectHeaderInjection(raw, "address");
        return new MailAddress(raw.Trim()).ToString();
    }

    public static string FormatAddressList(IEnumerable<string> addresses) =>
        string.Join(", ", addresses.Select(FormatAddress));

    public static void RejectHeaderInjection(string? value, string fieldName)
    {
        if (value?.IndexOfAny(['\r', '\n']) >= 0)
        {
            throw new ArgumentException($"{fieldName} cannot contain line breaks.");
        }
    }

    public static string Header(string name, string value) => FoldHeader(name, EncodeHeaderValue(value));

    public static string FoldHeader(string name, string value)
    {
        var prefix = name + ": ";
        var text = prefix + value;
        if (Encoding.UTF8.GetByteCount(text) <= MaxHeaderLineBytes)
        {
            return text;
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = prefix.TrimEnd();
        foreach (var word in words)
        {
            var separator = current.EndsWith(":", StringComparison.Ordinal) ? " " : " ";
            var candidate = current + separator + word;
            if (Encoding.UTF8.GetByteCount(candidate) > MaxHeaderLineBytes && current != prefix.TrimEnd())
            {
                lines.Add(current);
                current = " " + word;
            }
            else
            {
                current = candidate;
            }
        }

        lines.Add(current);
        return string.Join("\r\n", lines);
    }

    public static string EncodeHeaderValue(string value)
    {
        RejectHeaderInjection(value, "header");
        if (value.All(c => c <= 127))
        {
            return value;
        }

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return $"=?UTF-8?B?{base64}?=";
    }

    public static string ContentDispositionFilename(string fileName)
    {
        var safe = string.IsNullOrWhiteSpace(fileName) ? "attachment" : Path.GetFileName(fileName.Trim());
        RejectHeaderInjection(safe, "filename");
        if (safe.All(c => c is >= '!' and <= '~' && c != '"' && c != '\\'))
        {
            return $"filename=\"{safe}\"";
        }

        return $"filename*=UTF-8''{Uri.EscapeDataString(safe)}";
    }

    public static string Base64Block(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        if (base64.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(base64.Length + base64.Length / MaxMimeLineBytes * 2);
        for (var i = 0; i < base64.Length; i += MaxMimeLineBytes)
        {
            if (i > 0)
            {
                builder.Append("\r\n");
            }

            builder.Append(base64, i, Math.Min(MaxMimeLineBytes, base64.Length - i));
        }

        return builder.ToString();
    }

    public static string EncodeTextPart(string value, out string transferEncoding)
    {
        var normalized = NormalizeToCrlf(value);
        if (CanUseSevenBit(normalized))
        {
            transferEncoding = "7bit";
            return normalized;
        }

        transferEncoding = "quoted-printable";
        return QuotedPrintable(Encoding.UTF8.GetBytes(normalized));
    }

    private static bool CanUseSevenBit(string value) =>
        value.All(c => c is '\r' or '\n' or '\t' || c is >= ' ' and <= '~') &&
        value.Split("\r\n", StringSplitOptions.None).All(line => Encoding.ASCII.GetByteCount(line) <= MaxHeaderLineBytes);

    private static string QuotedPrintable(byte[] bytes)
    {
        var builder = new StringBuilder();
        var lineLength = 0;
        foreach (var b in bytes)
        {
            string encoded;
            if (b == (byte)'\r')
            {
                continue;
            }

            if (b == (byte)'\n')
            {
                builder.Append("\r\n");
                lineLength = 0;
                continue;
            }

            if (b is >= 33 and <= 60 or >= 62 and <= 126 || b is (byte)' ' or (byte)'\t')
            {
                encoded = ((char)b).ToString();
            }
            else
            {
                encoded = "=" + b.ToString("X2");
            }

            if (lineLength + encoded.Length > 75)
            {
                builder.Append("=\r\n");
                lineLength = 0;
            }

            builder.Append(encoded);
            lineLength += encoded.Length;
        }

        return builder.ToString();
    }
}
