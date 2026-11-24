namespace Miautrix.Mail.Protocols.Imap;

public sealed class SequenceSet
{
    private readonly IReadOnlyList<(uint First, uint? Last)> _ranges;

    private SequenceSet(IReadOnlyList<(uint First, uint? Last)> ranges) => _ranges = ranges;

    public static bool TryParse(string value, out SequenceSet? sequenceSet)
    {
        sequenceSet = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var ranges = new List<(uint, uint?)>();
        foreach (var item in value.Split(','))
        {
            var endpoints = item.Split(':');
            if (endpoints.Length is < 1 or > 2 || !TryEndpoint(endpoints[0], out var first)) return false;
            uint? last = null;
            if (endpoints.Length == 2)
            {
                if (!TryEndpoint(endpoints[1], out var parsedLast)) return false;
                last = parsedLast;
            }
            ranges.Add((first, last));
        }
        sequenceSet = new SequenceSet(ranges);
        return true;
    }

    public IReadOnlyList<uint> Resolve(uint maximum)
    {
        var values = new SortedSet<uint>();
        foreach (var (first, last) in _ranges)
        {
            var start = first == 0 ? maximum : first;
            var end = !last.HasValue ? start : last.Value == 0 ? maximum : last.Value;
            if (start == 0 || end == 0) continue;
            var low = Math.Min(start, end);
            var high = Math.Max(start, end);
            for (var value = low; value <= high; value++)
            {
                values.Add(value);
                if (value == uint.MaxValue) break;
            }
        }
        return values.ToArray();
    }

    private static bool TryEndpoint(string value, out uint endpoint)
    {
        if (value == "*") { endpoint = 0; return true; }
        return uint.TryParse(value, out endpoint) && endpoint > 0;
    }
}
