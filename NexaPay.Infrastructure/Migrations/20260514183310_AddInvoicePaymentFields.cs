using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaPay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bankgiro",
                table: "Transactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ocr",
                table: "Transactions",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bankgiro",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Ocr",
                table: "Transactions");
        }
    }
}
