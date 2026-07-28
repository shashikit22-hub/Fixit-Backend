using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationBotSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ServiceRequests",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AlternatePhone",
                table: "ServiceRequests",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HouseNumber",
                table: "ServiceRequests",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ServiceRequests",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ServiceRequests",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "ServiceRequests",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RatedAt",
                table: "ServiceRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "ServiceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestCode",
                table: "ServiceRequests",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "ServiceRequests",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentStep = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProfileName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SelectedServiceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhotoUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VideoUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitude = table.Column<double>(type: "double", nullable: true),
                    Longitude = table.Column<double>(type: "double", nullable: true),
                    AddressText = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AwaitingRatingForRequestId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$qZ4IMxP6dkt5Wjj3XnfpEelXwNvKcLt6.dEHE8A2CNu5FYKK1T0Va");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_RequestCode",
                table: "ServiceRequests",
                column: "RequestCode");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStates_PhoneNumber",
                table: "ConversationStates",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationStates");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_RequestCode",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "AlternatePhone",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "HouseNumber",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RatedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "RequestCode",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "ServiceRequests");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$6cAgtUpZIQ37MkBmVQjX7.ARMWPiPJI6jsb9WVuTLR9O3OYWnIgL6");
        }
    }
}
