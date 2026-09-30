using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediFlow.PatientProfile.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPatientProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PatientProfile");

            migrationBuilder.CreateTable(
                name: "Patients",
                schema: "PatientProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergies",
                schema: "PatientProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientAllergies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "PatientProfile",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientAllergies_PatientId_Name",
                schema: "PatientProfile",
                table: "PatientAllergies",
                columns: new[] { "PatientId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LastName_FirstName",
                schema: "PatientProfile",
                table: "Patients",
                columns: new[] { "LastName", "FirstName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientAllergies",
                schema: "PatientProfile");

            migrationBuilder.DropTable(
                name: "Patients",
                schema: "PatientProfile");
        }
    }
}
