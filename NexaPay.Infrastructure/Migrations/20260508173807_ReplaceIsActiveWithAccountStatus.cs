using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaPay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsActiveWithAccountStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Status with default Open(0) first so existing rows get a value.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Preserve inactive accounts as Closed(2).
            migrationBuilder.Sql(
                "UPDATE Accounts SET Status = 2 WHERE IsActive = 0");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Accounts");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Accounts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
