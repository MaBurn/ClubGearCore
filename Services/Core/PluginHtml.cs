using AngleSharp.Dom;
using Ganss.Xss;

namespace ClubGear.Services.Core;

/// <summary>
/// CORE-31. The single place where plugin-supplied HTML is made safe before the host renders it
/// (ADR 0001 GR-008: raw plugin HTML is never trusted). The services that collect plugin HTML call
/// it, so a view never receives unsanitised plugin markup.
/// </summary>
/// <remarks>
/// Strict allowlist in two profiles:
/// <list type="bullet">
/// <item><see cref="SanitizeSection"/> for self-service sections, which render outside any host
/// form: form controls survive so host-handled <c>form[data-action-key]</c> forms keep working.</item>
/// <item><see cref="SanitizeEditTab"/> for member edit tabs, which render <b>inside</b> the host's
/// member edit form: no form controls at all, so plugin markup can never add fields to that form.
/// Buttons stay, forced to <c>type="button"</c> and without <c>name</c>/<c>value</c>, for host
/// action buttons (<c>data-plugin-member-action</c>).</item>
/// </list>
/// Both profiles remove scripts, styles, frames, event handlers, <c>style</c> and
/// <c>javascript:</c>/<c>data:</c> URLs, strip host-reserved attributes plugin markup must not fake,
/// and prefix every id (and the attributes that reference ids) so plugin markup cannot clobber host
/// elements.
/// </remarks>
public static class PluginHtml
{
    /// <summary>Prefix for every id inside plugin markup.</summary>
    public const string IdPrefix = "plugin-content-";

    /// <summary>
    /// Attributes the host itself uses to locate sections, endpoints and the owning module. Plugin
    /// markup may not carry them, so host JS can trust the nearest one it finds.
    /// </summary>
    private static readonly string[] ReservedAttributePrefixes =
    [
        "data-selfservice-", "data-module-id", "data-host-", "data-plugin-content-",
        // Values the host derives from its own wrapper; a plugin button must not choose them.
        "data-plugin-action-endpoint", "data-plugin-module-id", "data-plugin-member-id",
    ];

    private static readonly string[] IdReferenceAttributes =
    [
        "for", "list", "aria-labelledby", "aria-describedby", "aria-controls", "aria-owns",
        "aria-activedescendant", "aria-errormessage", "aria-details", "aria-flowto",
    ];

    private static readonly string[] FormControlTags =
    [
        "form", "input", "select", "option", "optgroup", "textarea", "datalist", "output",
    ];

    private static readonly HtmlSanitizer SectionSanitizer = Create(allowFormControls: true);
    private static readonly HtmlSanitizer EditTabSanitizer = Create(allowFormControls: false);

    public static string SanitizeSection(string? html)
        => string.IsNullOrEmpty(html) ? string.Empty : SectionSanitizer.Sanitize(html);

    public static string SanitizeEditTab(string? html)
        => string.IsNullOrEmpty(html) ? string.Empty : EditTabSanitizer.Sanitize(html);

    private static HtmlSanitizer Create(bool allowFormControls)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "div", "span", "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6",
            "strong", "em", "b", "i", "u", "s", "small", "mark", "code", "pre", "kbd", "blockquote",
            "ul", "ol", "li", "dl", "dt", "dd",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "colgroup", "col",
            "a", "img", "figure", "figcaption", "details", "summary", "time", "abbr",
            "button", "label", "fieldset", "legend", "progress", "meter",
        };
        if (allowFormControls)
        {
            tags.UnionWith(FormControlTags);
        }

        var attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "class", "id", "title", "role", "lang", "dir", "hidden", "tabindex",
            "href", "target", "rel", "src", "alt", "width", "height", "datetime",
            "colspan", "rowspan", "scope", "headers", "for", "type", "disabled", "open", "label",
        };
        if (allowFormControls)
        {
            attributes.UnionWith(
            [
                "name", "value", "placeholder", "required", "readonly", "checked", "selected", "multiple",
                "min", "max", "step", "minlength", "maxlength", "pattern", "list", "autocomplete",
                "rows", "cols", "size",
            ]);
        }

        var options = new HtmlSanitizerOptions
        {
            AllowedTags = tags,
            AllowedAttributes = attributes,
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
        };

        var sanitizer = new HtmlSanitizer(options)
        {
            AllowDataAttributes = true,
        };

        // aria-* is safe text metadata, but the library has no wildcard for it.
        sanitizer.RemovingAttribute += (_, e) =>
        {
            if (e.Attribute.Name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
            }
        };

        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
            {
                return;
            }

            foreach (var name in element.Attributes.Select(a => a.Name).ToArray())
            {
                if (ReservedAttributePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    element.RemoveAttribute(name);
                }
            }

            PrefixIds(element);

            if (!allowFormControls && element.LocalName == "button")
            {
                // Inside the host's member edit form a button must never submit it.
                element.SetAttribute("type", "button");
            }
        };

        return sanitizer;
    }

    private static void PrefixIds(IElement element)
    {
        if (element.GetAttribute("id") is { Length: > 0 } id)
        {
            element.SetAttribute("id", IdPrefix + id);
        }

        foreach (var attribute in IdReferenceAttributes)
        {
            if (element.GetAttribute(attribute) is { Length: > 0 } references)
            {
                element.SetAttribute(attribute, string.Join(' ',
                    references.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(r => IdPrefix + r)));
            }
        }

        if (element.GetAttribute("href") is { Length: > 1 } href && href[0] == '#')
        {
            element.SetAttribute("href", "#" + IdPrefix + href[1..]);
        }
    }
}
