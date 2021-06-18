using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test.Filters
{
    public sealed class SmartQuoteTextFilterTest
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Hello world", "Hello world")]
        [InlineData("l’arco", "l'arco")]
        [InlineData("l‘arco", "l'arco")]
        [InlineData("‘hey!’", "‘hey!’")]
        [InlineData("‘hey!’ said he", "‘hey!’ said he")]
        [InlineData("he shouted: ‘hey!’", "he shouted: ‘hey!’")]
        public void Apply(string text, string expected)
        {
            SmartQuoteTextFilter filter = new SmartQuoteTextFilter();
            StringBuilder sb = new StringBuilder(text);
            filter.Apply(sb);
            Assert.Equal(expected, sb.ToString());
        }
    }
}
