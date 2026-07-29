using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Technicians",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Technicians",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GovtIdNumber",
                table: "Technicians",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAt",
                table: "Technicians",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Technicians",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Technicians",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$zU50wTJ2u3kHeh0UfrCln.ocLXKr/YwmvxOvkIahgrlj9PdwKBcn2");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$sB9mCPYd.3.vWJfTyQU34esMfB7.xw0g17bhGEt6kyX2gyvbmBxU6");

            migrationBuilder.CreateIndex(
                name: "IX_Technicians_Email",
                table: "Technicians",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Technicians_Email",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "GovtIdNumber",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Technicians");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Technicians");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$4ymPXW88Uga6Iy3p47xL7ukIFCZWwnNo5JWW/sTbyAvrKVInwSxcq");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$WyRYg.qTeDJwzvTHs6Sm.uP/JivMCyM.VMOXxi7zj1SWqDGBKucka");
        }
    }
}
