using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Member.
/// </summary>
/// <remarks>
/// A member holds exactly one BranchId and one RoleId. MembershipStage
/// is free text, not a lookup. Email is the only optional field.
///
/// Specification: §4 (Member), §9.1.7.
/// </remarks>
public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(e => e.MemberId);

        builder.Property(e => e.JoinDate).IsRequired();
        builder.Property(e => e.DateOfBirth).IsRequired();
        builder.Property(e => e.MembershipStage).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Surname).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Gender).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Phone).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.BranchId).IsRequired();
        builder.Property(e => e.RoleId).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.BranchId).HasDatabaseName("ix_member_branch");
        builder.HasIndex(e => e.RoleId).HasDatabaseName("ix_member_role");
    }
}
