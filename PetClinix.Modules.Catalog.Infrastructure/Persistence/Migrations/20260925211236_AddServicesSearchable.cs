using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetClinix.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServicesSearchable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameSearchable",
                table: "Services",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(f_unaccent(\"Name\"))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_NameSearchable",
                table: "Services",
                column: "NameSearchable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Services_NameSearchable",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "NameSearchable",
                table: "Services");
        }
    }
}
