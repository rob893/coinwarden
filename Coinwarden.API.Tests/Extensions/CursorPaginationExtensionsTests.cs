using System.Collections.Generic;
using Coinwarden.API.Extensions;
using Coinwarden.API.Models;
using Coinwarden.API.Models.QueryParameters;

namespace Coinwarden.API.Tests.Extensions;

/// <summary>
/// Tests for cursor pagination extension methods.
/// </summary>
public sealed class CursorPaginationExtensionsTests
{
    [Fact]
    public void ToCursorPaginatedList_FirstPage_ReturnsCorrectCount()
    {
        var items = new List<TestItem>
        {
            new() { Id = 1 },
            new() { Id = 2 },
            new() { Id = 3 },
            new() { Id = 4 },
            new() { Id = 5 },
        };

        var queryParams = new CursorPaginationQueryParameters { First = 2 };
        var result = items.ToCursorPaginatedList(queryParams);

        Assert.Equal(2, result.PageCount);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void ToCursorPaginatedList_EmptyList_ReturnsEmptyResult()
    {
        var items = new List<TestItem>();
        var queryParams = new CursorPaginationQueryParameters { First = 10 };
        var result = items.ToCursorPaginatedList(queryParams);

        Assert.Equal(0, result.PageCount);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void ToCursorPaginatedList_NoPageSize_ReturnsAll()
    {
        var items = new List<TestItem>
        {
            new() { Id = 1 },
            new() { Id = 2 },
            new() { Id = 3 },
        };

        var queryParams = new CursorPaginationQueryParameters();
        var result = items.ToCursorPaginatedList(queryParams);

        Assert.Equal(3, result.PageCount);
    }

    private sealed class TestItem : IIdentifiable<int>
    {
        public required int Id { get; init; }
    }
}
