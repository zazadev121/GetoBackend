using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace apiprojnew.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "News",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextEn",
                table: "News",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TitleEn",
                table: "News");

            migrationBuilder.DropColumn(
                name: "TextEn",
                table: "News");
        }
    }
}
