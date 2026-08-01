using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNameAndAreaToConversationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerArea",
                table: "ConversationStates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "ConversationStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$9fOesMfTDk5nbRl1HqJb4uZTASFjJyYsmuqyGkE2gV0C89LHhqRV2");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$DuCFTog9XUMmuE5N7xItG.GDbvdPMzY8MevqNU7LrAPDHtmCOXwYC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerArea",
                table: "ConversationStates");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "ConversationStates");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$6IWY5dCbiUFuoqflK2gfCuAWXkBMGvrTfg7W5SJylmyewyvkUx.Ue");

            migrationBuilder.UpdateData(
                table: "AdminUsers",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$3PfUd5vmUcbwLMqzfOJ2CefD.e0mTPEWwg3CkU.K.Ps2gHbX3zYmO");
        }
    }
}
