using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.Core.Test;

internal static class TestHelper
{
    public static string LoadResourceText(string name)
    {
        using StreamReader reader = new(
            Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Embix.Core.Test.Assets.{name}")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
