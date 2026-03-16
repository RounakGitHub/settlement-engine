using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Splitr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "replaced_by_token_hash",
                table: "refresh_tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "token_family",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "invite_code",
                table: "groups",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_family",
                table: "refresh_tokens",
                column: "token_family");

            migrationBuilder.CreateIndex(
                name: "ix_groups_invite_code",
                table: "groups",
                column: "invite_code",
                unique: true,
                filter: "invite_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_events_aggregate_id_version",
                table: "events",
                columns: new[] { "aggregate_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token_family",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_groups_invite_code",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "users");

            migrationBuilder.DropColumn(
                name: "replaced_by_token_hash",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_family",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "invite_code",
                table: "groups");
        }
    }
}
