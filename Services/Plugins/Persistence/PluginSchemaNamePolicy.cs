using System.Text;
using System.Text.RegularExpressions;
using ClubGear.Services;

namespace ClubGear.Services.Plugins.Persistence;

public sealed class PluginSchemaNamePolicy
{
    private static readonly Regex TableReferencePattern = new(
        @"(?:CREATE\s+TABLE(?:\s+IF\s+NOT\s+EXISTS)?|ALTER\s+TABLE|DROP\s+TABLE(?:\s+IF\s+EXISTS)?|INSERT\s+INTO|UPDATE|DELETE\s+FROM|FROM|JOIN)\s+(?<table>(?:""[^""]+""|\[[^\]]+\]|`[^`]+`|[A-Za-z_][A-Za-z0-9_]*))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string GetTablePrefix(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        return $"plugin_{NormalizeIdentifier(moduleId)}_";
    }

    public string GetTableName(string moduleId, string localName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localName);

        return GetTablePrefix(moduleId) + NormalizeIdentifier(localName);
    }

    public void ValidateSql(string moduleId, string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var prefix = GetTablePrefix(moduleId);
        foreach (Match match in TableReferencePattern.Matches(sql))
        {
            var tableName = UnwrapIdentifier(match.Groups["table"].Value);
            if (string.Equals(tableName, "sqlite_master", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException($"Plugin '{moduleId}' darf nur Tabellen mit dem Praefix '{prefix}' verwenden.");
            }
        }
    }

    private static string NormalizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasUnderscore = false;

        foreach (var character in value)
        {
            var normalized = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '_';

            if (normalized == '_' && previousWasUnderscore)
            {
                continue;
            }

            builder.Append(normalized);
            previousWasUnderscore = normalized == '_';
        }

        if (builder.Length == 0)
        {
            throw new UserFriendlyException("Tabellenname fuer Plugin-Persistenz ist ungueltig.");
        }

        if (builder[^1] == '_')
        {
            builder.Length--;
        }

        return builder.ToString();
    }

    private static string UnwrapIdentifier(string value)
    {
        if (value.Length >= 2)
        {
            if ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '[' && value[^1] == ']')
                || (value[0] == '`' && value[^1] == '`'))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}