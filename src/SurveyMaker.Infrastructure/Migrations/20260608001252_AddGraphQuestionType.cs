using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SurveyMaker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphQuestionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "QuestionTypes",
                columns: new[] { "QuestionTypeId", "QuestionTypeName" },
                values: new object[] { 25, "Graph" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "QuestionTypes",
                keyColumn: "QuestionTypeId",
                keyValue: 25);
        }
    }
}
