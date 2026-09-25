using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetClinix.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersNameSearchable : Migration
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
                table: "Users",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(f_unaccent(\"Name\"))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NameSearchable",
                table: "Users",
                column: "NameSearchable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NameSearchable",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NameSearchable",
                table: "Users");
        }
    }
}
