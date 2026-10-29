using Embix.Core.Filters;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Embix.Core.Test;

public sealed class StringBuilderExtensionsTest
{
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("abc", "", 0)]
    [InlineData("", "a", -1)]
    [InlineData("abc", "a", 0)]
    [InlineData("abc", "c", 2)]
    [InlineData("xyab", "ab", 2)]
    [InlineData("aaab", "aab", 1)]
    [InlineData("abcabd", "abd", 3)]
    [InlineData("abc", "abcd", -1)]
    [InlineData("abc", "x", -1)]
    public void IndexOf_Ok(string haystack, string needle, int expected)
    {
        Assert.Equal(expected, new StringBuilder(haystack).IndexOf(needle));
        Assert.Equal(expected > -1, new StringBuilder(haystack).Contains(needle));
    }
}

public sealed class StopwordTextFilterTest
{
    [Theory]
    [InlineData("the", "")]
    [InlineData("then", "then")]
    [InlineData("th", "th")]
    [InlineData("123", "")]
    [InlineData("12a", "12a")]
    [InlineData("rome", "rome")]
    public void Apply_Defaults_Ok(string text, string expected)
    {
        StringBuilder sb = new(text);
        new StopwordTextFilter().Apply(sb);
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public void Apply_Configured_Ok()
    {
        StopwordTextFilter filter = new();
        filter.Configure(new StopwordTextFilterOptions
        {
            Stopwords = ["de", "la", "de"],
            StopOnDigitOnly = false
        });

        StringBuilder sb = new("123");
        filter.Apply(sb);
        Assert.Equal("123", sb.ToString());

        sb = new StringBuilder("la");
        filter.Apply(sb);
        Assert.Equal("", sb.ToString());

        sb = new StringBuilder("the");
        filter.Apply(sb);
        Assert.Equal("the", sb.ToString());
    }
}

public sealed class NullStringTokenizerTest
{
    [Fact]
    public void Tokenize_NoFilter_WholeText()
    {
        NullStringTokenizer tokenizer = new();
        Assert.Equal(["Alpha Beta"],
            tokenizer.Tokenize(new StringBuilder("Alpha Beta")));
    }

    [Fact]
    public void Tokenize_FilterEmptyingText_NoToken()
    {
        NullStringTokenizer tokenizer = new()
        {
            Filter = new CompositeTextFilter(new StopwordTextFilter())
        };
        Assert.Empty(tokenizer.Tokenize(new StringBuilder("the")));
    }
}

public sealed class StandardAutoNumberTest
{
    [Fact]
    public void GetNextId_Seed_StartsAfterSeed()
    {
        StandardAutoNumber number = new(10);
        Assert.Equal(11, number.GetNextId());
        Assert.Equal(12, number.GetNextId());
    }

    [Fact]
    public void GetNextId_Concurrent_Unique()
    {
        StandardAutoNumber number = new();
        ConcurrentBag<int> ids = [];
        Parallel.For(0, 10000, _ => ids.Add(number.GetNextId()));
        Assert.Equal(10000, ids.Distinct().Count());
        Assert.Equal(10000, ids.Max());
    }
}

public sealed class CompositeTextFilterTest
{
    [Fact]
    public void Apply_StopsWhenEmpty()
    {
        CountingFilter counter = new();
        CompositeTextFilter filter = new(new StopwordTextFilter(), counter);
        filter.Apply(new StringBuilder("the"));
        Assert.Equal(0, counter.Count);

        filter.Apply(new StringBuilder("rome"));
        Assert.Equal(1, counter.Count);
    }

    private sealed class CountingFilter : ITextFilter
    {
        public int Count { get; private set; }
        public void Apply(StringBuilder text) => Count++;
    }
}
