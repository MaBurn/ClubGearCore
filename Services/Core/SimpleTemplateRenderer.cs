using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class SimpleTemplateRenderer : ITemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return template;
        }

        var output = template;
        foreach (var pair in values)
        {
            output = output
                .Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.OrdinalIgnoreCase)
                .Replace("<%" + pair.Key + "%>", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}
