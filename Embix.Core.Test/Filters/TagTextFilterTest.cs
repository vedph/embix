using Embix.Core.Filters;
using System.Text;
using Xunit;

namespace Embix.Core.Test.Filters
{
    public class TagTextFilterTest
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Héllo", "Héllo")]
        [InlineData("World<sup>12</sup>", "World12")]
        [InlineData("<sup>12</sup>World", "12World")]
        [InlineData("Hello<sup>12</sup>World", "Hello12World")]
        [InlineData("Héllo<NotATag", "Héllo<NotATag")]
        public void Apply(string text, string expected)
        {
            TagTextFilter filter = new TagTextFilter();
            StringBuilder sb = new StringBuilder(text);
            filter.Apply(sb);
            Assert.Equal(expected, sb.ToString());
        }
    }
}
