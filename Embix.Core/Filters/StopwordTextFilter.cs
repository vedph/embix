using Fusi.Tools.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Embix.Core.Filters
{
    /// <summary>
    /// Stopwords token filter. This text filter assumes that the text it is
    /// applied to corresponds to a single token.
    /// <para>Tag: <c>text-filter.stopword</c>.</para>
    /// </summary>
    /// <seealso cref="ITokenFilter" />
    [Tag("text-filter.stopword")]
    public sealed class StopwordTextFilter : ITextFilter,
        IConfigurable<StopwordTextFilterOptions>
    {
        private readonly List<string> _stopwords;
        private bool _stopOnDigitOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="StopwordTextFilter"/> class.
        /// </summary>
        public StopwordTextFilter()
        {
            _stopwords = new List<string>();

            Configure(new StopwordTextFilterOptions
            {
                Stopwords = new[]
                {
                    "the", "an", "a", "of", "in", "and", "was", "to", "is",
                    "on", "at", "by", "or", "as", "that", "from", "s", "it",
                    "with", "between", "no", "its", "this", "were", "are"
                },
                StopOnDigitOnly = true
            });
        }

        /// <summary>
        /// Configures this filter with the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="ArgumentNullException">options</exception>
        public void Configure(StopwordTextFilterOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _stopOnDigitOnly = options.StopOnDigitOnly;

            _stopwords.Clear();
            if (options.Stopwords?.Length > 0)
            {
                foreach (string word in options.Stopwords
                    .OrderByDescending(s => s.Length)
                    .ThenBy(s => s))
                {
                    if (!_stopwords.Contains(word)) _stopwords.Add(word);
                }
            }
        }

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

            // stop on digit only
            if (_stopOnDigitOnly)
            {
                int i = 0;
                while (i < text.Length)
                {
                    if (!char.IsDigit(text[i])) break;
                    i++;
                }
                if (i == text.Length)
                {
                    text.Clear();
                    return;
                }
            }

            // stopwords
            foreach (string word in _stopwords)
            {
                if (word.Length < text.Length) break;
                if (word.Length > text.Length) continue;
                int i = 0;
                while (i < text.Length)
                {
                    if (text[i] != word[i]) break;
                    i++;
                }
                if (i == word.Length)
                {
                    text.Clear();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Options for <see cref="StopwordTextFilter"/>.
    /// </summary>
    public class StopwordTextFilterOptions
    {
        /// <summary>
        /// Gets or sets the list of stopwords.
        /// </summary>
        public string[] Stopwords { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to exclude any word
        /// consisting of digits only.
        public bool StopOnDigitOnly { get; set; }
    }
}
