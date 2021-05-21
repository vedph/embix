using Embix.Core.Filters;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Embix.Core.Test
{
    public sealed class TokenizerTest
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("  ", "")]
        [InlineData("One", "one")]
        [InlineData("One, Two! ", "one two")]
        public void Tokenize(string text, string expected)
        {
            var tokenizer = new StandardStringTokenizer
            {
                Filter = new CompositeTextFilter(
                    new WhitespaceTextFilter(),
                    new StandardTextFilter())
            };

            string[] actTokens = tokenizer.Tokenize(
                new StringBuilder(text)).ToArray();
            string[] expTokens = expected.Length == 0
                ? Array.Empty<string>() : expected.Split();

            Assert.Equal(expTokens.Length, actTokens.Length);
            for (int i = 0; i < expTokens.Length; i++)
                Assert.Equal(expTokens[i], actTokens[i]);
        }
    }
}
