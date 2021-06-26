using System.Text;
using Xunit;

namespace Embix.Plugin.Greek.Test
{
    public sealed class GrcSigmaToLunateTextFilterTest
    {
        [Theory]
        [InlineData("", "")]
        [InlineData("Abc", "Abc")]
        [InlineData("ab μῆνιν c", "ab μῆνιν c")]
        [InlineData("Ἀστυπαλλαιέος", "Ἀϲτυπαλλαιέοϲ")]
        [InlineData("ΑΣΤΥΠΑΛΛΑΙΕΟΣ", "ΑϹΤΥΠΑΛΛΑΙΕΟϹ")]
        public void Apply_Ok(string input, string output)
        {
            GrcSigmaToLunateTextFilter filter = new GrcSigmaToLunateTextFilter();
            StringBuilder text = new StringBuilder(input);
            filter.Apply(text);
            Assert.Equal(output, text.ToString());
        }
    }
}
