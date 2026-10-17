using EntryHub.Api.Features.V1.Common;

namespace EntryHub.Api.Tests.Features.V1;

public class PagingTests
{
    [Fact]
    public void Defaults_to_the_first_page_of_twenty()
    {
        var paging = Paging.Normalize(null, null);

        Assert.Equal(1, paging.Page);
        Assert.Equal(20, paging.PageSize);
        Assert.Equal(0, paging.Skip);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(4, 4)]
    public void Page_is_never_below_one(int requested, int expected) =>
        Assert.Equal(expected, Paging.Normalize(requested, null).Page);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(5000, 100)]
    public void Page_size_stays_between_one_and_one_hundred(int requested, int expected) =>
        Assert.Equal(expected, Paging.Normalize(null, requested).PageSize);

    [Fact]
    public void Skip_counts_the_items_of_the_previous_pages() =>
        Assert.Equal(30, Paging.Normalize(4, 10).Skip);
}