using backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.HasKey(tc => tc.Id);
        builder.Property(tc => tc.Id).HasMaxLength(500);

        builder.Property(tc => tc.Body)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(tc => tc.TicketId).HasMaxLength(500).IsRequired();
        builder.Property(tc => tc.AuthorId).HasMaxLength(500).IsRequired();

        builder.Property(tc => tc.CreatedBy).HasMaxLength(500);

        // real relationship — comment belongs to one ticket
        builder.HasOne(tc => tc.Ticket)
            .WithMany(t => t.Comments)
            .HasForeignKey(tc => tc.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tc => tc.Author)
            .WithMany(u => u.Comments)
            .HasForeignKey(tc => tc.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);   // keep authorship intact

        // MUST mirror the Ticket filter across the required relationship,
        // or EF warns about inconsistent query filters
        builder.HasQueryFilter(tc => !tc.Ticket.IsDeleted);
    }
}