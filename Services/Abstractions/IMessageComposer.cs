namespace ClubGear.Services.Abstractions;

public interface IMessageComposer
{
    (string Subject, string Body) Compose(
        string subjectTemplate,
        string bodyTemplate,
        IReadOnlyDictionary<string, string>? values = null);
}
