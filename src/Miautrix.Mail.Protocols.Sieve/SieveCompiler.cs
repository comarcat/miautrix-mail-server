using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Miautrix.Mail.Protocols.Sieve;

public sealed record SieveValue(string? Text = null, long? Number = null, IReadOnlyList<SieveValue>? Items = null)
{
    public IReadOnlyList<string> Strings => Items is not null ? Items.SelectMany(x => x.Strings).ToArray() : Text is not null ? [Text] : Number is not null ? [Number.Value.ToString(CultureInfo.InvariantCulture)] : [];
}

public sealed record SieveTest(string Name, IReadOnlyList<SieveValue> Arguments, IReadOnlyList<string> Tags, IReadOnlyList<SieveTest>? Children = null);
public abstract record SieveStatement;
public sealed record SieveAction(string Name, IReadOnlyList<SieveValue> Arguments, IReadOnlyList<string> Tags) : SieveStatement;
public sealed record SieveConditional(IReadOnlyList<(SieveTest Test, IReadOnlyList<SieveStatement> Body)> Branches, IReadOnlyList<SieveStatement>? ElseBody) : SieveStatement;
public sealed record SieveProgram(IReadOnlyList<string> RequiredExtensions, IReadOnlyList<SieveStatement> Statements);
public sealed record SieveCompileResult(SieveProgram? Program, SieveParseResult Diagnostic)
{
    public bool Success => Program is not null && Diagnostic.Success;
}

/// <summary>Compiles RFC 5228 scripts to a small immutable syntax tree.</summary>
public sealed class SieveCompiler
{
    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "fileinto", "envelope", "imap4flags", "vacation", "reject", "ereject", "copy", "mailbox"
    };
    public SieveCompiler(SieveParser? validator = null) { }

    public SieveCompileResult Compile(string source)
    {
        try
        {
            var parser = new Parser(source);
            var program = parser.Parse();
            var unknown = program.RequiredExtensions.FirstOrDefault(x => !KnownExtensions.Contains(x));
            if (unknown is not null)
            {
                var line = source.Split('\n').Select((text, i) => (text, i)).FirstOrDefault(x => x.text.Contains(unknown, StringComparison.Ordinal)).i + 1;
                var text = source.Split('\n').ElementAtOrDefault(line - 1) ?? string.Empty;
                return new SieveCompileResult(null, new SieveParseResult(false, $"Unknown required extension '{unknown}'", line, text.IndexOf(unknown, StringComparison.Ordinal) + 1));
            }
            return new SieveCompileResult(program, new SieveParseResult(true));
        }
        catch (ParseException ex)
        {
            return new SieveCompileResult(null, new SieveParseResult(false, ex.Message, ex.Line, ex.Column));
        }
    }

    private enum TokenKind { Word, String, Number, Symbol, End }
    private sealed record Token(TokenKind Kind, string Value, int Line, int Column);
    private sealed class ParseException(string message, Token token) : Exception(message)
    {
        public int Line => token.Line;
        public int Column => token.Column;
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _position;
        private readonly List<string> _required = [];
        public Parser(string source) => _tokens = Lex(source);
        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
        private Token Take() { var value = Current; if (_position < _tokens.Count - 1) _position++; return value; }
        private bool Is(string value) => Current.Value.Equals(value, StringComparison.OrdinalIgnoreCase);
        private bool Accept(string value) { if (!Is(value)) return false; Take(); return true; }
        private Token Expect(string value) { if (Accept(value)) return _tokens[_position - 1]; throw Error($"Expected '{value}'."); }
        private ParseException Error(string message) => new(message, Current);

        public SieveProgram Parse()
        {
            var statements = ParseStatements(endAtBrace: false);
            return new SieveProgram(_required.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), statements);
        }

        private List<SieveStatement> ParseStatements(bool endAtBrace)
        {
            var result = new List<SieveStatement>();
            while (Current.Kind != TokenKind.End && !(endAtBrace && Is("}")))
            {
                if (Is("require"))
                {
                    Take();
                    _required.AddRange(ReadValue().Strings);
                    Expect(";");
                    continue;
                }
                if (Is("if")) { result.Add(ParseConditional()); continue; }
                var name = Take();
                if (name.Kind != TokenKind.Word) throw new ParseException("Expected a command.", name);
                if (!new[] { "fileinto", "redirect", "keep", "discard", "stop", "setflag", "addflag", "removeflag", "vacation", "reject", "ereject" }.Contains(name.Value, StringComparer.OrdinalIgnoreCase))
                    throw new ParseException($"Unknown Sieve command '{name.Value}'.", name);
                var (args, tags) = ReadArguments(until: ";");
                Expect(";");
                result.Add(new SieveAction(name.Value.ToLowerInvariant(), args, tags));
            }
            if (endAtBrace) Expect("}");
            return result;
        }

        private SieveConditional ParseConditional()
        {
            var branches = new List<(SieveTest, IReadOnlyList<SieveStatement>)>();
            Expect("if");
            var test = ParseTest();
            Expect("{");
            branches.Add((test, ParseStatements(true)));
            while (Accept("elsif"))
            {
                test = ParseTest(); Expect("{"); branches.Add((test, ParseStatements(true)));
            }
            IReadOnlyList<SieveStatement>? otherwise = null;
            if (Accept("else")) { Expect("{"); otherwise = ParseStatements(true); }
            return new SieveConditional(branches, otherwise);
        }

        private SieveTest ParseTest()
        {
            if (Accept("not")) return new SieveTest("not", [], [], [ParseTest()]);
            if (Is("allof") || Is("anyof"))
            {
                var name = Take().Value.ToLowerInvariant(); Expect("(");
                var children = new List<SieveTest> { ParseTest() };
                while (Accept(",")) children.Add(ParseTest());
                Expect(")"); return new SieveTest(name, [], [], children);
            }
            var testName = Take();
            if (testName.Kind != TokenKind.Word) throw new ParseException("Expected a test.", testName);
            if (!new[] { "header", "address", "envelope", "exists", "size", "true", "false", "mailboxexists", "hasflag" }.Contains(testName.Value, StringComparer.OrdinalIgnoreCase))
                throw new ParseException($"Unknown or unsupported Sieve test '{testName.Value}'.", testName);
            var (args, tags) = ReadArguments(until: "{", stopAtComma: true, stopAtParen: true);
            return new SieveTest(testName.Value.ToLowerInvariant(), args, tags);
        }

        private (List<SieveValue> Args, List<string> Tags) ReadArguments(string until, bool stopAtComma = false, bool stopAtParen = false)
        {
            var args = new List<SieveValue>(); var tags = new List<string>();
            while (Current.Kind != TokenKind.End && !Is(until) && !(stopAtComma && Is(",")) && !(stopAtParen && Is(")")) && !Is("{") && !Is("}"))
            {
                if (Current.Value.StartsWith(':'))
                {
                    var tag = Take().Value.ToLowerInvariant();
                    if (tag == ":comparator") tags.Add(tag + "=" + ReadValue().Text);
                    else tags.Add(tag);
                }
                else args.Add(ReadValue());
            }
            return (args, tags);
        }

        private SieveValue ReadValue()
        {
            var token = Take();
            if (token.Kind == TokenKind.String) return new SieveValue(Text: token.Value);
            if (token.Kind == TokenKind.Number && long.TryParse(token.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return new SieveValue(Number: number);
            if (token.Value == "[")
            {
                var values = new List<SieveValue>();
                if (!Accept("]")) { values.Add(ReadValue()); while (Accept(",")) values.Add(ReadValue()); Expect("]"); }
                return new SieveValue(Items: values);
            }
            throw new ParseException("Expected a string, number, or string list.", token);
        }

        private static List<Token> Lex(string input)
        {
            var tokens = new List<Token>(); var line = 1; var column = 1; var i = 0;
            while (i < input.Length)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c)) { Advance(c); i++; continue; }
                if (c == '#') { while (i < input.Length && input[i] is not '\r' and not '\n') { Advance(input[i]); i++; } continue; }
                if (c == '/' && i + 1 < input.Length && input[i + 1] == '*')
                {
                    Advance('/'); Advance('*'); i += 2; var closed = false;
                    while (i < input.Length) { if (input[i] == '*' && i + 1 < input.Length && input[i + 1] == '/') { Advance('*'); Advance('/'); i += 2; closed = true; break; } Advance(input[i]); i++; }
                    if (!closed) throw new ParseException("Unterminated block comment.", new Token(TokenKind.End, "", line, column));
                    continue;
                }
                var startLine = line; var startColumn = column;
                if (input.AsSpan(i).StartsWith("text:", StringComparison.OrdinalIgnoreCase))
                {
                    while (i < input.Length && input[i] is not '\r' and not '\n') { Advance(input[i]); i++; }
                    if (i < input.Length && input[i] == '\r') { Advance(input[i]); i++; }
                    if (i < input.Length && input[i] == '\n') { Advance(input[i]); i++; }
                    var lines = new List<string>(); var terminated = false;
                    while (i < input.Length)
                    {
                        var lineStart = i;
                        while (i < input.Length && input[i] is not '\r' and not '\n') { Advance(input[i]); i++; }
                        var lineText = input[lineStart..i];
                        if (lineText == ".") { terminated = true; if (i < input.Length && input[i] == '\r') { Advance(input[i]); i++; } if (i < input.Length && input[i] == '\n') { Advance(input[i]); i++; } break; }
                        lines.Add(lineText);
                        if (i < input.Length && input[i] == '\r') { Advance(input[i]); i++; }
                        if (i < input.Length && input[i] == '\n') { Advance(input[i]); i++; }
                    }
                    if (!terminated) throw new ParseException("Unterminated text: string.", new Token(TokenKind.End, "", startLine, startColumn));
                    tokens.Add(new Token(TokenKind.String, string.Join("\r\n", lines), startLine, startColumn));
                    continue;
                }
                if (c == '"')
                {
                    Advance(c); i++; var text = new StringBuilder(); var closed = false;
                    while (i < input.Length)
                    {
                        c = input[i++]; Advance(c);
                        if (c == '"') { closed = true; break; }
                        if (c == '\\' && i < input.Length)
                        {
                            c = input[i++]; Advance(c);
                            if (c == 'x' && i + 1 < input.Length && byte.TryParse(input.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var octet)) { text.Append((char)octet); Advance(input[i]); Advance(input[i + 1]); i += 2; }
                            else if (c is '"' or '\\') text.Append(c);
                            else if (c is 'n' or 'r' or 't') text.Append(c switch { 'n' => '\n', 'r' => '\r', _ => '\t' });
                            else { text.Append('\\'); text.Append(c); }
                        }
                        else text.Append(c);
                    }
                    if (!closed) throw new ParseException("Unterminated string literal.", new Token(TokenKind.End, "", startLine, startColumn));
                    tokens.Add(new Token(TokenKind.String, text.ToString(), startLine, startColumn)); continue;
                }
                if ("{}[]();,".Contains(c)) { tokens.Add(new Token(TokenKind.Symbol, c.ToString(), line, column)); Advance(c); i++; continue; }
                var begin = i;
                while (i < input.Length && !char.IsWhiteSpace(input[i]) && !"{}[]();,\"".Contains(input[i]) && !(input[i] == '#' || (input[i] == '/' && i + 1 < input.Length && input[i + 1] == '*'))) { Advance(input[i]); i++; }
                if (begin == i) { Advance(input[i]); i++; continue; }
                var value = input[begin..i];
                var kind = long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? TokenKind.Number : TokenKind.Word;
                tokens.Add(new Token(kind, value, startLine, startColumn));
            }
            tokens.Add(new Token(TokenKind.End, "", line, column));
            return tokens;

            void Advance(char value) { if (value == '\n') { line++; column = 1; } else if (value != '\r') column++; }
        }
    }
}
