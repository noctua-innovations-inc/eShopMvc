using eShopMvc.Web.Services;

namespace eShopMvc.Web.UnitTests.Services;

public class PagingTests
{
    [Theory]
    [InlineData(10, 0)]
    [InlineData(1, 0)]
    [InlineData(100, 5)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    public void IsValid_PositiveSizeAndNonNegativeIndex_IsTrue(int pageSize, int pageIndex)
    {
        Assert.True(Paging.IsValid(pageSize, pageIndex));
    }

    [Theory]
    [InlineData(0, 0)] // legacy: DivideByZeroException
    [InlineData(-1, 0)]
    [InlineData(10, -1)] // legacy: negative OFFSET in SQL (the hidden "Previous" link on page 0)
    [InlineData(int.MaxValue, 2)] // legacy: pageSize * pageIndex overflows to a negative Skip
    [InlineData(65536, 65536)]
    public void IsValid_ZeroSizeNegativeIndexOrOverflow_IsFalse(int pageSize, int pageIndex)
    {
        Assert.False(Paging.IsValid(pageSize, pageIndex));
    }
}
