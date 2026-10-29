using Embix.Core.Config;
using Embix.Core.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Embix.Core.Test.Config;

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
        Assert.Single(_profile.GetDocuments());
        Assert.Equal(3, _profile.MetadataFields.Count);
        Assert.Equal("rank", _profile.MetadataFields[0]);
        Assert.Equal("yearMin", _profile.MetadataFields[1]);
        Assert.Equal("yearMax", _profile.MetadataFields[2]);
    }

    [Fact]
    public void Profile_FieldLengths_Loaded()
    {
        EmbixProfile profile = TestHelper.CreateProfile(
            "{ \"TokenFieldLengths\": { \"value\": 50 }, " +
            "\"OccurrenceFieldLengths\": { \"note\": 10 } }");
        Assert.Equal(50, profile.TokenFieldLengths["value"]);
        // not specified: default kept
        Assert.Equal(5, profile.TokenFieldLengths["language"]);
        Assert.Equal(10, profile.OccurrenceFieldLengths["note"]);
    }

    [Fact]
    public void Profile_NoFieldLengths_Defaults()
    {
        EmbixProfile profile = TestHelper.CreateProfile("{}");
        Assert.Equal(100, profile.TokenFieldLengths["value"]);
        Assert.Equal(5, profile.TokenFieldLengths["language"]);
        Assert.Empty(profile.OccurrenceFieldLengths);
        Assert.Empty(profile.MetadataFields);
        Assert.Empty(profile.GetDocuments());
    }

    [Fact]
    public void Profile_Documents_InDefinitionOrder()
    {
        EmbixProfile profile = TestHelper.CreateProfile(
            "{ \"Documents\": [ { \"Id\": \"z\" }, { \"Id\": \"a\" }, " +
            "{ \"Id\": \"m\" } ] }");
        Assert.Equal(["z", "a", "m"], profile.GetDocuments().Select(d => d.Id));
    }

    [Fact]
    public void Profile_DuplicateDocumentId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TestHelper.CreateProfile(
            "{ \"Documents\": [ { \"Id\": \"a\" }, { \"Id\": \"a\" } ] }"));
    }

    [Fact]
    public void Profile_GetTokenMultiplier_Ok()
    {
        Assert.IsType<ReverseTokenMultiplier>(_profile.GetTokenMultiplier("rev"));
        Assert.Null(_profile.GetTokenMultiplier("not-existing"));
    }

    [Fact]
    public void Profile_GetTokenizer_WithFilters()
    {
        IStringTokenizer? tokenizer = _profile.GetTokenizer("std");
        Assert.IsType<StandardStringTokenizer>(tokenizer);
        Assert.IsType<StopwordTextFilter>(Assert.Single(tokenizer.Filter!.Filters));
    }

    [Fact]
    public void Profile_GetNonExistingDocument_Null()
    {
        DocumentDefinition? doc = _profile.GetDocument("not-existing");
        Assert.Null(doc);
    }

    [Fact]
    public void Profile_GetExistingDocument_Ok()
    {
        DocumentDefinition? doc = _profile.GetDocument("place");
        Assert.NotNull(doc);
        Assert.Equal("place", doc.Id);
        Assert.Equal("SELECT COUNT(*) FROM place;", doc.CountSql);
        Assert.Equal("SELECT title AS plttl, description AS pldsc, " +
            "details AS pldtl, id AS m_target_id FROM place " +
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
