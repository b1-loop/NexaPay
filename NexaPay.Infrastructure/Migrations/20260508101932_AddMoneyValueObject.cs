using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaPay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoneyValueObject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Amount_Currency",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BalanceAfterTransaction_Currency",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AccountCurrency",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount_Currency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BalanceAfterTransaction_Currency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountCurrency",
                table: "Accounts");
        }
    }
}
