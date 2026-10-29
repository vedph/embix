using Embix.Core.Config;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Configuration;
using SimpleInjector;
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

    /// <summary>
    /// Creates a profile from the specified JSON code, including components
    /// from this test assembly (e.g. <see cref="ReverseTokenMultiplier"/>).
    /// </summary>
    public static EmbixProfile CreateProfile(string json)
    {
        Container container = new();
        EmbixFilterFactory.ConfigureServices(container,
            typeof(TestHelper).Assembly);
        container.Verify();
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryJson(json).Build();
        return new EmbixProfile(json, new EmbixFilterFactory(container, config));
    }
}

/// <summary>
/// Index builder factory using the generic SQL writer, with components
/// from this test assembly.
/// </summary>
internal sealed class TestIndexBuilderFactory(string profile,
    IDbConnectionFactory factory)
    : IndexBuilderFactory(profile, factory, typeof(TestHelper).Assembly)
{
}
