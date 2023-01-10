using System.Collections.Generic;

namespace Embix.Core;

/// <summary>
/// Token multiplier. This takes a single token, and emits 0 or more tokens
/// from it. Implementors of this interface can be used to provide synonyms,
/// transliterated forms, etc.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ITokenMultiplier<T>
{
    /// <summary>
    /// Multiplies the specified token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The resulting tokens.</returns>
    IEnumerable<T> Multiply(T token);
}
