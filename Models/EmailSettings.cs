namespace ClubGear.Models;

public class EmailSettings
{
    public const string SectionName = "Email";

    public string SenderName { get; set; } = "ClubGear";
    public string SenderAddress { get; set; } = "noreply@clubgear.local";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseStartTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UsePickupDirectory { get; set; } = true;
    public string PickupDirectory { get; set; } = "App_Data/MailDrop";
}
