using Fusi.Tools.Config;
using System.Collections.Generic;

namespace Embix.Core.Test
{
    /// <summary>
    /// Mock multiplier for testing purposes. This just multiplies the token
    /// by reversing its value.
    /// </summary>
    [Tag("string-token-multiplier.reverse")]
    public sealed class ReverseTokenMultiplier : IStringTokenMultiplier
    {
        // https://www.c-sharpcorner.com/UploadFile/19b1bd/reverse-a-string-in-different-ways-using-C-Sharp/
        private static string ReverseString(string str)
        {
            char[] inputstream = str.ToCharArray();
            int length = str.Length - 1;
            for (int i = 0; i < length; i++, length--)
            {
                inputstream[i] ^= inputstream[length];
                inputstream[length] ^= inputstream[i];
                inputstream[i] ^= inputstream[length];
            }
            return new string(inputstream);
        }

        public IEnumerable<string> Multiply(string token)
        {
            string reverse = ReverseString(token);
            return reverse == token
                ? new[] { token }
                : new[] { token, reverse };
        }
    }
}
