using System.ComponentModel.DataAnnotations;
using eShopMvc.Web.Models;

namespace eShopMvc.Web.UnitTests.Models;

// Decision P2-20: the price range is checked as a decimal.
public class CatalogItemValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("19.50")]
    [InlineData("1000000")]
    [InlineData("1000000.00")]
    public void Price_InRange_IsValid(string price)
    {
        Assert.Empty(PriceErrors(decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("1000000.01")]
    [InlineData("1000000.4")] // legacy's int range rounded this to 1000000 and accepted it
    [InlineData("2000000")]
    [InlineData("2147483648")] // int.MaxValue + 1: legacy's int range threw an OverflowException
    [InlineData("99999999999")]
    [InlineData("79228162514264337593543950335")] // decimal.MaxValue
    public void Price_AboveRange_GetsTheRangeMessageInsteadOfAnException(string price)
    {
        var errors = PriceErrors(decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains("The field Price must be between 0 and 1000000.", errors);
    }

    [Fact]
    public void Price_Negative_GetsTheFormatAndTheRangeMessage()
    {
        Assert.Equal(
            ["The field Price must be a positive number with maximum two decimals.", "The field Price must be between 0 and 1000000."],
            PriceErrors(-1m).Order(StringComparer.Ordinal));
    }

    private static List<string> PriceErrors(decimal price)
    {
        var item = new CatalogItem { Name = "x", Price = price };
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(price, new ValidationContext(item) { MemberName = nameof(CatalogItem.Price) }, results);
        return results.Select(r => r.ErrorMessage!).ToList();
    }
}
