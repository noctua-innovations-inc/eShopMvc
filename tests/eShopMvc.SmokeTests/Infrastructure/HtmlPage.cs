using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace eShopMvc.SmokeTests.Infrastructure;

/// <summary>A parsed HTML page with helpers for the elements the smoke tests care about.</summary>
public sealed class HtmlPage
{
    public const string AntiforgeryFieldName = "__RequestVerificationToken";

    private HtmlPage(IHtmlDocument document, string url)
    {
        Document = document;
        Url = url;
    }

    public IHtmlDocument Document { get; }

    public string Url { get; }

    public static HtmlPage Parse(string html, string url) => new(new HtmlParser().ParseDocument(html), url);

    public IElement? Find(string selector) => Document.QuerySelector(selector);

    public IElement Single(string selector) =>
        Document.QuerySelector(selector) ?? throw new Xunit.Sdk.XunitException($"No element matches '{selector}' on {Url}.");

    public IReadOnlyList<IElement> All(string selector) => Document.QuerySelectorAll(selector).ToList();

    /// <summary>The first POST form on the page.</summary>
    public IHtmlFormElement PostForm =>
        Document.Forms.FirstOrDefault(f => string.Equals(f.Method, "post", StringComparison.OrdinalIgnoreCase))
        ?? throw new Xunit.Sdk.XunitException($"No POST form on {Url}.");

    public string AntiforgeryToken =>
        PostForm.QuerySelector($"input[name='{AntiforgeryFieldName}']")?.GetAttribute("value")
        ?? throw new Xunit.Sdk.XunitException($"No antiforgery token in the form on {Url}.");

    public bool HasValidationError => Document.QuerySelector(".field-validation-error") is not null;

    /// <summary>
    /// The fields a browser would submit for the first POST form: named inputs (not buttons),
    /// selects (selected option, else the first) and textareas, including the antiforgery token.
    /// </summary>
    public Dictionary<string, string> FormFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in PostForm.QuerySelectorAll("input[name], select[name], textarea[name]"))
        {
            var name = element.GetAttribute("name")!;
            switch (element)
            {
                case IHtmlInputElement input when input.Type is "submit" or "button" or "image" or "reset":
                    break;
                case IHtmlInputElement input when input.Type is "checkbox" or "radio":
                    if (input.IsChecked)
                    {
                        fields[name] = input.Value;
                    }

                    break;
                case IHtmlInputElement input:
                    fields[name] = input.Value;
                    break;
                case IHtmlSelectElement select:
                    var option = select.Options.FirstOrDefault(o => o.IsSelected) ?? select.Options.FirstOrDefault();
                    if (option is not null)
                    {
                        fields[name] = option.Value;
                    }

                    break;
                case IHtmlTextAreaElement textArea:
                    fields[name] = textArea.Value;
                    break;
            }
        }

        return fields;
    }

    /// <summary>The (value, text) options of a select in the POST form.</summary>
    public IReadOnlyList<(string Value, string Text)> SelectOptions(string name)
    {
        var select = PostForm.QuerySelector($"select[name='{name}']") as IHtmlSelectElement
            ?? throw new Xunit.Sdk.XunitException($"No select '{name}' in the form on {Url}.");
        return select.Options.Select(o => (o.Value, o.Text.Trim())).ToList();
    }

    /// <summary>The visible text of the page body with whitespace collapsed.</summary>
    public string BodyText => Normalize(Document.Body?.TextContent ?? string.Empty);

    public static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
