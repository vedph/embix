using Embix.Core.Filters;
using Fusi.Text.Unicode;
using Fusi.Tools.Config;
using System;
using System.Text;

namespace Embix.Plugin.Greek;

/// <summary>
/// Ancient (polytonic) Greek romanizer filter.
/// Tag: <c>text-filter.grc-romanizer</c>.
/// </summary>
/// <seealso cref="ITextFilter" />
/// <seealso cref="IConfigurable{GrcRomanizerTextFilterOptions}" />
[Tag("text-filter.grc-romanizer")]
public class GrcRomanizerTextFilter : GrcRomanizer, ITextFilter
{
    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text wrapped in a <see cref="StringBuilder" />
    /// .</param>
    /// <exception cref="ArgumentNullException">text</exception>
    public void Apply(StringBuilder text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        // find the index and length of each Greek span in text
        // and process it via the transliterator
        int i = 0;

        while (i < text.Length)
        {
            if (UniData.IsInGreekRange(text[i]))
            {
                int j = i + 1;
                while (j < text.Length &&
                    (char.IsWhiteSpace(text[j])
                    || UniData.IsInGreekRange(text[j])))
                {
                    j++;
                }
                string greek = text.ToString(i, j - i);
                string roman = Transliterator.Transliterate(greek);
                text.Remove(i, j - i);
                text.Insert(i, roman);
                i += roman.Length;
            }
            else i++;
        }
    }
}
