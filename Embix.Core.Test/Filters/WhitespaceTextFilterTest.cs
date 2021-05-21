using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test.Filters
{
    public sealed class WhitespaceTextFilterTest
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Hello world", "Hello world")]
        [InlineData("Hello\tworld", "Hello world")]
        [InlineData("  left trim", "left trim")]
        [InlineData("right trim  ", "right trim")]
        [InlineData("  both  trim\t", "both trim")]
        public void Apply(string text, string expected)
        {
            WhitespaceTextFilter filter = new WhitespaceTextFilter();
            StringBuilder sb = new StringBuilder(text);
            filter.Apply(sb);
            Assert.Equal(expected, sb.ToString());
        }
    }
}
