using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetClinix.Modules.Appointments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitScheduledDateAndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledDateUtc",
                table: "Appointments");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "Appointments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduledTime",
                table: "Appointments",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId_ScheduledDate",
                table: "Appointments",
                columns: new[] { "ClinicId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VeterinarianId_ScheduledDate",
                table: "Appointments",
                columns: new[] { "VeterinarianId", "ScheduledDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId_ScheduledDate",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_VeterinarianId_ScheduledDate",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ScheduledTime",
                table: "Appointments");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDateUtc",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
