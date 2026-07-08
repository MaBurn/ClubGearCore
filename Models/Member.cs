using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubGear.Models;

public class Member
{
    public int Id { get; set; }

    [StringLength(50)]
    public string? Title { get; set; }

    [StringLength(100)]
    public string? OauthID { get; set; }

    [StringLength(100)]
    public string? OAuthUserName { get; set; }

    public bool IsVerified { get; set; }

    [StringLength(30)]
    public string? MemberNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(30)]
    public string? Gender { get; set; }

    public int? MembershipTypeId { get; set; }
    public MembershipType? MembershipType { get; set; }

    public List<MemberMetadataValue> MetadataValues { get; set; } = new();

    /// <summary>
    /// Transient, form-bound metadata values keyed by <see cref="MembershipTypeField.Key"/>,
    /// posted alongside <see cref="MembershipTypeId"/> from the member edit/create form.
    /// Not persisted directly; <see cref="Services.Core.MemberFeatureService"/> validates and
    /// encodes these into <see cref="MetadataValues"/> rows.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string?> MetadataInputs { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? Joined { get; set; }

    public DateTime? Leaved { get; set; }

    public DateTime? LastUpdated { get; set; }

    public bool IsDeceased { get; set; }

    public bool NotifyViaEmail { get; set; }

    public bool NotifyViaMatrix { get; set; }

    public bool DataprivacyAccepted { get; set; }

    public bool NewsletterConsent { get; set; }

    [StringLength(255)]
    public string? ProfileImagePath { get; set; }

    [StringLength(255)]
    public string? PendingEmail { get; set; }

    [StringLength(255)]
    public string? EmailVerificationToken { get; set; }

    public DateTime? EmailVerificationTokenExpiry { get; set; }

    [StringLength(2000)]
    public string? RentalPayoutOptions { get; set; }

    [StringLength(255)]
    public string? InitPassword { get; set; }

    [StringLength(255)]
    public string? KeycloakUsername { get; set; }

    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public List<MemberAddress> Addresses { get; set; } = new();

    public string FullName
    {
        get
        {
            var typeKey = MembershipType?.Key;

            if (string.Equals(typeKey, "Firma", StringComparison.Ordinal))
            {
                var companyName = GetMetadataValue("company_name");
                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    return companyName;
                }
            }
            else if (string.Equals(typeKey, "Verein", StringComparison.Ordinal))
            {
                var clubName = GetMetadataValue("club_name");
                if (!string.IsNullOrWhiteSpace(clubName))
                {
                    return clubName;
                }
            }

            return $"{FirstName} {LastName}".Trim();
        }
    }

    /// <summary>Display label for MemberReference metadata fields: "Nachname, Vorname, Mitgliedsnummer".</summary>
    public string ReferenceLabel => $"{LastName}, {FirstName}, {MemberNumber}";

    private string? GetMetadataValue(string fieldKey)
    {
        return MetadataValues
            .FirstOrDefault(v => v.Field is not null && string.Equals(v.Field.Key, fieldKey, StringComparison.Ordinal))
            ?.Value;
    }
}
