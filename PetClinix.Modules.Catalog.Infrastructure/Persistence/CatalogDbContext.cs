using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Catalog.Domain.Entities;

namespace PetClinix.Modules.Catalog.Infrastructure.Persistence;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("Services");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Description).HasMaxLength(500);
            entity.Property(s => s.DurationInMinutes).IsRequired();
            entity.Property(s => s.Price).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(s => s.ClinicId).IsRequired();
            entity.Property(s => s.CreatedByUserId).IsRequired();
            entity.HasIndex(s => new { s.ClinicId, s.Name }).IsUnique();

            entity.Property<string>("NameSearchable")
                .HasComputedColumnSql("lower(f_unaccent(\"Name\"))", stored: true);

            entity.HasIndex("NameSearchable");
        });
    }
}