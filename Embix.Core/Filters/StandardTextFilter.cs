using Fusi.Tools.Config;
using System;
using System.Text;

namespace Embix.Core.Filters
{
    /// <summary>
    /// Standard text filter. This removes any character which is not a letter,
    /// a digit, an apostrophe, or a space. Also, letter characters are deprived
    /// of their diacritics and lowercased.
    /// <para>Tag: <c>text-filter.standard</c>.</para>
    /// </summary>
    /// <seealso cref="ITextFilter" />
    [Tag("text-filter.standard")]
    public sealed class StandardTextFilter : ITextFilter
    {
        /// <summary>
        /// Applies this filter to the specified text.
        /// </summary>
        /// <param name="text">The text wrapped in a <see cref="StringBuilder" />.
        /// </param>
        /// <exception cref="ArgumentNullException">text</exception>
        public void Apply(StringBuilder text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            for (int i = text.Length - 1; i > -1; i--)
            {
                switch (text[i])
                {
                    case '’':
                    case '\'':
                    case ' ':
                        break;
                    default:
                        if (char.IsLetter(text[i]))
                        {
                            if (text[i] > 'z')
                            {
                                text[i] = TextFilterHelper.UniData.GetSegment(
                                    text[i], true);
                            }
                            text[i] = char.ToLowerInvariant(text[i]);
                        }
                        else
                        {
                            if (!char.IsDigit(text[i])) text.Remove(i, 1);
                        }
                        break;
                }
            }
        }
    }
}
