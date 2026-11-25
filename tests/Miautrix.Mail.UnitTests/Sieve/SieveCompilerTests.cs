using Miautrix.Mail.Protocols.Sieve;

namespace Miautrix.Mail.UnitTests.Sieve;

public sealed class SieveCompilerTests
{
    private readonly SieveCompiler _compiler = new();
    private readonly SieveInterpreter _interpreter = new();
    private static readonly SieveMessageContext Context = new(
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Subject"] = ["Weekly Project report"],
            ["From"] = ["Alice Example <alice@example.test>"],
            ["To"] = ["bob@example.test"],
            ["Auto-Submitted"] = ["no"]
        },
        "alice@example.test", "bob@example.test", 900, ExistingMailboxes: new HashSet<string>(["Inbox", "Archive"], StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("require [\"fileinto\"]; fileinto \"Archive\";")]
    [InlineData("require [\"envelope\"]; if envelope :is \"from\" \"alice@example.test\" { keep; }")]
    [InlineData("require [\"imap4flags\"]; setflag [\"\\\\Seen\"]; addflag [\"\\\\Flagged\"]; removeflag [\"\\\\Seen\"];")]
    [InlineData("require [\"vacation\"]; vacation :days 7 :subject \"Away\" \"I am away\";")]
    [InlineData("require [\"reject\"]; reject \"No thanks\"; ereject \"No thanks\";")]
    [InlineData("require [\"copy\"]; fileinto :copy \"Archive\";")]
    [InlineData("require [\"mailbox\"]; fileinto :create \"NewFolder\";")]
    [InlineData("if true { keep; } elsif false { discard; } else { stop; }")]
    [InlineData("if not header :matches \"Subject\" \"*secret*\" { redirect \"archive@example.test\"; }")]
    [InlineData("# comments and encoded octets\nif header :is \"Subject\" \"Weekly Project report\\x21\" { keep; }")]
    [InlineData("require [\"vacation\"]; vacation :mime text:\r\nI am away.\r\n.\r\n;")]
    public void Compiler_CompilesSupportedCommandsAndControlFlow(string source)
    {
        var result = _compiler.Compile(source);
        Assert.True(result.Success, result.Diagnostic.ErrorMessage);
    }

    [Theory]
    [InlineData("if header :contains \"Subject\" \"project\" { discard; }", true)]
    [InlineData("if header :is \"Subject\" \"Weekly Project report\" { discard; }", true)]
    [InlineData("if header :matches \"Subject\" \"Weekly * report\" { discard; }", true)]
    [InlineData("if header :matches \"Subject\" \"Weekly \\\\* report\" { discard; }", false)]
    [InlineData("if header :is :comparator \"i;octet\" \"Subject\" \"Weekly Project report\" { discard; }", true)]
    [InlineData("if exists [\"Subject\", \"From\"] { discard; }", true)]
    [InlineData("if address :domain :contains \"From\" \"example.test\" { discard; }", true)]
    [InlineData("if address :localpart :is \"From\" \"alice\" { discard; }", true)]
    [InlineData("if envelope :is \"to\" \"bob@example.test\" { discard; }", true)]
    [InlineData("if size :over 100 { discard; }", true)]
    [InlineData("if allof (true, anyof (false, not false)) { discard; }", true)]
    [InlineData("if mailboxexists \"Archive\" { discard; }", true)]
    public void Interpreter_EvaluatesCoreTestsAndComparators(string source, bool expected)
    {
        var compiled = _compiler.Compile(source);
        Assert.True(compiled.Success, compiled.Diagnostic.ErrorMessage);
        var executed = _interpreter.Execute(compiled.Program!, Context);
        Assert.False(executed.RuntimeError);
        Assert.Equal(expected, executed.Effects.Any(x => x.Name == "discard"));
    }

    [Fact]
    public void Interpreter_ReturnsImplicitKeepWhenScriptCompletesWithoutAction()
    {
        var compiled = _compiler.Compile("if false { discard; }");
        Assert.True(compiled.Success, compiled.Diagnostic.ErrorMessage);
        var executed = _interpreter.Execute(compiled.Program!, Context);
        Assert.Equal("keep", Assert.Single(executed.Effects).Name);
    }

    [Theory]
    [InlineData("keep;")]
    [InlineData("fileinto \"Archive\";")]
    [InlineData("redirect \"archive@example.test\";")]
    [InlineData("discard;")]
    [InlineData("stop;")]
    [InlineData("require [\"imap4flags\"]; setflag [\"\\\\Seen\"];")]
    [InlineData("require [\"imap4flags\"]; addflag [\"\\\\Seen\"];")]
    [InlineData("require [\"imap4flags\"]; removeflag [\"\\\\Seen\"];")]
    [InlineData("require [\"reject\"]; reject \"No thanks\";")]
    [InlineData("require [\"reject\"]; ereject \"No thanks\";")]
    [InlineData("require [\"vacation\"]; vacation :days 7 \"Away\";")]
    [InlineData("require [\"copy\"]; fileinto :copy \"Archive\";")]
    [InlineData("require [\"mailbox\"]; fileinto :create \"Folder\";")]
    public void Interpreter_EmitsCoreActionEffects(string source)
    {
        var compiled = _compiler.Compile(source);
        Assert.True(compiled.Success, compiled.Diagnostic.ErrorMessage);
        var result = _interpreter.Execute(compiled.Program!, Context);
        Assert.False(result.RuntimeError);
        Assert.NotEmpty(result.Effects);
    }

    [Fact]
    public void Interpreter_FallsBackToKeepOnStepLimit()
    {
        var compiled = _compiler.Compile("if true { discard; }");
        Assert.True(compiled.Success, compiled.Diagnostic.ErrorMessage);
        var executed = _interpreter.Execute(compiled.Program!, Context, stepLimit: 1);
        Assert.True(executed.RuntimeError);
        Assert.Equal("keep", Assert.Single(executed.Effects).Name);
    }

    [Fact]
    public void Compiler_ReportsUnknownRequireLocation()
    {
        var result = _compiler.Compile("require [\"madeup\"]; keep;");
        Assert.False(result.Success);
        Assert.Equal(1, result.Diagnostic.ErrorLine);
        Assert.NotNull(result.Diagnostic.ErrorColumn);
    }
}
