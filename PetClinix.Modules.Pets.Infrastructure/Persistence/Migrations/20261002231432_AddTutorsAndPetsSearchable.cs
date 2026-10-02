using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetClinix.Modules.Pets.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorsAndPetsSearchable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION public.f_unaccent(text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                AS $$             SELECT public.unaccent('public.unaccent', $1)
                $$;
            ");

            migrationBuilder.AddColumn<string>(
                name: "NameSearchable",
                table: "Tutors",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(f_unaccent(\"Name\"))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NameSearchable",
                table: "Pets",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(f_unaccent(\"Name\"))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tutors_NameSearchable",
                table: "Tutors",
                column: "NameSearchable");

            migrationBuilder.CreateIndex(
                name: "IX_Pets_NameSearchable",
                table: "Pets",
                column: "NameSearchable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tutors_NameSearchable",
                table: "Tutors");

            migrationBuilder.DropIndex(
                name: "IX_Pets_NameSearchable",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "NameSearchable",
                table: "Tutors");

            migrationBuilder.DropColumn(
                name: "NameSearchable",
                table: "Pets");
        }
    }
}
