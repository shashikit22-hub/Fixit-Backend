using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminFullNameAndUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AdminUsers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FullName", "PasswordHash" },
                values: new object[] { "Admin", "$2a$11$4ymPXW88Uga6Iy3p47xL7ukIFCZWwnNo5JWW/sTbyAvrKVInwSxcq" });

            migrationBuilder.InsertData(
                table: "AdminUsers",
                columns: new[] { "Id", "FullName", "PasswordHash", "Role", "Username" },
                values: new object[] { 2, "VarunKumar", "$2a$11$WyRYg.qTeDJwzvTHs6Sm.uP/JivMCyM.VMOXxi7zj1SWqDGBKucka", "Admin", "admin@tinyfix.com" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AdminUsers");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$qZ4IMxP6dkt5Wjj3XnfpEelXwNvKcLt6.dEHE8A2CNu5FYKK1T0Va");
        }
    }
}
