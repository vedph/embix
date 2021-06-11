using Embix.Core.Config;
using Embix.Core.Filters;
using System.Collections.Generic;
using Xunit;

namespace Embix.Core.Test.Config
{
    public sealed class EmbixProfileTest
    {
        private static readonly EmbixProfile _profile = GetProfile();

        private static EmbixProfile GetProfile()
        {
            string json = TestHelper.LoadResourceText("Config.json");
            return new EmbixProfile(json, EmbixFilterFactoryTest.GetFactory());
        }

        [Fact]
        public void Profile_Definitions_Ok()
        {
            Assert.Equal(1, _profile.Documents.Count);
            Assert.Equal(3, _profile.MetadataFields.Length);
            Assert.Equal("rank", _profile.MetadataFields[0]);
            Assert.Equal("yearMin", _profile.MetadataFields[1]);
            Assert.Equal("yearMax", _profile.MetadataFields[2]);
        }

        [Fact]
        public void Profile_GetNonExistingDocument_Null()
        {
            DocumentDefinition doc = _profile.GetDocument("not-existing");
            Assert.Null(doc);
        }

        [Fact]
        public void Profile_GetExistingDocument_Ok()
        {
            DocumentDefinition doc = _profile.GetDocument("place");
            Assert.NotNull(doc);
            Assert.Equal("place", doc.Id);
            Assert.Equal("SELECT COUNT(*) FROM place;", doc.CountSql);
            Assert.Equal("SELECT title AS plttl, description AS pldsc, " +
                "details AS pldtl, id AS m_targetid FROM place " +
                "ORDER BY place.id " +
                "LIMIT {1} OFFSET {0};", doc.DataSql);
            Assert.Equal(2, doc.TextFilterChains.Count);
            Assert.Equal(2, doc.Tokenizers.Count);
        }

        [Fact]
        public void Profile_GetDocumentFilters_Ok()
        {
            IList<ITextFilter> filters = _profile.GetFilters("tag-wsp-std");
            Assert.Equal(3, filters.Count);
            Assert.Equal(typeof(TagTextFilter), filters[0].GetType());
            Assert.Equal(typeof(WhitespaceTextFilter), filters[1].GetType());
            Assert.Equal(typeof(StandardTextFilter), filters[2].GetType());
        }
    }
}
