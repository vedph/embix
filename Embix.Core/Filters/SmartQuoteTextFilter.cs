using Fusi.Tools.Config;
using System.Text;

namespace Embix.Core.Filters
{
    /// <summary>
    /// Smart quote text filter. This just looks for any U+2018 or U+2019
    /// smart quotes which are both preceded and followed by a letter, and
    /// replaces them with a straight apostrophe. This can be useful when
    /// typographic habits have changed the apostrophe into quote.
    /// </summary>
    /// <seealso cref="ITextFilter" />
    [Tag("text-filter.smart-quote")]
    public sealed class SmartQuoteTextFilter : ITextFilter
    {
        public void Apply(StringBuilder text)
        {
            for (int i = 1; i < text.Length - 1; i++)
            {
                if ((text[i] == '\u2019' || text[i] == '\u2018') &&
                    (char.IsLetter(text[i - 1]) &&
                    (char.IsLetter(text[i + 1]))))
                {
                    text[i] = '\'';
                }
            }
        }
    }
}
