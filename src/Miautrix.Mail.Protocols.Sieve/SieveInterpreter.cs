using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Miautrix.Mail.Protocols.Sieve;

public sealed record SieveMessageContext(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Headers,
    string EnvelopeFrom,
    string EnvelopeTo,
    long SizeBytes,
    string? BodyText = null,
    IReadOnlySet<string>? ExistingMailboxes = null);

public sealed record SieveEffect(string Name, IReadOnlyList<string> Values, IReadOnlyList<string> Flags, IReadOnlyList<string> Tags, IReadOnlyList<IReadOnlyList<string>>? ArgumentGroups = null);
public sealed record SieveExecutionResult(IReadOnlyList<SieveEffect> Effects, bool RuntimeError, bool TimedOut, int Steps);

/// <summary>Evaluates compiled Sieve syntax with bounded steps and wall time.</summary>
public sealed class SieveInterpreter
{
    private const int DefaultStepLimit = 10_000;
    private static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromMilliseconds(100);
    private int _steps;
    private int _stepLimit;
    private Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _timeLimit;
    private SieveMessageContext _context = null!;
    private readonly List<SieveEffect> _effects = [];

    public SieveExecutionResult Execute(SieveProgram program, SieveMessageContext context, int stepLimit = DefaultStepLimit, TimeSpan? timeLimit = null)
    {
        _steps = 0; _stepLimit = Math.Clamp(stepLimit, 1, 1_000_000); _timeLimit = timeLimit ?? DefaultTimeLimit;
        _clock = Stopwatch.StartNew(); _context = context; _effects.Clear();
        var timedOut = false; var failed = false;
        try { Run(program.Statements); }
        catch (LimitException ex) { timedOut = ex.TimedOut; failed = true; }
        catch (Exception) { failed = true; }
        if (failed) return new SieveExecutionResult([new SieveEffect("keep", [], [], [])], true, timedOut, _steps);
        if (!_effects.Any(x => x.Name is "keep" or "fileinto" or "redirect" or "discard" or "reject" or "ereject"))
            _effects.Add(new SieveEffect("keep", [], [], []));
        return new SieveExecutionResult(_effects.ToArray(), false, false, _steps);
    }

    private void Run(IReadOnlyList<SieveStatement> statements)
    {
        foreach (var statement in statements)
        {
            Tick();
            switch (statement)
            {
                case SieveAction action:
                    ExecuteAction(action);
                    if (action.Name == "stop") return;
                    break;
                case SieveConditional conditional:
                    var matched = false;
                    foreach (var branch in conditional.Branches)
                    {
                        Tick();
                        if (!Evaluate(branch.Test)) continue;
                        Run(branch.Body); matched = true; break;
                    }
                    if (!matched && conditional.ElseBody is not null) Run(conditional.ElseBody);
                    break;
                default: throw new InvalidOperationException("Invalid compiled statement.");
            }
        }
    }

    private bool Evaluate(SieveTest test)
    {
        Tick();
        if (test.Name == "not") return !Evaluate(test.Children![0]);
        if (test.Name == "allof") return test.Children!.All(Evaluate);
        if (test.Name == "anyof") return test.Children!.Any(Evaluate);
        if (test.Name == "true") return true;
        if (test.Name == "false") return false;
        if (test.Name is "size")
        {
            var amount = test.Arguments.FirstOrDefault()?.Number ?? throw new InvalidOperationException("size requires a numeric operand");
            return test.Tags.Contains(":over") ? _context.SizeBytes > amount : test.Tags.Contains(":under") && _context.SizeBytes < amount;
        }
        if (test.Name is "exists" or "mailboxexists")
        {
            var values = test.Arguments.FirstOrDefault()?.Strings ?? [];
            return values.All(name => test.Name == "mailboxexists"
                ? _context.ExistingMailboxes?.Contains(name) == true
                : _context.Headers.Keys.Any(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
        if (test.Name is not ("header" or "address" or "envelope")) throw new InvalidOperationException("Unsupported test.");

        if (test.Arguments.Count < 2) throw new InvalidOperationException($"{test.Name} requires header/address and key operands");
        var names = test.Arguments[0].Strings;
        var keys = test.Arguments[1].Strings;
        if (names.Count == 0 || keys.Count == 0) throw new InvalidOperationException($"{test.Name} operands cannot be empty");
        var actual = new List<string>();
        foreach (var name in names)
        {
            if (test.Name == "envelope")
            {
                actual.Add(name.Equals("from", StringComparison.OrdinalIgnoreCase) ? _context.EnvelopeFrom : name.Equals("to", StringComparison.OrdinalIgnoreCase) ? _context.EnvelopeTo : "");
            }
            else if (_context.Headers.TryGetValue(name, out var found)) actual.AddRange(found);
        }
        if (test.Name == "address") actual = actual.SelectMany(x => ExtractAddresses(x, test.Tags)).ToList();
        var comparator = test.Tags.FirstOrDefault(x => x.StartsWith(":comparator=", StringComparison.Ordinal));
        var octet = comparator?.EndsWith("i;octet", StringComparison.OrdinalIgnoreCase) == true;
        var mode = test.Tags.Contains(":contains") ? "contains" : test.Tags.Contains(":matches") ? "matches" : "is";
        return actual.Any(a => keys.Any(key => Match(a, key, mode, octet)));
    }

    private void ExecuteAction(SieveAction action)
    {
        if (action.Name == "stop") return;
        var groups = action.Arguments.Select(x => x.Strings).ToArray();
        var values = groups.SelectMany(x => x).ToArray();
        var flags = action.Tags.Contains(":flags") && groups.Length > 0 ? groups[0].ToArray() : [];
        var payload = flags.Length == 0 ? values : groups.Skip(1).SelectMany(x => x).ToArray();
        if (action.Name is "setflag" or "addflag" or "removeflag")
        {
            _effects.Add(new SieveEffect(action.Name, [], values, action.Tags, action.Arguments.Select(x => x.Strings).ToArray())); return;
        }
        if (action.Name is "keep" or "fileinto" or "redirect" or "discard" or "reject" or "ereject" or "vacation")
        {
            if (action.Name == "discard" || action.Name == "keep") payload = [];
            _effects.Add(new SieveEffect(action.Name, payload, flags, action.Tags, action.Arguments.Select(x => x.Strings).ToArray()));
            if (action.Tags.Contains(":copy") && action.Name is "fileinto" or "redirect")
                _effects.Add(new SieveEffect("keep", [], [], []));
            return;
        }
        throw new InvalidOperationException("Unsupported action.");
    }

    private static IEnumerable<string> ExtractAddresses(string value, IReadOnlyList<string> tags)
    {
        foreach (Match match in Regex.Matches(value, @"[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase))
        {
            var address = match.Value;
            if (tags.Contains(":localpart")) yield return address[..address.LastIndexOf('@')];
            else if (tags.Contains(":domain")) yield return address[(address.LastIndexOf('@') + 1)..];
            else yield return address;
        }
    }

    private static bool Match(string actual, string key, string mode, bool octet)
    {
        var comparison = octet ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (mode == "is") return actual.Equals(key, comparison);
        if (mode == "contains") return actual.IndexOf(key, comparison) >= 0;
        var regexPattern = new System.Text.StringBuilder("^");
        for (var index = 0; index < key.Length; index++)
        {
            if (key[index] == '\\' && index + 1 < key.Length) regexPattern.Append(Regex.Escape(key[++index].ToString()));
            else if (key[index] == '*') regexPattern.Append(".*");
            else regexPattern.Append(Regex.Escape(key[index].ToString()));
        }
        regexPattern.Append('$');
        return Regex.IsMatch(actual, regexPattern.ToString(), RegexOptions.CultureInvariant | (octet ? RegexOptions.None : RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(25));
    }

    private void Tick()
    {
        if (++_steps > _stepLimit) throw new LimitException(false);
        if (_clock.Elapsed > _timeLimit) throw new LimitException(true);
    }
    private sealed class LimitException(bool timedOut) : Exception { public bool TimedOut { get; } = timedOut; }
}
