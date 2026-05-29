using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPlanner.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOriginalDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserOriginalDuration",
                table: "MLTrainingData",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserOriginalDuration",
                table: "MLTrainingData");
        }
    }
}
