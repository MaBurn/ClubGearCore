using System.Globalization;
using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

/// <summary>
/// Stateless helper validating and encoding/decoding per-<see cref="MembershipTypeField"/>
/// metadata values posted alongside a member's chosen <see cref="MembershipTypeId"/>.
/// </summary>
public sealed class MemberMetadataService : IMemberMetadataService
{
    public MemberMetadataValidationOutcome ValidateAndEncode(
        IReadOnlyList<MembershipTypeField> fields,
        IReadOnlyDictionary<string, string?> postedValues,
        IReadOnlyCollection<int>? existingMemberIds = null,
        MemberReferenceIntegrityContext? referenceContext = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        postedValues ??= new Dictionary<string, string?>();

        var errors = new List<string>();
        var encoded = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            postedValues.TryGetValue(field.Key, out var rawValue);
            rawValue = string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();

            if (rawValue is null)
            {
                if (field.FieldType == MemberMetadataFieldType.Boolean)
                {
                    // Unchecked checkboxes simply post nothing; encode as an explicit "false".
                    encoded[field.Key] = "false";
                    continue;
                }

                if (field.IsRequired)
                {
                    errors.Add($"Das Feld '{field.Label}' ist erforderlich.");
                }

                encoded[field.Key] = null;
                continue;
            }

            switch (field.FieldType)
            {
                case MemberMetadataFieldType.Text:
                    encoded[field.Key] = rawValue;
                    break;

                case MemberMetadataFieldType.Number:
                    if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        errors.Add($"Das Feld '{field.Label}' muss eine Zahl sein.");
                        break;
                    }

                    encoded[field.Key] = number.ToString(CultureInfo.InvariantCulture);
                    break;

                case MemberMetadataFieldType.Boolean:
                    if (!TryParseBoolean(rawValue, out var boolValue))
                    {
                        errors.Add($"Das Feld '{field.Label}' muss ein Wahrheitswert (ja/nein) sein.");
                        break;
                    }

                    encoded[field.Key] = boolValue ? "true" : "false";
                    break;

                case MemberMetadataFieldType.Date:
                    if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        errors.Add($"Das Feld '{field.Label}' muss ein gueltiges Datum sein.");
                        break;
                    }

                    encoded[field.Key] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    break;

                case MemberMetadataFieldType.MemberReference:
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memberId))
                    {
                        errors.Add($"Das Feld '{field.Label}' muss eine gueltige Mitglieds-ID sein.");
                        break;
                    }

                    if (existingMemberIds is not null && !existingMemberIds.Contains(memberId))
                    {
                        errors.Add($"Das Feld '{field.Label}' verweist auf ein nicht vorhandenes Mitglied.");
                        break;
                    }

                    if (referenceContext is not null)
                    {
                        // Single-level hierarchy integrity: prevent self-reference, grandchildren,
                        // and 2-cycles. See 04_design.md §2.3.
                        if (referenceContext.SelfId is int selfId && memberId == selfId)
                        {
                            errors.Add($"Das Feld '{field.Label}' darf nicht auf sich selbst verweisen.");
                            break;
                        }

                        if (referenceContext.ExistingSubMemberIds.Contains(memberId))
                        {
                            errors.Add($"Das Feld '{field.Label}' verweist auf ein Mitglied, das selbst ein Untermitglied ist.");
                            break;
                        }

                        if (referenceContext.SelfId is int self && referenceContext.ExistingParentIds.Contains(self))
                        {
                            errors.Add($"Dieses Mitglied hat bereits Untermitglieder und kann daher nicht selbst zugeordnet werden.");
                            break;
                        }
                    }

                    encoded[field.Key] = memberId.ToString(CultureInfo.InvariantCulture);
                    break;

                default:
                    encoded[field.Key] = rawValue;
                    break;
            }
        }

        return errors.Count > 0
            ? MemberMetadataValidationOutcome.Failed(errors)
            : MemberMetadataValidationOutcome.Ok(encoded);
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "ja":
            case "yes":
            case "on":
                result = true;
                return true;
            case "0":
            case "false":
            case "nein":
            case "no":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }
}
