using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class MessageComposer : IMessageComposer
{
    private readonly ITemplateRenderer _templateRenderer;

    public MessageComposer(ITemplateRenderer templateRenderer)
    {
        _templateRenderer = templateRenderer;
    }

    public (string Subject, string Body) Compose(
        string subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string>? values = null)
    {
        var subject = _templateRenderer.Render(subjectTemplate, values);
        var body = _templateRenderer.Render(bodyTemplate, values);
        return (subject, body);
    }
}
