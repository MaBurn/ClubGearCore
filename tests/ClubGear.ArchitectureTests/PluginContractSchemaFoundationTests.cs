using System.Text.Json;
using ClubGear.Plugin.Contracts;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginContractSchemaFoundationTests
{
    [Fact]
    public void MemberActionSlot_DeserializesLegacyPayload_WithoutArgumentSchema()
    {
        const string json = """
            {
              "key": "carinfo.add",
              "label": "Fahrzeug anlegen",
              "permissionKey": "members.manage",
              "style": "outline-primary",
              "order": 10,
              "confirmMessage": "Aktion ausfuehren?"
            }
            """;

        var slot = Deserialize<MemberActionSlot>(json);

        Assert.Equal("carinfo.add", slot.Key);
        Assert.Null(slot.ArgumentSchema);
    }

    [Fact]
    public void MemberActionSlot_RoundTrips_WithArgumentSchemaMetadata()
    {
        var slot = new MemberActionSlot(
            "carinfo.add",
            "Fahrzeug anlegen",
            "members.manage",
            "outline-primary",
            5,
            "Jetzt speichern?",
            [
                new PluginFieldSchema(
                    "licensePlate",
                    "Kennzeichen",
                    PluginSchemaFieldType.Text,
                    true,
                    0,
                    "Pflichtfeld",
                    "B-AB 123",
                    new PluginFieldSchemaConstraint(
                        MinLength: 2,
                        MaxLength: 16,
                        RegexPattern: "^[A-Za-z0-9\\- ]+$",
                        CustomMessage: "Ungueltiges Kennzeichen."))
            ]);

        var json = Serialize(slot);
        var roundTrip = Deserialize<MemberActionSlot>(json);

        var schemaField = Assert.Single(roundTrip.ArgumentSchema!);
        Assert.Equal("licensePlate", schemaField.Key);
        Assert.True(schemaField.Required);
        Assert.Equal("^[A-Za-z0-9\\- ]+$", schemaField.Constraints!.RegexPattern);
        Assert.Equal("Ungueltiges Kennzeichen.", schemaField.Constraints.CustomMessage);
    }

    [Fact]
    public void PluginMemberActionResult_DeserializesLegacyPayload_WithoutFieldErrors()
    {
        const string json = """
            {
              "success": false,
              "status": "invalid",
              "message": "Validierung fehlgeschlagen"
            }
            """;

        var result = Deserialize<PluginMemberActionResult>(json);

        Assert.False(result.Success);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public void ActionAndCommandResults_SerializeNormalizedFieldErrors()
    {
        var fieldErrors = new[]
        {
            new PluginFieldError("licensePlate", "Kennzeichen ist erforderlich.", "required")
        };

        var action = new PluginMemberActionResult(false, "invalid", "Bitte Eingabe pruefen.", fieldErrors);
        var command = new PluginCommandResult(false, "invalid", "Bitte Eingabe pruefen.", fieldErrors);

        var actionRoundTrip = Deserialize<PluginMemberActionResult>(Serialize(action));
        var commandRoundTrip = Deserialize<PluginCommandResult>(Serialize(command));

        Assert.Equal("licensePlate", Assert.Single(actionRoundTrip.FieldErrors!).FieldKey);
        Assert.Equal("required", Assert.Single(commandRoundTrip.FieldErrors!).Code);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("Unable to deserialize test payload.");
}