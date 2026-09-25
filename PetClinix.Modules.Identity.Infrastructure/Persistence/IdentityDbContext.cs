using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Identity.Domain.Entities;

namespace PetClinix.Modules.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.ToTable("Clinics");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Slug).HasConversion(s => s.Value, v => Domain.ValueObjects.ClinicSlug.Create(v));
            entity.Property(c => c.Email).HasConversion(e => e.Value, v => Domain.ValueObjects.Email.Create(v));
            entity.Property(c => c.PhoneNumber).HasConversion(p => p.Value, v => Domain.ValueObjects.PhoneNumber.Create(v));

            entity.Property(c => c.TradeName).HasMaxLength(150).IsRequired();
            entity.Property(c => c.LegalName).HasMaxLength(150).IsRequired();
            entity.Property(c => c.DocumentNumber).HasMaxLength(30).IsRequired();
            entity.Property(c => c.ZipCode).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Street).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Number).HasMaxLength(20).IsRequired();
            entity.Property(c => c.Neighborhood).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Complement).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(100).IsRequired();
            entity.Property(c => c.State).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email).HasConversion(e => e.Value, v => Domain.ValueObjects.Email.Create(v));
            entity.Property(u => u.PhoneNumber).HasConversion(p => p.Value, v => Domain.ValueObjects.PhoneNumber.Create(v));

            entity.Property(u => u.Name).HasMaxLength(150).IsRequired();
            entity.Property(u => u.DocumentNumber).HasMaxLength(20).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(255);
            entity.Property(u => u.BirthDate).IsRequired();

            entity.Property<string>("NameSearchable")
                .HasComputedColumnSql("lower(f_unaccent(\"Name\"))", stored: true);

            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex("NameSearchable");
        });
    }
}