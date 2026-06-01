using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RandomVideoCallWebpage.Data;

#nullable disable

namespace RandomVideoCallWebpage.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260602120000_AddFriends")]
public partial class AddFriends : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FriendRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FromUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FriendRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_FriendRequests_AspNetUsers_FromUserId",
                    column: x => x.FromUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FriendRequests_AspNetUsers_ToUserId",
                    column: x => x.ToUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Friendships",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserAId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                UserBId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Friendships", x => x.Id));

        migrationBuilder.CreateTable(
            name: "FriendMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SenderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ReceiverId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_FriendMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "FriendCalls",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CallerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ReceiverId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_FriendCalls", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_FromUserId_ToUserId",
            table: "FriendRequests",
            columns: new[] { "FromUserId", "ToUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_ToUserId",
            table: "FriendRequests",
            column: "ToUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Friendships_UserAId_UserBId",
            table: "Friendships",
            columns: new[] { "UserAId", "UserBId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FriendMessages_ReceiverId",
            table: "FriendMessages",
            column: "ReceiverId");

        migrationBuilder.CreateIndex(
            name: "IX_FriendMessages_SenderId_ReceiverId",
            table: "FriendMessages",
            columns: new[] { "SenderId", "ReceiverId" });

        migrationBuilder.CreateIndex(
            name: "IX_FriendCalls_ReceiverId",
            table: "FriendCalls",
            column: "ReceiverId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FriendCalls");
        migrationBuilder.DropTable(name: "FriendMessages");
        migrationBuilder.DropTable(name: "Friendships");
        migrationBuilder.DropTable(name: "FriendRequests");
    }
}
