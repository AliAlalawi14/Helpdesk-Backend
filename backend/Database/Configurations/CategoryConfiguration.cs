using Microsoft.EntityFrameworkCore;
using backend.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(500);

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CreatedBy).HasMaxLength(500);
        builder.Property(c => c.UpdatedBy).HasMaxLength(500);

        // soft delete — your explicit choice
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => c.Name);
    }
}