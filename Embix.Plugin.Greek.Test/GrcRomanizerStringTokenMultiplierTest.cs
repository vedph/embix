using System.Linq;
using Xunit;

namespace Embix.Plugin.Greek.Test
{
    public sealed class GrcRomanizerStringTokenMultiplierTest
    {
        [Fact]
        public void Multiply_Empty_Empty()
        {
            GrcRomanizerStringTokenMultiplier multiplier =
                new GrcRomanizerStringTokenMultiplier();
            string[] results = multiplier.Multiply("").ToArray();
            Assert.Single(results);
            Assert.Equal("", results[0]);
        }

        [Fact]
        public void Multiply_NonGreek_1()
        {
            GrcRomanizerStringTokenMultiplier multiplier =
                new GrcRomanizerStringTokenMultiplier();
            string[] results = multiplier.Multiply("hello").ToArray();
            Assert.Single(results);
            Assert.Equal("hello", results[0]);
        }

        [Fact]
        public void Multiply_Greek_2()
        {
            GrcRomanizerStringTokenMultiplier multiplier =
                new GrcRomanizerStringTokenMultiplier();
            string[] results = multiplier.Multiply("μῆνιν").ToArray();
            Assert.Equal(2, results.Length);
            Assert.Equal("μῆνιν", results[0]);
            Assert.Equal("mênin", results[1]);
        }
    }
}
