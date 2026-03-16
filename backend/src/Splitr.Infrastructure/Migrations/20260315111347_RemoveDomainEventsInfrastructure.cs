using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Splitr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDomainEventsInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "settlements",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "delete_after",
                table: "groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_settlements_group_id_expires_at",
                table: "settlements",
                columns: new[] { "group_id", "expires_at" },
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_groups_delete_after",
                table: "groups",
                column: "delete_after",
                filter: "is_archived = true AND delete_after IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_settlements_group_id_expires_at",
                table: "settlements");

            migrationBuilder.DropIndex(
                name: "ix_groups_delete_after",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "settlements");

            migrationBuilder.DropColumn(
                name: "delete_after",
                table: "groups");
        }
    }
}
