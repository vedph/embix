using Embix.Core;
using Fusi.Text.Unicode;
using Fusi.Tools.Config;
using System.Collections.Generic;
using System.Linq;

namespace Embix.Plugin.Greek;

/// <summary>
/// String-token multiplier which for any Greek token provides an additional
/// transliterated token.
/// Tag: <c>string-token-multiplier.grc-romanizer</c>.
/// </summary>
/// <seealso cref="IStringTokenMultiplier" />
[Tag("string-token-multiplier.grc-romanizer")]
public sealed class GrcRomanizerStringTokenMultiplier : GrcRomanizer,
    IStringTokenMultiplier
{
    /// <summary>
    /// Multiplies the specified token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The original token and eventually its transliterated form.
    /// </returns>
    public IEnumerable<string> Multiply(string token)
    {
        if (!string.IsNullOrEmpty(token) &&
            token.Any(c => UniData.IsInGreekRange(c)))
        {
            return new[] { token, Transliterator.Transliterate(token) };
        }
        return new[] { token };
    }
}
