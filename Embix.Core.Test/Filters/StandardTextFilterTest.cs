using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test.Filters;

public sealed class StandardTextFilterTest
{
    [Theory]
    [InlineData("", "")]
    [InlineData("HeLlO", "hello")]
    [InlineData("héllò", "hello")]
    [InlineData("ma'al", "ma'al")]
    [InlineData("\"ab12-c!\"", "ab12c")]
    public void Apply(string text, string expected)
    {
        StandardTextFilter filter = new();
        StringBuilder sb = new(text);
        filter.Apply(sb);
        Assert.Equal(expected, sb.ToString());
    }
}
