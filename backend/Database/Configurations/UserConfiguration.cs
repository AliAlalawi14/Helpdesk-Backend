using backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(500);

        builder.Property(u => u.Name).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(300).IsRequired();
        builder.Property(u => u.IdentityId).HasMaxLength(500).IsRequired();

        // int column, like the ticket enums
        builder.Property(u => u.Role).IsRequired();

        builder.Property(u => u.CreatedBy).HasMaxLength(500);
        builder.Property(u => u.UpdatedBy).HasMaxLength(500);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.IdentityId).IsUnique();
        builder.HasIndex(u => u.Role);

        // soft delete, same as Category and Ticket
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
