using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test;

public sealed class StandardStringTokenizerTest
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("One", "one")]
    [InlineData("One, Two! ", "one two")]
    [InlineData("it's true!", "it s true")]
    // the char after a separator used to be skipped as a separator
    [InlineData("l' alba", "l alba")]
    [InlineData("a 'b'", "a b")]
    [InlineData("x''y", "x y")]
    [InlineData("'start", "start")]
    [InlineData("end'", "end")]
    public void Tokenize(string? text, string expected)
    {
        var tokenizer = new StandardStringTokenizer
        {
            Filter = new CompositeTextFilter(
                new WhitespaceTextFilter(),
                new StandardTextFilter())
        };

        string[] actTokens = [.. tokenizer.Tokenize(
            new StringBuilder(text))];
        string[] expTokens = expected.Length == 0
            ? [] : expected.Split();

        Assert.Equal(expTokens.Length, actTokens.Length);
        for (int i = 0; i < expTokens.Length; i++)
            Assert.Equal(expTokens[i], actTokens[i]);
    }

    [Fact]
    public void Tokenize_NoFilter_DoubleSpaces_NoEmptyTokens()
    {
        StandardStringTokenizer tokenizer = new();
        Assert.Equal(["a", "b"], tokenizer.Tokenize(new StringBuilder("a  b")));
    }
}
