using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentStarted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Assignments",
                type: "datetime(6)",
                nullable: true);

            // Backfill: set StartedAt = AcceptedAt for already-completed assignments
            migrationBuilder.Sql("UPDATE Assignments SET StartedAt = AcceptedAt WHERE Status = 'Completed' AND AcceptedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Assignments");
        }
    }
}
