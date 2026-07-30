using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Assignments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Assignments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Assignments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_Status",
                table: "Assignments",
                column: "Status");

            // Data migration: set existing completed assignments to 'Completed', rest to 'Pending'
            migrationBuilder.Sql("UPDATE Assignments SET Status = 'Completed' WHERE CompletedAt IS NOT NULL;");
            migrationBuilder.Sql("UPDATE Assignments SET Status = 'Pending' WHERE CompletedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_Status",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Assignments");
        }
    }
}
