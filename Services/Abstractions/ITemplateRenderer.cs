namespace ClubGear.Services.Abstractions;

public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, string>? values);
}
