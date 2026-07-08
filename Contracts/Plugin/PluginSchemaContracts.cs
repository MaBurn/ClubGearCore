namespace ClubGear.Plugin.Contracts;

public enum PluginSchemaFieldType
{
    Text,
    MultilineText,
    Number,
    Date,
    Boolean,
    Select,
    Secret
}

public sealed record PluginFieldSchemaOption(
    string Value,
    string Label);

public sealed record PluginFieldSchemaConstraint(
    decimal? Min = null,
    decimal? Max = null,
    int? MinLength = null,
    int? MaxLength = null,
    string? RegexPattern = null,
    string? DateMin = null,
    string? DateMax = null,
    string? CustomMessage = null);

public sealed record PluginFieldSchema(
    string Key,
    string Label,
    PluginSchemaFieldType InputType = PluginSchemaFieldType.Text,
    bool Required = false,
    int Order = 0,
    string? HelpText = null,
    string? Placeholder = null,
    PluginFieldSchemaConstraint? Constraints = null,
    IReadOnlyList<PluginFieldSchemaOption>? Options = null);

public sealed record PluginFieldError(
    string FieldKey,
    string Message,
    string? Code = null);