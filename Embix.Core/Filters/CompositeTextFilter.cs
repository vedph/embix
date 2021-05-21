using System.Collections.Generic;
using System.Text;

namespace Embix.Core.Filters
{
    /// <summary>
    /// A composite text filter, chaining any number of
    /// <see cref="ITextFilter"/>'s. This is used to filter the whole input
    /// text before tokenizing it, and to filter each token once it has been
    /// extracted from the text.
    /// </summary>
    public class CompositeTextFilter
    {
        /// <summary>
        /// Gets the filters.
        /// </summary>
        public List<ITextFilter> Filters { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTextFilter"/>
        /// class.
        /// </summary>
        public CompositeTextFilter(params ITextFilter[] filters)
        {
            Filters = new List<ITextFilter>(filters);
        }

        /// <summary>
        /// Applies the filters to the specified text, stopping the chain
        /// if the text is reduced to zero.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Apply(StringBuilder text)
        {
            foreach (ITextFilter filter in Filters)
            {
                filter.Apply(text);
                if (text.Length == 0) break;
            }
        }
    }
}
