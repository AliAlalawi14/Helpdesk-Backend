using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations.Application;
    /// <inheritdoc />
    public partial class AddTicketResolvedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                schema: "ticket",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_resolved_at",
                schema: "ticket",
                table: "tickets",
                column: "resolved_at");

            // Backfill tickets that are already terminal, so the metrics endpoints
            // have real history to aggregate instead of an all-null column.
            //
            // Status is stored as int: 2 = Resolved, 3 = Closed.
            //
            // The interval is derived from hashtext(id) rather than a constant so the
            // spread is 3-47 hours instead of one flat number — a fixed offset would
            // make every average identical and hide whether the metric works at all.
            // It's deterministic, so re-running on a restored dump gives the same data.
            //
            // LEAST(..., now()) keeps a recently-created ticket from being stamped as
            // resolved in the future.
            migrationBuilder.Sql("""
                UPDATE ticket.tickets
                SET resolved_at = LEAST(
                        created_at + make_interval(hours => 3 + mod(abs(hashtext(id)::bigint), 45)::int),
                        now())
                WHERE status IN (2, 3)
                  AND resolved_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_resolved_at",
                schema: "ticket",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                schema: "ticket",
                table: "tickets");
        }
    }
