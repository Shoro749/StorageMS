using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class OutgoingRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutgoingItems_OutgoingRequest_RequestId",
                table: "OutgoingItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OutgoingRequest_Users_CreatedById",
                table: "OutgoingRequest");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OutgoingRequest",
                table: "OutgoingRequest");

            migrationBuilder.RenameTable(
                name: "OutgoingRequest",
                newName: "OutgoingRequests");

            migrationBuilder.RenameIndex(
                name: "IX_OutgoingRequest_CreatedById",
                table: "OutgoingRequests",
                newName: "IX_OutgoingRequests_CreatedById");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutgoingRequests",
                table: "OutgoingRequests",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutgoingItems_OutgoingRequests_RequestId",
                table: "OutgoingItems",
                column: "RequestId",
                principalTable: "OutgoingRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OutgoingRequests_Users_CreatedById",
                table: "OutgoingRequests",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OutgoingItems_OutgoingRequests_RequestId",
                table: "OutgoingItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OutgoingRequests_Users_CreatedById",
                table: "OutgoingRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OutgoingRequests",
                table: "OutgoingRequests");

            migrationBuilder.RenameTable(
                name: "OutgoingRequests",
                newName: "OutgoingRequest");

            migrationBuilder.RenameIndex(
                name: "IX_OutgoingRequests_CreatedById",
                table: "OutgoingRequest",
                newName: "IX_OutgoingRequest_CreatedById");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutgoingRequest",
                table: "OutgoingRequest",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OutgoingItems_OutgoingRequest_RequestId",
                table: "OutgoingItems",
                column: "RequestId",
                principalTable: "OutgoingRequest",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OutgoingRequest_Users_CreatedById",
                table: "OutgoingRequest",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
