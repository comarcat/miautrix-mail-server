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
        var address = new MailAddress(raw.Trim());
        if (address.DisplayName.Any(c => c > 127))
            return EncodeHeaderValue(address.DisplayName) + " <" + address.Address + ">";
        return address.ToString();
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

    public static string Header(string name, string value) => FoldHeader(name, value);

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
            if (Encoding.UTF8.GetByteCount(candidate) > MaxHeaderLineBytes)
            {
                if (current != prefix.TrimEnd() || lines.Count == 0) lines.Add(current);
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
        if (value.All(c => c <= 127) && value.Split(' ').All(word => Encoding.UTF8.GetByteCount(word) <= 60))
        {
            return value;
        }

        var words = new List<string>();
        var chunk = new StringBuilder();
        var chunkBytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var runeBytes = Encoding.UTF8.GetByteCount(rune.ToString());
            if (chunkBytes + runeBytes > 45 && chunk.Length > 0)
            {
                words.Add(ToEncodedWord(chunk.ToString()));
                chunk.Clear();
                chunkBytes = 0;
            }
            chunk.Append(rune.ToString());
            chunkBytes += runeBytes;
        }
        if (chunk.Length > 0) words.Add(ToEncodedWord(chunk.ToString()));
        return string.Join(" ", words);
    }

    public static IReadOnlyList<string> FilenameParameters(string parameterName, string fileName)
    {
        var safe = string.IsNullOrWhiteSpace(fileName) ? "attachment" : Path.GetFileName(fileName.Trim());
        RejectHeaderInjection(safe, "filename");
        if (safe.Length <= 40 && safe.All(c => c is >= '!' and <= '~' && c != '"' && c != '\\'))
        {
            return [$"{parameterName}=\"{safe}\""];
        }
        var escaped = Uri.EscapeDataString(safe);
        var segments = new List<string>();
        while (escaped.Length > 0)
        {
            var take = Math.Min(45, escaped.Length);
            while (take > 0 && take < escaped.Length && escaped[take - 1] == '%') take--;
            while (take > 1 && take < escaped.Length && escaped[take - 2] == '%') take -= 2;
            if (take == 0) take = Math.Min(3, escaped.Length);
            segments.Add(escaped[..take]);
            escaped = escaped[take..];
        }
        return segments.Select((part, index) => index == 0
            ? $"{parameterName}*0*=UTF-8''{part}"
            : $"{parameterName}*{index}*={part}").ToArray();
    }

    private static string ToEncodedWord(string value) => $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(value))}?=";

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
