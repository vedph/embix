using Fusi.Text.Unicode;

namespace Embix.Core;

internal static class TextFilterHelper
{
    private static UniData? _ud;

    public static UniData UniData
    {
        get
        {
            return _ud ??= new UniData();
        }
    }
}
