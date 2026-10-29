using System;
using System.Text;

namespace Embix.Core;

/// <summary>
/// Extensions to <see cref="StringBuilder"/>.
/// </summary>
public static class StringBuilderExtensions
{
    /// <summary>
    /// Determines whether <paramref name="haystack"/> contains the specified
    /// <paramref name="needle"/>.
    /// </summary>
    /// <param name="haystack">The haystack.</param>
    /// <param name="needle">The needle.</param>
    /// <returns>
    ///   <c>true</c> if found; otherwise, <c>false</c>.
    /// </returns>
    public static bool Contains(this StringBuilder haystack, string needle)
    {
        return haystack.IndexOf(needle) != -1;
    }

    /// <summary>
    /// Gets the index of the first occurrence of <paramref name="needle"/>
    /// in <paramref name="haystack"/> (ordinal comparison).
    /// </summary>
    /// <param name="haystack">The haystack.</param>
    /// <param name="needle">The needle.</param>
    /// <returns>Index or -1 if not found.</returns>
    /// <exception cref="ArgumentNullException">haystack or needle</exception>
    public static int IndexOf(this StringBuilder haystack, string needle)
    {
        ArgumentNullException.ThrowIfNull(haystack);
        ArgumentNullException.ThrowIfNull(needle);

        if (needle.Length == 0) return 0;

        int last = haystack.Length - needle.Length;
        for (int m = 0; m <= last; m++)
        {
            int i = 0;
            while (i < needle.Length && haystack[m + i] == needle[i]) i++;
            if (i == needle.Length) return m;
        }
        return -1;
    }
}
