using Miautrix.Mail.Protocols.Imap;

namespace Miautrix.Mail.UnitTests.Imap;

public sealed class SequenceSetTests
{
    [Theory]
    [InlineData("1", 5, "1")]
    [InlineData("1:5", 5, "1,2,3,4,5")]
    [InlineData("2,4:*", 5, "2,4,5")]
    [InlineData("5:2", 5, "2,3,4,5")]
    public void Resolves_sequence_sets(string input, uint maximum, string expected)
    {
        Assert.True(SequenceSet.TryParse(input, out var set));
        Assert.Equal(expected, string.Join(',', set!.Resolve(maximum)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,,2")]
    [InlineData("0")]
    [InlineData("1:bad")]
    public void Rejects_invalid_sequence_sets(string input) => Assert.False(SequenceSet.TryParse(input, out _));
}
