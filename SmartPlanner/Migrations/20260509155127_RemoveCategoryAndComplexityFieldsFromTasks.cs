using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPlanner.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCategoryAndComplexityFieldsFromTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Complexity",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Activities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Complexity",
                table: "Tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
