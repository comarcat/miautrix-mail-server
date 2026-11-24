using System.Text;

namespace Miautrix.Mail.Protocols.Imap;

/// <summary>Modified UTF-7 mailbox names from RFC 3501, used until UTF8=ACCEPT is enabled.</summary>
internal static class ImapMailboxNameCodec
{
    public static string Encode(string value)
    {
        var output = new StringBuilder(value.Length);
        var encoded = new StringBuilder();
        void FlushEncoded()
        {
            if (encoded.Length == 0) return;
            var bytes = Encoding.BigEndianUnicode.GetBytes(encoded.ToString());
            output.Append('&').Append(Convert.ToBase64String(bytes).TrimEnd('=').Replace('/', ',')).Append('-');
            encoded.Clear();
        }

        foreach (var ch in value)
        {
            if (ch is >= '\x20' and <= '\x7e')
            {
                FlushEncoded();
                if (ch == '&') output.Append("&-");
                else output.Append(ch);
            }
            else
            {
                encoded.Append(ch);
            }
        }
        FlushEncoded();
        return output.ToString();
    }

    public static string Decode(string value)
    {
        var output = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length;)
        {
            if (value[index] != '&')
            {
                if (value[index] > '\x7e' || value[index] < '\x20')
                    throw new FormatException("Mailbox names must use modified UTF-7 until UTF8=ACCEPT is enabled.");
                output.Append(value[index++]);
                continue;
            }

            var terminator = value.IndexOf('-', index + 1);
            if (terminator < 0) throw new FormatException("Invalid modified UTF-7 mailbox name.");
            var payload = value[(index + 1)..terminator];
            if (payload.Length == 0)
            {
                output.Append('&');
            }
            else
            {
                var base64 = payload.Replace(',', '/');
                base64 += new string('=', (4 - base64.Length % 4) % 4);
                output.Append(Encoding.BigEndianUnicode.GetString(Convert.FromBase64String(base64)));
            }
            index = terminator + 1;
        }
        return output.ToString();
    }
}
