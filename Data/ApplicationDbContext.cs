using ClubGear.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberAddress> MemberAddresses => Set<MemberAddress>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();
    public DbSet<SystemEventLog> SystemEventLogs => Set<SystemEventLog>();
    public DbSet<AppPermission> Permissions => Set<AppPermission>();
    public DbSet<AppRolePermission> RolePermissions => Set<AppRolePermission>();
    public DbSet<NotificationRecord> NotificationRecords => Set<NotificationRecord>();
    public DbSet<PluginStatusRecord> PluginStatusRecords => Set<PluginStatusRecord>();
    public DbSet<PluginMigrationState> PluginMigrationStates => Set<PluginMigrationState>();
    public DbSet<SystemConfigEntry> SystemConfigEntries => Set<SystemConfigEntry>();
    public DbSet<MembershipType> MembershipTypes => Set<MembershipType>();
    public DbSet<MembershipTypeField> MembershipTypeFields => Set<MembershipTypeField>();
    public DbSet<MemberMetadataValue> MemberMetadataValues => Set<MemberMetadataValue>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppPermission>()
            .HasIndex(p => p.Key)
            .IsUnique();

        builder.Entity<AppRolePermission>()
            .HasIndex(rp => new { rp.RoleName, rp.PermissionKey })
            .IsUnique();

        builder.Entity<NotificationRecord>()
            .HasIndex(n => new { n.Channel, n.CreatedAtUtc });

        builder.Entity<PluginStatusRecord>()
            .HasIndex(p => p.Key)
            .IsUnique();

        builder.Entity<PluginMigrationState>()
            .HasIndex(state => new { state.PluginKey, state.MigrationId })
            .IsUnique();

        builder.Entity<PluginMigrationState>()
            .HasIndex(state => state.PluginKey);

        builder.Entity<SystemConfigEntry>()
            .HasIndex(entry => new { entry.Section, entry.Name })
            .IsUnique();

        builder.Entity<Member>()
            .HasIndex(m => m.MemberNumber)
            .IsUnique();

        builder.Entity<Member>()
            .HasIndex(m => m.ApplicationUserId)
            .IsUnique();

        builder.Entity<Member>()
            .HasIndex(m => m.OauthID)
            .IsUnique();

        builder.Entity<MemberAddress>()
            .HasOne(a => a.Member)
            .WithMany(m => m.Addresses)
            .HasForeignKey(a => a.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MembershipType>()
            .HasIndex(t => t.Key)
            .IsUnique();

        // Sub-member container flag: a NOT NULL bool with a SQL DEFAULT 0 so that
        // EF's EnsureCreated schema matches the idempotent migration
        // (Data/Migrations/202607080101_AddSubMemberHierarchy.cs, ADD COLUMN ... NOT NULL DEFAULT 0)
        // and the AddMembershipTypeModel seed INSERTs (which omit this column) keep working.
        builder.Entity<MembershipType>()
            .Property(t => t.AllowsSubMembers)
            .HasDefaultValue(false);

        builder.Entity<MembershipTypeField>()
            .HasIndex(f => new { f.MembershipTypeId, f.Key })
            .IsUnique();

        builder.Entity<MembershipTypeField>()
            .HasOne(f => f.MembershipType)
            .WithMany(t => t.Fields)
            .HasForeignKey(f => f.MembershipTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MemberMetadataValue>()
            .HasIndex(v => new { v.MemberId, v.FieldId })
            .IsUnique();

        builder.Entity<MemberMetadataValue>()
            .HasOne(v => v.Field)
            .WithMany()
            .HasForeignKey(v => v.FieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MemberMetadataValue>()
            .HasOne<Member>()
            .WithMany(m => m.MetadataValues)
            .HasForeignKey(v => v.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Member>()
            .HasOne(m => m.MembershipType)
            .WithMany()
            .HasForeignKey(m => m.MembershipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Legacy pre-MembershipType columns: physically NOT NULL on databases that predate this
        // feature (see Data/Migrations/202607070101_AddMembershipTypeModel.cs). No longer exposed
        // on Member.cs, but must stay mapped as shadow properties so every INSERT still supplies an
        // explicit value - otherwise SQLite rejects new Member rows with a NOT NULL violation.
        builder.Entity<Member>().Property<bool>("IsCompany");
        builder.Entity<Member>().Property<bool>("IsClub");
        builder.Entity<Member>().Property<bool>("FamilyMembership");
    }
}
