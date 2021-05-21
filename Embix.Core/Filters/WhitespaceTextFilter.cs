using Fusi.Tools.Config;
using System.Diagnostics;
using System.Text;

namespace Embix.Core.Filters
{
    /// <summary>
    /// Whitespaces text filter. This normalizes whitespace characters by
    /// replacing them with a single space and trimming the text at both
    /// sides.
    /// <para>Tag: <c>text-filter.whitespace</c>.</para>
    /// </summary>
    /// <seealso cref="ITextFilter" />
    [Tag("text-filter.whitespace")]
    public sealed class WhitespaceTextFilter : ITextFilter
    {
        public void Apply(StringBuilder text)
        {
            // reach last non WS
            int i = text.Length - 1;
            while (i > -1 && char.IsWhiteSpace(text[i])) i--;
            if (i == -1)
            {
                text.Clear();
                return;
            }
            if (i < text.Length - 1)
                text.Remove(i + 1, text.Length - (i + 1));

            // reach first non WS
            i = 0;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            Debug.Assert(i < text.Length);
            if (i > 0) text.Remove(0, i);

            // normalize any other whitespace
            i = text.Length - 1;
            while (i > -1)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    int right = i;
                    text[i--] = ' ';
                    while (i > -1 && char.IsWhiteSpace(text[i])) i--;
                    if (right - i > 1) text.Remove(i + 1, right - i - 1);
                }
                else i--;
            }
        }
    }
}
