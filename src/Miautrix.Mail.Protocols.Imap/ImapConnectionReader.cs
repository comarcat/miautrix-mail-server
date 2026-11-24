namespace Miautrix.Mail.Protocols.Imap;

public sealed class ImapLineTooLongException(byte[] partialLine) : Exception("IMAP command line exceeded maximum length.")
{
    public byte[] PartialLine { get; } = partialLine;
}

/// <summary>Buffered byte reader shared by IMAP command lines and literal payloads.</summary>
public sealed class ImapConnectionReader
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private readonly int _maxLineBytes;
    private readonly int _maxLiteralBytes;
    private int _offset;
    private int _count;

    public ImapConnectionReader(Stream stream, int maxLineBytes = 64 * 1024, int maxLiteralBytes = 50 * 1024 * 1024, int bufferSize = 8192)
    {
        if (maxLineBytes <= 0 || maxLiteralBytes < 0 || bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
        _stream = stream;
        _maxLineBytes = maxLineBytes;
        _maxLiteralBytes = maxLiteralBytes;
        _buffer = new byte[bufferSize];
    }

    public async Task<byte[]?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        using var line = new MemoryStream();
        while (true)
        {
            if (_offset == _count && !await FillAsync(cancellationToken))
                return line.Length == 0 ? null : line.ToArray();

            var newline = Array.IndexOf(_buffer, (byte)'\n', _offset, _count - _offset);
            var take = newline < 0 ? _count - _offset : newline - _offset + 1;
            if (line.Length + take > _maxLineBytes)
            {
                line.Write(_buffer, _offset, Math.Max(0, _maxLineBytes - (int)line.Length));
                throw new ImapLineTooLongException(line.ToArray());
            }
            line.Write(_buffer, _offset, take);
            _offset += take;
            if (newline < 0) continue;

            var result = line.ToArray();
            var length = result.Length;
            if (length > 0 && result[length - 1] == (byte)'\n') length--;
            if (length > 0 && result[length - 1] == (byte)'\r') length--;
            return result.AsSpan(0, length).ToArray();
        }
    }

    public async Task<byte[]> ReadLiteralAsync(int length, CancellationToken cancellationToken = default)
    {
        if (length < 0 || length > _maxLiteralBytes) throw new InvalidDataException("IMAP literal exceeds configured limit.");
        var result = new byte[length];
        var written = 0;
        while (written < length)
        {
            if (_offset == _count && !await FillAsync(cancellationToken)) throw new EndOfStreamException("Incomplete IMAP literal.");
            var take = Math.Min(length - written, _count - _offset);
            Buffer.BlockCopy(_buffer, _offset, result, written, take);
            written += take;
            _offset += take;
        }
        return result;
    }

    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        _offset = 0;
        _count = await _stream.ReadAsync(_buffer.AsMemory(), cancellationToken);
        return _count > 0;
    }
}
