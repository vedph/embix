using System;
using System.Text;

namespace Embix.Core;

/// <summary>
/// Extensions to <see cref="StringBuilder"/>.
/// See https://stackoverflow.com/questions/12261344/fastest-search-method-in-stringbuilder.
/// </summary>
public static class StringBuilderExtensions
{
    /// <summary>
    /// Determines whether <paramref name="haystack"/> contains
    /// <paramref name="needle"/>.
    /// </summary>
    /// <param name="haystack">The haystack.</param>
    /// <param name="needle">The needle.</param>
    /// <returns>
    /// <c>true</c> if contains the needle; otherwise, <c>false</c>.
    /// </returns>
    public static bool Contains(this StringBuilder haystack, string needle)
    {
        return haystack.IndexOf(needle) != -1;
    }

    /// <summary>
    /// Finds the index of <paramref name="needle"/> in
    /// <paramref name="haystack"/>.
    /// </summary>
    /// <param name="haystack">The haystack.</param>
    /// <param name="needle">The needle.</param>
    /// <returns>Index or -1 if not found</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static int IndexOf(this StringBuilder haystack, string needle)
    {
        if (haystack == null) throw new ArgumentNullException(nameof(haystack));
        if (needle == null) throw new ArgumentNullException(nameof(needle));

        if (needle.Length == 0) return 0;

        // can't beat just spinning through for it
        if (needle.Length == 1)
        {
            char c = needle[0];
            for (int idx = 0; idx != haystack.Length; ++idx)
            {
                if (haystack[idx] == c) return idx;
            }
            return -1;
        }

        int m = 0;
        int i = 0;
        int[] T = KMPTable(needle);
        while (m + i < haystack.Length)
        {
            if (needle[i] == haystack[m + i])
            {
                if (i == needle.Length - 1)
                {
                    return m == needle.Length ? -1 : m;
                }
                ++i;
            }
            else
            {
                m = m + i - T[i];
                i = T[i] > -1 ? T[i] : 0;
            }
        }

        return -1;
    }

    private static int[] KMPTable(string sought)
    {
        int[] table = new int[sought.Length];
        int pos = 2;
        int cnd = 0;
        table[0] = -1;
        table[1] = 0;

        while (pos < table.Length)
        {
            if (sought[pos - 1] == sought[cnd])
            {
                table[pos++] = ++cnd;
            }
            else if (cnd > 0)
            {
                cnd = table[cnd];
            }
            else
            {
                table[pos++] = 0;
            }
        }

        return table;
    }
}
