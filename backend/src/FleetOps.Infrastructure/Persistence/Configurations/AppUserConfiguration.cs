using FleetOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(32).IsRequired();

        builder.HasIndex(u => u.Username).IsUnique();
    }
}
