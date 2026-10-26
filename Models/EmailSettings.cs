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

    // IMAP / Sent-folder visibility. Both features stay off until an admin configures a mailbox.
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public bool ImapUseSsl { get; set; } = true;
    public string? ImapUsername { get; set; }
    public string? ImapPassword { get; set; }
    public string SentFolder { get; set; } = "Sent";
    public bool AppendSentEnabled { get; set; }

    // Bounce detection. Off by default; the poll interval is clamped to >= 1 minute at runtime.
    public string BounceFolder { get; set; } = "INBOX";
    public bool BounceCheckEnabled { get; set; }
    public int BouncePollIntervalMinutes { get; set; } = 5;
}
