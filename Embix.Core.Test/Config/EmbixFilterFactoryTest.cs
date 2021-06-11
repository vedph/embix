using Embix.Core.Config;
using Embix.Core.Filters;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Configuration;
using SimpleInjector;
using System.Collections.Generic;
using Xunit;

namespace Embix.Core.Test.Config
{
    public sealed class EmbixFilterFactoryTest
    {
        private static readonly EmbixFilterFactory _factory = GetFactory();

        private static IConfiguration GetConfiguration()
        {
            string json = TestHelper.LoadResourceText("Config.json");
            return new ConfigurationBuilder().AddInMemoryJson(json).Build();
        }

        internal static EmbixFilterFactory GetFactory()
        {
            Container container = new Container();
            EmbixFilterFactory.ConfigureServices(container,
                typeof(EmbixFilterFactoryTest).Assembly);
            container.Verify();
            return new EmbixFilterFactory(container, GetConfiguration());
        }

        [Fact]
        public void GetTextFilters_NotExisting_Null()
        {
            IList<ITextFilter> filters = _factory.GetTextFilters("not-existing");
            Assert.Empty(filters);
        }

        [Fact]
        public void GetTextFilters_Existing_Ok()
        {
            IList<ITextFilter> filters = _factory.GetTextFilters("tag-wsp-std");
            Assert.NotNull(filters);
            Assert.Equal(3, filters.Count);
            Assert.Equal(typeof(TagTextFilter), filters[0].GetType());
            Assert.Equal(typeof(WhitespaceTextFilter), filters[1].GetType());
            Assert.Equal(typeof(StandardTextFilter), filters[2].GetType());
        }

        [Fact]
        public void GetTokenMultiplier_NotExisting_Null()
        {
            IStringTokenMultiplier multiplier =
                _factory.GetTokenMultiplier("not-existing");
            Assert.Null(multiplier);
        }

        [Fact]
        public void GetTokenMultipliers_Existing_Ok()
        {
            IStringTokenMultiplier multiplier =
                _factory.GetTokenMultiplier("rev");
            Assert.NotNull(multiplier);
            Assert.Equal(typeof(ReverseTokenMultiplier), multiplier.GetType());
        }
    }
}
