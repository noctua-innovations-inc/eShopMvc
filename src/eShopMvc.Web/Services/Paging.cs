namespace eShopMvc.Web.Services;

/// <summary>
/// Catalog list paging arguments (Decision R-16). Legacy threw (500) for pageSize 0 (divide by zero),
/// a negative pageIndex (negative OFFSET in SQL) and pageSize * pageIndex overflowing int (negative Skip).
/// </summary>
public static class Paging
{
    public static bool IsValid(int pageSize, int pageIndex) =>
        pageSize >= 1
        && pageIndex >= 0
        && (long)pageSize * pageIndex <= int.MaxValue;
}
