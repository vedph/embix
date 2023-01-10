using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test.Filters;

public sealed class SeparatorTextFilterTest
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Hello world", "Hello world")]
    [InlineData("one/uno", "one uno")]
    [InlineData("one,uno,heis", "one uno heis")]
    public void Apply(string text, string expected)
    {
        SeparatorTextFilter filter = new();
        StringBuilder sb = new(text);
        filter.Apply(sb);
        Assert.Equal(expected, sb.ToString());
    }
}
