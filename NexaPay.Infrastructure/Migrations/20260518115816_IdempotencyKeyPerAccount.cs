using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexaPay.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdempotencyKeyPerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey_AccountId",
                table: "Transactions",
                columns: new[] { "IdempotencyKey", "AccountId" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey_AccountId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }
    }
}
