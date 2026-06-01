using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RandomVideoCallWebpage.Data;

#nullable disable

namespace RandomVideoCallWebpage.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260601080000_AddUserIsBlocked")]
    /// <inheritdoc />
    public partial class AddUserIsBlocked : Migration    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "AspNetUsers");
        }
    }
}
