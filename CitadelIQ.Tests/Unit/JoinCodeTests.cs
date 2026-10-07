using CitadelIQ.Domain.Rules;

namespace CitadelIQ.Tests.Unit;

public class JoinCodeTests
{
    [Fact]
    public void Generated_codes_are_12_crockford_characters_and_differ()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => JoinCode.Generate()).ToList();

        Assert.All(codes, code =>
        {
            Assert.Equal(12, code.Length);
            Assert.Matches("^[0-9A-HJKMNP-TV-Z]{12}$", code);
            Assert.Equal(code, JoinCode.Normalize(code));
        });
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Theory]
    [InlineData("7K3M-Q9TX-2HDB", "7K3MQ9TX2HDB")]
    [InlineData("7k3m q9tx 2hdb", "7K3MQ9TX2HDB")]
    [InlineData(" 7K3MQ9TX2HDB ", "7K3MQ9TX2HDB")]
    [InlineData("OOOO-IIII-LLLL", "000011111111")] // Crockford look-alikes
    public void Normalize_accepts_display_formats(string input, string expected)
    {
        Assert.Equal(expected, JoinCode.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7K3M-Q9TX")] // too short
    [InlineData("7K3M-Q9TX-2HDB-X")] // too long
    [InlineData("7K3M-Q9TX-2HDU")] // U is not in the alphabet
    [InlineData("7K3M_Q9TX_2HDB")] // unknown separator
    public void Normalize_rejects_anything_that_cannot_be_a_code(string? input)
    {
        Assert.Null(JoinCode.Normalize(input));
    }

    [Fact]
    public void Format_groups_in_fours()
    {
        Assert.Equal("7K3M-Q9TX-2HDB", JoinCode.Format("7K3MQ9TX2HDB"));
    }
}
