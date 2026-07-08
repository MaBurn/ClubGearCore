using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public class SelfServiceAddressInputViewModel
{
    public int Id { get; set; }

    [StringLength(255)]
    [Display(Name = "Strasse")]
    public string? Street { get; set; }

    [StringLength(20)]
    [Display(Name = "Postleitzahl")]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    [Display(Name = "Stadt")]
    public string? City { get; set; }

    [StringLength(100)]
    [Display(Name = "Land")]
    public string? Country { get; set; }

    [Display(Name = "Standardadresse")]
    public bool IsDefault { get; set; }
}

public class SelfServiceProfileViewModel
{
    [Display(Name = "Verknuepftes Mitglied")]
    public bool MemberLinked { get; set; }

    [StringLength(30)]
    [Display(Name = "Mitgliedsnummer")]
    public string? MemberNumber { get; set; }

    [Display(Name = "Mitglied aktiv")]
    public bool MemberActive { get; set; }

    [Display(Name = "Beigetreten")]
    public DateTime? MemberJoinedAt { get; set; }

    [StringLength(100)]
    [Display(Name = "Vorname")]
    public string? FirstName { get; set; }

    [StringLength(100)]
    [Display(Name = "Nachname")]
    public string? LastName { get; set; }

    [Display(Name = "Geburtsdatum")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Zuletzt aktualisiert")]
    public DateTime? LastUpdated { get; set; }

    [StringLength(255)]
    [Display(Name = "Profilbild")]
    public string? ProfileImagePath { get; set; }

    [StringLength(255)]
    [Display(Name = "Ausstehende E-Mail")]
    public string? PendingEmail { get; set; }

    [Display(Name = "Adressen")]
    public List<SelfServiceAddressInputViewModel> Addresses { get; set; } = new();

    [Required]
    [StringLength(150)]
    [Display(Name = "Vollstaendiger Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    [Display(Name = "E-Mail")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }
}
