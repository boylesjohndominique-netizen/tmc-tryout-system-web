using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TmcTryoutSystem.Migrations
{
    /// <inheritdoc />
    public partial class SportCategoriesAndDynamicCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SportCategoryId",
                table: "Sports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SportCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Icon = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportCategories", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EvaluationCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SportCategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Icon = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_SportCategories_SportCategoryId",
                        column: x => x.SportCategoryId,
                        principalTable: "SportCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EvaluationScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EvaluationId = table.Column<int>(type: "int", nullable: false),
                    EvaluationCriterionId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationScores_EvaluationCriteria_EvaluationCriterionId",
                        column: x => x.EvaluationCriterionId,
                        principalTable: "EvaluationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationScores_Evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "Evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Default categories. Fixed ids let the data migration below map the old
            // evaluation columns onto the physical sports criteria.
            migrationBuilder.InsertData(
                table: "SportCategories",
                columns: new[] { "Id", "Name", "Description", "Icon", "DisplayOrder" },
                values: new object[,]
                {
                    { 1, "Physical sports", "Sports judged on athletic ability and physical performance.", "activity", 1 },
                    { 2, "Mind sports", "Games and sports decided by thinking, strategy and mental skill.", "diagram-3", 2 },
                    { 3, "Esports", "Competitive video gaming decided by reaction, game sense and teamwork.", "controller", 3 }
                });

            migrationBuilder.InsertData(
                table: "EvaluationCriteria",
                columns: new[] { "Id", "SportCategoryId", "Name", "Icon", "DisplayOrder" },
                values: new object[,]
                {
                    { 1, 1, "Skill", "bullseye", 0 },
                    { 2, 1, "Speed", "lightning-charge", 1 },
                    { 3, 1, "Agility", "arrow-repeat", 2 },
                    { 4, 1, "Endurance", "activity", 3 },
                    { 5, 1, "Teamwork", "people", 4 },
                    { 6, 1, "Discipline", "shield-check", 5 },
                    { 7, 2, "Logic", "diagram-3", 0 },
                    { 8, 2, "Strategy", "map", 1 },
                    { 9, 2, "Concentration", "bullseye", 2 },
                    { 10, 2, "Memory", "lightbulb", 3 },
                    { 11, 2, "Problem solving", "puzzle", 4 },
                    { 12, 3, "Reaction time", "lightning-charge", 0 },
                    { 13, 3, "Game sense", "eye", 1 },
                    { 14, 3, "Mechanical skill", "joystick", 2 },
                    { 15, 3, "Teamwork", "people", 3 },
                    { 16, 3, "Communication", "chat-dots", 4 }
                });

            // Keep every existing sport evaluable by pointing it at the physical category.
            migrationBuilder.Sql("UPDATE `Sports` SET `SportCategoryId` = 1 WHERE `SportCategoryId` IS NULL;");

            // Copy existing scores into the new table before the old columns are dropped.
            migrationBuilder.Sql(
                "INSERT INTO `EvaluationScores` (`EvaluationId`, `EvaluationCriterionId`, `Value`) " +
                "SELECT `Id`, 1, `Skill` FROM `Evaluations` " +
                "UNION ALL SELECT `Id`, 2, `Speed` FROM `Evaluations` " +
                "UNION ALL SELECT `Id`, 3, `Agility` FROM `Evaluations` " +
                "UNION ALL SELECT `Id`, 4, `Endurance` FROM `Evaluations` " +
                "UNION ALL SELECT `Id`, 5, `Teamwork` FROM `Evaluations` " +
                "UNION ALL SELECT `Id`, 6, `Discipline` FROM `Evaluations`;");

            migrationBuilder.DropColumn(name: "Agility", table: "Evaluations");
            migrationBuilder.DropColumn(name: "Discipline", table: "Evaluations");
            migrationBuilder.DropColumn(name: "Endurance", table: "Evaluations");
            migrationBuilder.DropColumn(name: "OverallPerformance", table: "Evaluations");
            migrationBuilder.DropColumn(name: "Skill", table: "Evaluations");
            migrationBuilder.DropColumn(name: "Speed", table: "Evaluations");
            migrationBuilder.DropColumn(name: "Teamwork", table: "Evaluations");

            migrationBuilder.CreateIndex(
                name: "IX_Sports_SportCategoryId",
                table: "Sports",
                column: "SportCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_SportCategoryId",
                table: "EvaluationCriteria",
                column: "SportCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_EvaluationCriterionId",
                table: "EvaluationScores",
                column: "EvaluationCriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_EvaluationId_EvaluationCriterionId",
                table: "EvaluationScores",
                columns: new[] { "EvaluationId", "EvaluationCriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SportCategories_Name",
                table: "SportCategories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sports_SportCategories_SportCategoryId",
                table: "Sports",
                column: "SportCategoryId",
                principalTable: "SportCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sports_SportCategories_SportCategoryId",
                table: "Sports");

            migrationBuilder.DropTable(
                name: "EvaluationScores");

            migrationBuilder.DropTable(
                name: "EvaluationCriteria");

            migrationBuilder.DropTable(
                name: "SportCategories");

            migrationBuilder.DropIndex(
                name: "IX_Sports_SportCategoryId",
                table: "Sports");

            migrationBuilder.DropColumn(
                name: "SportCategoryId",
                table: "Sports");

            migrationBuilder.AddColumn<int>(
                name: "Agility",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Discipline",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Endurance",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OverallPerformance",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Skill",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Speed",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Teamwork",
                table: "Evaluations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
