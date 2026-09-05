using backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Database.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasMaxLength(500);

        builder.Property(t => t.Subject)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(4000)
            .IsRequired();

        // enums stored as int (EF default) → priority sorts by severity for free.
        // API speaks strings via the global JsonStringEnumConverter.
        builder.Property(t => t.Status).IsRequired();
        builder.Property(t => t.Priority).IsRequired();

        // FK string lengths (match Id length)
        builder.Property(t => t.CategoryId).HasMaxLength(500).IsRequired();
        builder.Property(t => t.RequesterId).HasMaxLength(500).IsRequired();
        builder.Property(t => t.AssigneeId).HasMaxLength(500);   // nullable = unassigned

        builder.Property(t => t.CreatedBy).HasMaxLength(500);
        builder.Property(t => t.UpdatedBy).HasMaxLength(500);

        // the ONE real relationship we can wire now
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);   // don't orphan tickets when a category is removed

        // Two navigations onto the same table, so each FK is named explicitly —
        // EF can't infer which of Requester/Assignee owns which column.
        builder.HasOne(t => t.Requester)
            .WithMany(u => u.RequestedTickets)
            .HasForeignKey(t => t.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);   // a requester can't be hard-deleted out from under their tickets

        builder.HasOne(t => t.Assignee)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);    // losing an assignee just unassigns the ticket

        // soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);

        // indexes on the columns the centerpiece query filters/sorts on
        // Postgres owns the counter: EF never supplies a value, and the unique index is
        // what makes the reference safe to quote.
        builder.Property(t => t.Reference).ValueGeneratedOnAdd().UseIdentityByDefaultColumn();
        builder.HasIndex(t => t.Reference).IsUnique();

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.AssigneeId);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.ResolvedAt);   // every throughput/average metric filters on it
    }
}