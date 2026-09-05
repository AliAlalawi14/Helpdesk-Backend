using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations.Application;
    /// <inheritdoc />
    public partial class AddDomainUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                schema: "ticket",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    identity_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            // Backfill before the foreign keys go on, or every existing ticket and
            // comment fails the new constraint and the migration rolls back.
            //
            // The domain id is set to the identity id: today's identity rows already
            // use the ids the seeded tickets point at (usr_jordan, usr_sam, ...), so
            // reusing them is what makes requester_id/assignee_id/author_id resolve.
            //
            // Guarded by to_regclass because the app context migrates BEFORE the
            // identity context — on a fresh database the identity tables don't exist
            // yet and there is nothing to backfill.
            //
            // DISTINCT ON keeps one row per user if anyone ended up with two roles in
            // the join table; ORDER BY takes the highest.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('identity.asp_net_users') IS NOT NULL THEN
                        INSERT INTO ticket.users
                            (id, name, email, role, is_active, identity_id,
                             is_deleted, created_at, updated_at)
                        SELECT DISTINCT ON (u.id)
                            u.id,
                            COALESCE(u.name, u.email, u.id),
                            COALESCE(u.email, u.user_name, u.id),
                            CASE r.name
                                WHEN 'admin' THEN 2
                                WHEN 'moderator' THEN 1
                                ELSE 0
                            END,
                            COALESCE(u.is_active, TRUE),
                            u.id,
                            FALSE,
                            COALESCE(u.created_at, NOW()),
                            NOW()
                        FROM identity.asp_net_users u
                        LEFT JOIN identity.asp_net_user_roles ur ON ur.user_id = u.id
                        LEFT JOIN identity.asp_net_roles r ON r.id = ur.role_id
                        ORDER BY u.id, CASE r.name
                            WHEN 'admin' THEN 2
                            WHEN 'moderator' THEN 1
                            ELSE 0
                        END DESC;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_requester_id",
                schema: "ticket",
                table: "tickets",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comments_author_id",
                schema: "ticket",
                table: "ticket_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "ticket",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_identity_id",
                schema: "ticket",
                table: "users",
                column: "identity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_role",
                schema: "ticket",
                table: "users",
                column: "role");

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_comments_users_author_id",
                schema: "ticket",
                table: "ticket_comments",
                column: "author_id",
                principalSchema: "ticket",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_assignee_id",
                schema: "ticket",
                table: "tickets",
                column: "assignee_id",
                principalSchema: "ticket",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "ticket",
                table: "tickets",
                column: "requester_id",
                principalSchema: "ticket",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_comments_users_author_id",
                schema: "ticket",
                table: "ticket_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_assignee_id",
                schema: "ticket",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "ticket",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "users",
                schema: "ticket");

            migrationBuilder.DropIndex(
                name: "ix_tickets_requester_id",
                schema: "ticket",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_ticket_comments_author_id",
                schema: "ticket",
                table: "ticket_comments");
        }
    }
