using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Splitr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "google_id",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_google_id",
                table: "users",
                column: "google_id",
                unique: true,
                filter: "google_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_google_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "google_id",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
