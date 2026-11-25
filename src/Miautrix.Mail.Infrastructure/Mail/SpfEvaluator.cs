using System.Net;
using System.Net.Sockets;
using System.Text;
using Miautrix.Mail.Application.Transport;

namespace Miautrix.Mail.Infrastructure.Mail;

/// <summary>RFC 7208 evaluator. One instance is used per message so DNS limits are message scoped.</summary>
public sealed class SpfEvaluator(IDnsResolver dns)
{
    private sealed class State(IDnsResolver resolver, IPAddress ip, string sender, string helo, CancellationToken token)
    {
        public readonly IDnsResolver Dns = resolver;
        public readonly IPAddress Ip = ip;
        public readonly string Sender = sender;
        public readonly string Helo = helo;
        public readonly CancellationToken Token = token;
        public int Lookups;
        public int Voids;
    }

    public async Task<SpfAuthenticationResult> EvaluateAsync(IPAddress? ip, string? helo, string? mailFrom, CancellationToken cancellationToken = default)
    {
        var nullPath = string.IsNullOrWhiteSpace(mailFrom) || mailFrom == "<>";
        var identity = nullPath ? helo : mailFrom;
        if (string.IsNullOrWhiteSpace(identity)) return new("none", null);
        var domain = identity.Contains('@') ? identity[(identity.LastIndexOf('@') + 1)..] : identity;
        domain = domain.Trim('<', '>').TrimEnd('.').ToLowerInvariant();
        if (domain.Length == 0 || !domain.Contains('.')) return new("permerror", domain, "invalid SPF identity domain");
        if (ip is null) return new("none", domain, "client IP unavailable");
        var state = new State(dns, ip, nullPath ? $"postmaster@{domain}" : identity!, helo ?? domain, cancellationToken);
        try
        {
            var result = await EvalDomain(state, domain, 0);
            return new(result, domain, result == "permerror" ? "invalid SPF record or DNS lookup limit exceeded" : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new("temperror", domain, "DNS lookup timeout"); }
        catch (TimeoutException) { return new("temperror", domain, "DNS lookup timeout"); }
        catch (FormatException ex) { return new("permerror", domain, ex.Message); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new("temperror", domain, ex.GetType().Name); }
    }

    private static async Task<string> EvalDomain(State s, string domain, int depth)
    {
        if (depth > 10) return "permerror";
        IReadOnlyList<string> txt;
        try { txt = await s.Dns.GetTxtAsync(domain, s.Token); }
        catch (OperationCanceledException) { throw; }
        catch { return "temperror"; }
        var records = txt.Where(x => IsSpfRecord(x)).ToArray();
        if (records.Length == 0) { s.Voids++; return CheckLimits(s) ? "permerror" : "none"; }
        if (records.Length != 1) return "permerror";
        var tokens = records[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || !tokens[0].Equals("v=spf1", StringComparison.OrdinalIgnoreCase)) return "permerror";
        string? redirect = null;
        foreach (var raw in tokens.Skip(1))
        {
            if (raw.StartsWith("exp=", StringComparison.OrdinalIgnoreCase)) continue;
            if (raw.StartsWith("redirect=", StringComparison.OrdinalIgnoreCase)) { if (redirect is not null) return "permerror"; redirect = raw[9..]; continue; }
            if (raw.Contains('=')) return "permerror"; // unknown modifier is ignored only when syntactically valid
            var qualifier = raw[0] is '+' or '-' or '~' or '?' ? raw[0] : '+';
            var mechanism = qualifier == '+' && raw[0] is not '+' ? raw : raw[1..];
            var outcome = await EvalMechanism(s, domain, mechanism, depth);
            if (outcome is "temperror" or "permerror") return outcome;
            if (outcome == "pass") return qualifier switch { '-' => "fail", '~' => "softfail", '?' => "neutral", _ => "pass" };
        }
        if (redirect is not null)
        {
            if (!CountLookup(s)) return "permerror";
            var target = await ExpandAsync(redirect, s, domain);
            var redirected = await EvalDomain(s, target, depth + 1);
            return redirected == "none" ? "permerror" : redirected;
        }
        return "neutral";
    }

    private static async Task<string> EvalMechanism(State s, string domain, string mech, int depth)
    {
        if (mech.Equals("all", StringComparison.OrdinalIgnoreCase)) return "pass";
        if (mech.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase))
        {
            var ipv6 = mech.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase);
            var parts = mech[(ipv6 ? 4 : 4)..].Split('/');
            if (!IPAddress.TryParse(parts[0], out var network) || (ipv6 ? network.AddressFamily != AddressFamily.InterNetworkV6 : network.AddressFamily != AddressFamily.InterNetwork)) return "permerror";
            var max = ipv6 ? 128 : 32;
            var prefix = max;
            if (parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > max))) return "permerror";
            return InNetwork(s.Ip, network, prefix) ? "pass" : "nomatch";
        }
        if (mech.StartsWith("include:", StringComparison.OrdinalIgnoreCase))
        {
            if (!CountLookup(s)) return "permerror";
            var target = await ExpandAsync(mech[8..], s, domain);
            var result = await EvalDomain(s, target, depth + 1);
            return result switch { "pass" => "pass", "fail" or "softfail" or "neutral" => "nomatch", "none" => "permerror", _ => result };
        }
        if (mech.StartsWith("exists:", StringComparison.OrdinalIgnoreCase))
        {
            if (!CountLookup(s)) return "permerror";
            var name = await ExpandAsync(mech[7..], s, domain);
            var ips = await s.Dns.GetAAsync(name, s.Token);
            if (ips.Count == 0 && !Void(s)) return "permerror";
            return ips.Count > 0 ? "pass" : "nomatch";
        }
        var isA = mech.Equals("a", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("a:", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("a/", StringComparison.OrdinalIgnoreCase);
        var isMx = mech.Equals("mx", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("mx:", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("mx/", StringComparison.OrdinalIgnoreCase);
        var isPtr = mech.Equals("ptr", StringComparison.OrdinalIgnoreCase) || mech.StartsWith("ptr:", StringComparison.OrdinalIgnoreCase);
        if (!isA && !isMx && !isPtr) return "permerror";
        if (!CountLookup(s)) return "permerror";
        var arg = mech[(isA ? 1 : isMx ? 2 : 3)..];
        var segments = arg.Split('/', StringSplitOptions.None);
        var host = segments[0].StartsWith(':') ? await ExpandAsync(segments[0][1..], s, domain) : string.IsNullOrEmpty(segments[0]) ? domain : await ExpandAsync(segments[0], s, domain);
        var cidr4 = 32; var cidr6 = 128;
        var slash = mech.IndexOf('/');
        if (slash >= 0)
        {
            var cidrs = mech[(slash + 1)..].Split('/');
            if (!int.TryParse(cidrs[0], out cidr4) || cidr4 is < 0 or > 32) return "permerror";
            if (cidrs.Length == 2 && (!int.TryParse(cidrs[1], out cidr6) || cidr6 is < 0 or > 128)) return "permerror";
        }
        var addresses = new List<IPAddress>();
        if (isA) addresses.AddRange(s.Ip.AddressFamily == AddressFamily.InterNetwork
            ? await s.Dns.GetAAsync(host, s.Token)
            : await s.Dns.GetAaaaAsync(host, s.Token));
        else if (isMx)
        {
            var mx = await s.Dns.GetMxAsync(host, s.Token);
            if (mx.Count == 0 && !Void(s)) return "permerror";
            if (mx.Count > 10) return "permerror";
            foreach (var server in mx)
            {
                var hostAddresses = s.Ip.AddressFamily == AddressFamily.InterNetwork
                    ? await s.Dns.GetAAsync(server.Host, s.Token)
                    : await s.Dns.GetAaaaAsync(server.Host, s.Token);
                if (hostAddresses.Count == 0 && !Void(s)) return "permerror";
                addresses.AddRange(hostAddresses);
            }
        }
        else
        {
            var ptrs = await s.Dns.GetPtrAsync(s.Ip, s.Token);
            if (ptrs.Count == 0 && !Void(s)) return "permerror";
            foreach (var ptr in ptrs.Take(10))
                if ((ptr.Equals(host, StringComparison.OrdinalIgnoreCase) || ptr.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)) &&
                    (s.Ip.AddressFamily == AddressFamily.InterNetwork ? await s.Dns.GetAAsync(ptr, s.Token) : await s.Dns.GetAaaaAsync(ptr, s.Token)).Contains(s.Ip)) return "pass";
        }
        if (addresses.Count == 0 && !isPtr && !isMx && !Void(s)) return "permerror";
        return addresses.Any(ip => InNetwork(s.Ip, ip, ip.AddressFamily == AddressFamily.InterNetwork ? cidr4 : cidr6)) ? "pass" : "nomatch";
    }

    private static bool CountLookup(State s) { s.Lookups++; return s.Lookups <= 10; }
    private static bool Void(State s) { s.Voids++; return s.Voids <= 2; }
    private static bool CheckLimits(State s) => s.Lookups > 10 || s.Voids > 2;
    private static bool IsSpfRecord(string x) => x.Equals("v=spf1", StringComparison.OrdinalIgnoreCase) || x.StartsWith("v=spf1 ", StringComparison.OrdinalIgnoreCase) || x.StartsWith("v=spf1\t", StringComparison.OrdinalIgnoreCase);
    private static bool InNetwork(IPAddress ip, IPAddress network, int prefix)
    {
        var a = ip.GetAddressBytes(); var b = network.GetAddressBytes(); if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++) { var bits = Math.Clamp(prefix - i * 8, 0, 8); var mask = bits == 0 ? 0 : 0xff << (8 - bits); if ((a[i] & mask) != (b[i] & mask)) return false; }
        return true;
    }

    private static async Task<string> ExpandAsync(string input, State s, string domain)
    {
        var output = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '%') { output.Append(input[i]); continue; }
            if (++i >= input.Length) throw new FormatException("incomplete SPF macro");
            if (input[i] == '%') output.Append('%'); else if (input[i] == '_') output.Append(' '); else if (input[i] == '-') output.Append("%20");
            else if (input[i] == '{')
            {
                var end = input.IndexOf('}', i + 1); if (end < 0) throw new FormatException("unterminated SPF macro");
                output.Append(await MacroAsync(input[(i + 1)..end], s, domain)); i = end;
            }
            else throw new FormatException("invalid SPF macro");
        }
        return output.ToString().TrimEnd('.').ToLowerInvariant();
    }

    private static async Task<string> MacroAsync(string body, State s, string domain)
    {
        if (body.Length == 0) throw new FormatException("empty SPF macro");
        var p = 1;
        while (p < body.Length && char.IsDigit(body[p])) p++;
        var count = p == 1 ? int.MaxValue : int.Parse(body[1..p]);
        var reverse = p < body.Length && body[p] is 'r' or 'R'; if (reverse) p++;
        var delimiters = p == body.Length ? ".-+" : body[p..];
        var code = body.Length > 0 ? char.ToLowerInvariant(body[0]) : throw new FormatException();
        var value = code switch
        {
            's' => s.Sender, 'l' => s.Sender.Split('@')[0], 'o' => s.Sender.Contains('@') ? s.Sender[(s.Sender.LastIndexOf('@') + 1)..] : domain,
            'd' => domain, 'i' => FormatIp(s.Ip), 'p' => await ValidatedPtrName(s, domain), 'v' => s.Ip.AddressFamily == AddressFamily.InterNetworkV6 ? "ip6" : "in-addr", 'h' => s.Helo,
            'c' => s.Ip.ToString(), 'r' => "unknown", 't' => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), _ => throw new FormatException("unknown SPF macro")
        };
        var pieces = value.Split(delimiters.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        if (count != int.MaxValue) pieces = pieces.TakeLast(count).ToArray();
        if (reverse) Array.Reverse(pieces);
        var transformed = string.Join('.', pieces);
        return body.Length > 0 && char.IsUpper(body[0]) ? Uri.EscapeDataString(transformed) : transformed;
    }

    private static string FormatIp(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
        var hex = Convert.ToHexString(ip.GetAddressBytes()).ToLowerInvariant();
        return string.Join('.', hex.Reverse());
    }

    private static async Task<string> ValidatedPtrName(State s, string currentDomain)
    {
        if (!CountLookup(s)) throw new FormatException("SPF lookup limit exceeded during p macro expansion.");
        var ptrs = await s.Dns.GetPtrAsync(s.Ip, s.Token);
        if (ptrs.Count == 0) { if (!Void(s)) throw new FormatException("SPF void lookup limit exceeded."); return "unknown"; }
        var valid = new List<string>();
        foreach (var ptr in ptrs.Take(10))
        {
            var addresses = s.Ip.AddressFamily == AddressFamily.InterNetwork
                ? await s.Dns.GetAAsync(ptr, s.Token)
                : await s.Dns.GetAaaaAsync(ptr, s.Token);
            if (addresses.Contains(s.Ip)) valid.Add(ptr.TrimEnd('.').ToLowerInvariant());
        }
        return valid.FirstOrDefault(name => name.Equals(currentDomain, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + currentDomain, StringComparison.OrdinalIgnoreCase))
            ?? valid.FirstOrDefault(name => name.Equals(s.Helo, StringComparison.OrdinalIgnoreCase) || name.EndsWith("." + s.Helo, StringComparison.OrdinalIgnoreCase))
            ?? valid.FirstOrDefault()
            ?? "unknown";
    }
}
