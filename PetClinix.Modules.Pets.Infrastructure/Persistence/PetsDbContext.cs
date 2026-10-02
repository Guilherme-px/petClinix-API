using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Pets.Domain.Entities;
using PetClinix.Modules.Pets.Domain.ValueObjects;

namespace PetClinix.Modules.Pets.Infrastructure.Persistence;

public class PetsDbContext : DbContext
{
    public PetsDbContext(DbContextOptions<PetsDbContext> options) : base(options) { }
    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<Pet> Pets => Set<Pet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tutor>(entity =>
        {
            entity.ToTable("Tutors");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Cpf).HasConversion(c => c.Value, v => Cpf.Create(v));
            entity.Property(t => t.Name).HasMaxLength(150).IsRequired();
            entity.Property(t => t.Email).HasMaxLength(256);
            entity.Property(t => t.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(t => t.SecondaryPhoneNumber).HasMaxLength(20);
            entity.Property(t => t.ZipCode).HasMaxLength(20).IsRequired();
            entity.Property(t => t.Street).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Number).HasMaxLength(20).IsRequired();
            entity.Property(t => t.Neighborhood).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Complement).HasMaxLength(200);
            entity.Property(t => t.City).HasMaxLength(100).IsRequired();
            entity.Property(t => t.State).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Notes).HasMaxLength(1000);
            entity.HasIndex(t => new { t.ClinicId, t.Cpf }).IsUnique();
            entity.Property(t => t.CreatedByUserId).IsRequired();

            entity.Property<string>("NameSearchable")
                .HasComputedColumnSql("lower(f_unaccent(\"Name\"))", stored: true);

            entity.HasIndex("NameSearchable");
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.ToTable("Pets");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Species).IsRequired();
            entity.Property(p => p.Breed).HasMaxLength(50);
            entity.Property(p => p.Sex).IsRequired();
            entity.Property(p => p.Notes).HasMaxLength(1000);
            entity.Property(p => p.ClinicId).IsRequired();
            entity.Property(p => p.TutorId).IsRequired();
            entity.Property(p => p.CreatedByUserId).IsRequired();
            entity.HasIndex(p => new { p.TutorId, p.Name }).IsUnique();

            entity.Property<string>("NameSearchable")
                .HasComputedColumnSql("lower(f_unaccent(\"Name\"))", stored: true);

            entity.HasIndex("NameSearchable");
        });
    }
}