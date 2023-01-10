using System.Text;
using Xunit;

namespace Embix.Plugin.Greek.Test;

public sealed class GrcRomanizerTextFilterTest
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Abc", "Abc")]
    [InlineData("ab μῆνιν c", "ab mênin c")]
    [InlineData("Μῆνιν c", "Mênin c")]
    [InlineData("ab μῆνιν", "ab mênin")]
    [InlineData("Μῆνιν iram ἄειδε cane, θεά dea", "Mênin iram áeide cane, theá dea")]
    public void Apply_DefaultOptions_Ok(string greek, string roman)
    {
        GrcRomanizerTextFilter filter = new();
        StringBuilder text = new(greek);
        filter.Apply(text);
        Assert.Equal(roman, text.ToString());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Abc", "Abc")]
    [InlineData("ab μῆνιν c", "ab menin c")]
    [InlineData("Μῆνιν c", "Menin c")]
    [InlineData("ab μῆνιν", "ab menin")]
    [InlineData("Μῆνιν iram ἄειδε cane, θεά dea", "Menin iram aeide cane, thea dea")]
    public void Apply_AsciiOnly_Ok(string greek, string roman)
    {
        GrcRomanizerTextFilter filter = new();
        filter.Configure(new GrcRomanizerOptions
        {
            TargetTable = "$GtrTarget7"
        });
        StringBuilder text = new(greek);
        filter.Apply(text);
        Assert.Equal(roman, text.ToString());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Abc", "Abc")]
    [InlineData("μῆνιν", "menin")]
    [InlineData("Μῆνιν", "Menin")]
    [InlineData("χθές", "chthes")]
    [InlineData("προσευξάμενοι", "proseuxamenoi")]
    [InlineData("ἄγγελος", "aggelos")]
    [InlineData("ὡς", "hos")]
    [InlineData("ῥέω", "rheo")]
    [InlineData("συρρέω", "syrrheo")]
    public void Apply_Pinakes_Ok(string greek, string roman)
    {
        GrcRomanizerTextFilter filter = new();
        filter.Configure(new GrcRomanizerOptions
        {
            TargetTable = "$GtrTarget7",
            KsiAsX = true,
            KhiAsCh = true,
            IncludeIpogegrammeni = false,
            GammaPlusVelarAsN = false,
            HAfterRR = true,
            ConvertPunctuation = true
        });
        StringBuilder text = new(greek);
        filter.Apply(text);
        Assert.Equal(roman, text.ToString());
    }
}
