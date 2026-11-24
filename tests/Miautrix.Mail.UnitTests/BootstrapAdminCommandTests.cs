using Miautrix.Mail.Cli.Commands;

namespace Miautrix.Mail.UnitTests;

public sealed class BootstrapAdminCommandTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Existing_credential_requires_explicit_reset(bool exists, bool reset, bool expected) =>
        Assert.Equal(expected, BootstrapAdminCommand.CanReplaceCredential(exists, reset));

    [Theory]
    [InlineData("correct horse battery staple", true)]
    [InlineData("1234567", false)]
    [InlineData("        ", false)]
    public void Password_policy_requires_eight_non_whitespace_characters(string password, bool expected) =>
        Assert.Equal(expected, BootstrapAdminCommand.IsPasswordAllowed(password));
}
