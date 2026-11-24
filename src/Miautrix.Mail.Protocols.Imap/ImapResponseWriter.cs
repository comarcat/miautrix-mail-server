using System.Text;

namespace Miautrix.Mail.Protocols.Imap;

public static class ImapResponseWriter
{
    public static byte[] Serialize(ImapCommandResult result)
    {
        using var buffer = new MemoryStream();
        foreach (var segment in result.ResponseSegments) buffer.Write(segment.Bytes);
        if (result.Status == "CONTINUE") return buffer.ToArray();
        buffer.Write(Encoding.UTF8.GetBytes($"{result.Tag} {result.Status} {result.Message}\r\n"));
        return buffer.ToArray();
    }

    public static async Task WriteAsync(Stream stream, ImapCommandResult result, CancellationToken cancellationToken = default)
    {
        foreach (var segment in result.ResponseSegments)
            await stream.WriteAsync(segment.Bytes.AsMemory(), cancellationToken);
        if (result.Status == "CONTINUE") return;
        await stream.WriteAsync(Encoding.UTF8.GetBytes($"{result.Tag} {result.Status} {result.Message}\r\n").AsMemory(), cancellationToken);
    }
}
