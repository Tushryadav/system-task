using Microsoft.EntityFrameworkCore;
using SRAAS.Api.Entities;
using SRAAS.Api.Enums;

namespace SRAAS.Api.Data;

public class SraasDbContext : DbContext
{
    public SraasDbContext(DbContextOptions<SraasDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<App> Apps => Set<App>();
    public DbSet<OrgMember> OrgMembers => Set<OrgMember>();
    public DbSet<OrgInvite> OrgInvites => Set<OrgInvite>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
    public DbSet<AppMember> AppMembers => Set<AppMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Map PostgreSQL enums to lowercase strings ───
        modelBuilder.HasPostgresEnum<AppTypeEnum>("app_type_enum");
        modelBuilder.HasPostgresEnum<MemberRoleEnum>("member_role_enum");
        modelBuilder.HasPostgresEnum<MemberStatusEnum>("member_status_enum");
        modelBuilder.HasPostgresEnum<InviteTypeEnum>("invite_type_enum");
        modelBuilder.HasPostgresEnum<ChannelTypeEnum>("channel_type_enum");
        modelBuilder.HasPostgresEnum<ContentTypeEnum>("content_type_enum");

        // ═══════════════════════════════════════════════════
        //  ORGANIZATIONS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Slug).HasColumnName("slug").IsRequired();
            entity.Property(e => e.SeatLimit).HasColumnName("seat_limit").HasDefaultValue(10);
            entity.Property(e => e.SeatsUsed).HasColumnName("seats_used").HasDefaultValue(0);
            entity.Property(e => e.Settings).HasColumnName("settings").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // ═══════════════════════════════════════════════════
        //  APPS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<App>(entity =>
        {
            entity.ToTable("apps");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.AppType).HasColumnName("app_type").HasDefaultValue(AppTypeEnum.Chat);
            entity.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Apps)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ═══════════════════════════════════════════════════
        //  ORG MEMBERS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<OrgMember>(entity =>
        {
            entity.ToTable("org_members");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.InviteId).HasColumnName("invite_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasDefaultValue(MemberRoleEnum.Member);
            entity.Property(e => e.Status).HasColumnName("status").HasDefaultValue(MemberStatusEnum.Active);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.OrgMembers)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Invite)
                  .WithMany(i => i.JoinedMembers)
                  .HasForeignKey(e => e.InviteId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.OrgId, e.Email }).IsUnique();
            entity.HasIndex(e => new { e.OrgId, e.IsActive }).HasDatabaseName("idx_org_members_org_active");
        });

        // ═══════════════════════════════════════════════════
        //  ORG INVITES
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<OrgInvite>(entity =>
        {
            entity.ToTable("org_invites");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.InviteCode).HasColumnName("invite_code").IsRequired();
            entity.Property(e => e.InviteType).HasColumnName("invite_type").HasDefaultValue(InviteTypeEnum.Multi);
            entity.Property(e => e.MaxUses).HasColumnName("max_uses").HasDefaultValue(1);
            entity.Property(e => e.UsedCount).HasColumnName("used_count").HasDefaultValue(0);
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.OrgInvites)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.InviteCode).IsUnique();
            entity.HasIndex(e => e.InviteCode)
                  .HasDatabaseName("idx_invites_code")
                  .HasFilter("is_active = true");
        });

        // ═══════════════════════════════════════════════════
        //  REFRESH TOKENS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.MemberId).HasColumnName("member_id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(e => e.DeviceInfo).HasColumnName("device_info").HasColumnType("jsonb");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.LastUsedAt).HasColumnName("last_used_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Member)
                  .WithMany(m => m.RefreshTokens)
                  .HasForeignKey(e => e.MemberId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.RefreshTokens)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TokenHash)
                  .HasDatabaseName("idx_refresh_tokens_hash")
                  .HasFilter("is_revoked = false");
        });

        // ═══════════════════════════════════════════════════
        //  CHANNELS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<Channel>(entity =>
        {
            entity.ToTable("channels");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AppId).HasColumnName("app_id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.ChannelType).HasColumnName("channel_type").HasDefaultValue(ChannelTypeEnum.General);
            entity.Property(e => e.IsPrivate).HasColumnName("is_private").HasDefaultValue(false);
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.App)
                  .WithMany(a => a.Channels)
                  .HasForeignKey(e => e.AppId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Channels)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.AppId).HasDatabaseName("idx_channels_app");
        });

        // ═══════════════════════════════════════════════════
        //  CHANNEL MEMBERS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<ChannelMember>(entity =>
        {
            entity.ToTable("channel_members");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.OrgMemberId).HasColumnName("org_member_id");
            entity.Property(e => e.LastReadAt).HasColumnName("last_read_at").HasDefaultValueSql("now()");
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Channel)
                  .WithMany(c => c.ChannelMembers)
                  .HasForeignKey(e => e.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OrgMember)
                  .WithMany(m => m.ChannelMemberships)
                  .HasForeignKey(e => e.OrgMemberId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ChannelId, e.OrgMemberId }).IsUnique();
        });

        // ═══════════════════════════════════════════════════
        //  APP MEMBERS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<AppMember>(entity =>
        {
            entity.ToTable("app_members");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AppId).HasColumnName("app_id");
            entity.Property(e => e.OrgMemberId).HasColumnName("org_member_id");
            entity.Property(e => e.Role).HasColumnName("role").HasDefaultValue("member");
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.App)
                  .WithMany(a => a.AppMembers)
                  .HasForeignKey(e => e.AppId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OrgMember)
                  .WithMany(m => m.AppMemberships)
                  .HasForeignKey(e => e.OrgMemberId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.AppId, e.OrgMemberId }).IsUnique();
        });

        // ═══════════════════════════════════════════════════
        //  MESSAGES
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.ReplyToId).HasColumnName("reply_to_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ContentType).HasColumnName("content_type").HasDefaultValue(ContentTypeEnum.Text);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.IsEdited).HasColumnName("is_edited").HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Channel)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(e => e.ChannelId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.Messages)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sender)
                  .WithMany(m => m.Messages)
                  .HasForeignKey(e => e.SenderId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ReplyTo)
                  .WithMany(m => m.Replies)
                  .HasForeignKey(e => e.ReplyToId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.ChannelId, e.CreatedAt })
                  .IsDescending(false, true)
                  .HasDatabaseName("idx_messages_channel_created")
                  .HasFilter("is_deleted = false");

            entity.HasIndex(e => e.OrgId).HasDatabaseName("idx_messages_org");
        });

        // ═══════════════════════════════════════════════════
        //  MESSAGE ATTACHMENTS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.ToTable("message_attachments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.FileName).HasColumnName("file_name").IsRequired();
            entity.Property(e => e.FileType).HasColumnName("file_type").IsRequired();
            entity.Property(e => e.FileSizeKb).HasColumnName("file_size_kb");
            entity.Property(e => e.StorageKey).HasColumnName("storage_key").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Message)
                  .WithMany(m => m.Attachments)
                  .HasForeignKey(e => e.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.MessageAttachments)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════════════
        //  MESSAGE REACTIONS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<MessageReaction>(entity =>
        {
            entity.ToTable("message_reactions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.MessageId).HasColumnName("message_id");
            entity.Property(e => e.OrgMemberId).HasColumnName("org_member_id");
            entity.Property(e => e.Emoji).HasColumnName("emoji").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Message)
                  .WithMany(m => m.Reactions)
                  .HasForeignKey(e => e.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.OrgMember)
                  .WithMany(m => m.Reactions)
                  .HasForeignKey(e => e.OrgMemberId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MessageId, e.OrgMemberId, e.Emoji }).IsUnique();
            entity.HasIndex(e => e.MessageId).HasDatabaseName("idx_reactions_message");
        });

        // ═══════════════════════════════════════════════════
        //  AUDIT LOGS
        // ═══════════════════════════════════════════════════
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OrgId).HasColumnName("org_id");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.Action).HasColumnName("action").IsRequired();
            entity.Property(e => e.TargetType).HasColumnName("target_type");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            entity.HasOne(e => e.Organization)
                  .WithMany(o => o.AuditLogs)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Actor)
                  .WithMany(m => m.AuditLogs)
                  .HasForeignKey(e => e.ActorId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.OrgId, e.CreatedAt })
                  .IsDescending(false, true)
                  .HasDatabaseName("idx_audit_logs_org_created");
        });
    }
}
