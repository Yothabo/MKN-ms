
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Member.
/// </summary>
/// <remarks>
/// MemberStatusId is non-null as of Step 7. Member.IsActive is
/// retained until Step 8.
///
/// Specification: §4 (Member), §9.1.7, §15.8.1, §15.8.2, §15.8.3,
/// §15.8.27.
/// </remarks>
public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(e => e.MemberId);

        builder.Property(e => e.ReceiptNumber).HasMaxLength(100);
        builder.Property(e => e.CardNumber).HasMaxLength(100);
        builder.Property(e => e.JoinDate).IsRequired();
        builder.Property(e => e.JoinReason).HasMaxLength(1000);
        builder.Property(e => e.DateOfBirth).IsRequired();
        builder.Property(e => e.MembershipStage).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Surname).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Gender).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Phone).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.BranchId).IsRequired();
        builder.Property(e => e.RoleId).IsRequired();
        builder.Property(e => e.MemberStatusId).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.MemberStatus)
            .WithMany()
            .HasForeignKey(e => e.MemberStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.BranchId).HasDatabaseName("ix_member_branch");
        builder.HasIndex(e => e.RoleId).HasDatabaseName("ix_member_role");
        builder.HasIndex(e => e.MemberStatusId).HasDatabaseName("ix_member_status");
    }
}
