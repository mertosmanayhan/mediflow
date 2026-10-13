using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediFlow.PatientProfile.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditingAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_LastName_FirstName",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "PatientProfile",
                table: "Patients",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                schema: "PatientProfile",
                table: "Patients",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "PatientProfile",
                table: "Patients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "PatientProfile",
                table: "Patients",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LastName_FirstName",
                schema: "PatientProfile",
                table: "Patients",
                columns: new[] { "LastName", "FirstName" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_LastName_FirstName",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "PatientProfile",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LastName_FirstName",
                schema: "PatientProfile",
                table: "Patients",
                columns: new[] { "LastName", "FirstName" });
        }
    }
}
