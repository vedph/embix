using System.Text;

namespace Embix.Core.Filters;

/// <summary>
/// A text filter for indexing.
/// </summary>
public interface ITextFilter
{
    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text wrapped in a <see cref="StringBuilder"/>.
    /// </param>
    void Apply(StringBuilder text);
}
