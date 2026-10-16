using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmcTryoutSystem.Migrations
{
    /// <inheritdoc />
    public partial class WidenCourseColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Users",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Applicants",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // Rows captured before this change hold only the acronym. Expand them to the full
            // labels the dropdowns now save, so accounts, filters and reports all read the same.
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSIT (Bachelor of Science in Information Technology)' WHERE CourseOrGrade = 'BSIT';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSOA (Bachelor of Science in Office Administration)' WHERE CourseOrGrade = 'BSOA';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSED (Bachelor of Secondary Education)' WHERE CourseOrGrade = 'BSED';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSPolSci (Bachelor of Science in Political Science)' WHERE CourseOrGrade = 'BSPolSci';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSCrim (Bachelor of Science in Criminology)' WHERE CourseOrGrade = 'BSCrim';");

            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSIT (Bachelor of Science in Information Technology)' WHERE CourseOrGrade = 'BSIT';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSOA (Bachelor of Science in Office Administration)' WHERE CourseOrGrade = 'BSOA';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSED (Bachelor of Secondary Education)' WHERE CourseOrGrade = 'BSED';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSPolSci (Bachelor of Science in Political Science)' WHERE CourseOrGrade = 'BSPolSci';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSCrim (Bachelor of Science in Criminology)' WHERE CourseOrGrade = 'BSCrim';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Shrink the data back to the acronyms first: varchar(20) cannot hold the full labels.
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSIT' WHERE CourseOrGrade = 'BSIT (Bachelor of Science in Information Technology)';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSOA' WHERE CourseOrGrade = 'BSOA (Bachelor of Science in Office Administration)';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSED' WHERE CourseOrGrade = 'BSED (Bachelor of Secondary Education)';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSPolSci' WHERE CourseOrGrade = 'BSPolSci (Bachelor of Science in Political Science)';");
            migrationBuilder.Sql("UPDATE Users SET CourseOrGrade = 'BSCrim' WHERE CourseOrGrade = 'BSCrim (Bachelor of Science in Criminology)';");

            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSIT' WHERE CourseOrGrade = 'BSIT (Bachelor of Science in Information Technology)';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSOA' WHERE CourseOrGrade = 'BSOA (Bachelor of Science in Office Administration)';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSED' WHERE CourseOrGrade = 'BSED (Bachelor of Secondary Education)';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSPolSci' WHERE CourseOrGrade = 'BSPolSci (Bachelor of Science in Political Science)';");
            migrationBuilder.Sql("UPDATE Applicants SET CourseOrGrade = 'BSCrim' WHERE CourseOrGrade = 'BSCrim (Bachelor of Science in Criminology)';");
            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Users",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CourseOrGrade",
                table: "Applicants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(80)",
                oldMaxLength: 80)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
