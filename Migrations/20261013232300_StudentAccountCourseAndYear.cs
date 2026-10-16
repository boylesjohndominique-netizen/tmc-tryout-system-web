using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmcTryoutSystem.Migrations
{
    /// <inheritdoc />
    public partial class StudentAccountCourseAndYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CourseOrGrade",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "YearLevel",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseOrGrade",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "YearLevel",
                table: "Users");
        }
    }
}
