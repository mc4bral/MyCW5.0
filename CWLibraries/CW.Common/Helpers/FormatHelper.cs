using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using System.Globalization;
using System.Text;

namespace CW.Common.Helpers;

public class FormatHelper
{
    private static readonly IList<string> _keywordsToRemoveOnCurrencyParsing = new List<string>() { "incl tax", "excl tax" };

    public static decimal? ParseCurrencyString(string currencyString)
    {
        var result = 0.0m;

        foreach (var keyword in _keywordsToRemoveOnCurrencyParsing)
        {
            currencyString = currencyString.Replace(keyword, string.Empty).Trim();
        }

        if (decimal.TryParse(currencyString, NumberStyles.Currency, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        return null;
    }

    public static string FormatCurrency(decimal amount)
    {
        return string.Format("{0:C}", Math.Floor(amount * 100) / 100);
    }

    public static string FormatDecimal(decimal amount)
    {
        return string.Format("{0:0.00}", Math.Floor(amount * 100) / 100);
    }

    public static async Task<string> FormatAbsPriceAsync(decimal price, IPriceFormatter priceFormatter, bool showCurrency = false, bool showTax = false)
    {
        return ((price < 0) ? "-" : "") + await priceFormatter.FormatPriceAsync(Math.Abs(price), showCurrency, showTax);
    }

    public static string FormatAbsPrice(decimal price)
    {
        return ((price < 0) ? "-" : "") + Math.Abs(price).ToString("F");
    }

    public static async Task<string> FormatPriceAsync(decimal price, IPriceFormatter priceFormatter, ICurrencyService currencyService,
        CurrencySettings currencySettings, IWorkContext workContext, bool priceIncludesTax, bool showCurrency = true)
    {
        var primaryStoreCurrency = await currencyService.GetCurrencyByIdAsync(currencySettings.PrimaryStoreCurrencyId);
        if (primaryStoreCurrency == null)
            throw new Exception("Cannot load primary store currency");
        return await priceFormatter.FormatPriceAsync(price, showCurrency,
            primaryStoreCurrency.CurrencyCode,
            (await workContext.GetWorkingLanguageAsync()).Id,
            priceIncludesTax);
    }

    public static string FormatDateTime(DateTime dateTime)
    {
        return String.Format("{0:dd/MM/yyyy hh:mm tt}", dateTime);
    }

    public static string FormatDate(DateTime dateTime)
    {
        return String.Format("{0:dd/MM/yyyy}", dateTime);
    }

    public static string FormatGender(string genderValue)
    {
        if (!string.IsNullOrEmpty(genderValue))
        {
            if (genderValue.Trim().ToLower() == "m")
            {
                return "Male";
            }
            else
            {
                return "Female";
            }
        }

        return string.Empty;
    }

    public static string GeneratePacknameTitle(string packnameText, int itemsCount = 0, bool isCurrentUser = false)
    {
        var formatBuilder = new StringBuilder("{0}");
        var formatArgs = new List<object>() { packnameText };

        if (isCurrentUser)
        {
            formatBuilder.Append(" (me)");
        }

        if (itemsCount > 0)
        {
            formatBuilder.Append(" - ({1})");
            formatArgs.Add(itemsCount);
        }

        return string.Format(formatBuilder.ToString(), formatArgs.ToArray());
    }

    public static string SanitizeCreditCardNumber(string creditCardNumber)
    {
        if (string.IsNullOrEmpty(creditCardNumber))
            return string.Empty;

        return creditCardNumber.Replace("-", string.Empty);
    }

    public static async Task<string> FormatPercentageAsync(decimal number)
    {
        var workContext = EngineContext.Current.Resolve<IWorkContext>();
        if (workContext != null)
        {
            var language = await workContext.GetWorkingLanguageAsync();
            var culture = new CultureInfo(language.LanguageCulture);
            return number.ToString("P", culture.NumberFormat);
        }
        return number.ToString("P");
    }
}
