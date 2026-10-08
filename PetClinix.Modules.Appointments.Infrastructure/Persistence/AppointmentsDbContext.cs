using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Appointments.Domain.Entities;

namespace PetClinix.Modules.Appointments.Infrastructure.Persistence;

public class AppointmentsDbContext : DbContext
{
    public AppointmentsDbContext(DbContextOptions<AppointmentsDbContext> options) : base(options) { }

    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ClinicId).IsRequired();
            entity.Property(a => a.TutorId).IsRequired();
            entity.Property(a => a.PetId).IsRequired();
            entity.Property(a => a.ServiceId).IsRequired();
            entity.Property(a => a.VeterinarianId).IsRequired();
            entity.Property(a => a.ScheduledDate).HasColumnType("date").IsRequired();
            entity.Property(a => a.ScheduledTime).HasColumnType("time").IsRequired();
            entity.Property(a => a.Status).IsRequired();
            entity.Property(a => a.Notes).HasMaxLength(1000);
            entity.Property(a => a.CreatedByUserId).IsRequired();
            entity.Property(a => a.RowVersion).IsRowVersion();
            entity.HasIndex(a => new { a.VeterinarianId, a.ScheduledDate });
            entity.HasIndex(a => new { a.ClinicId, a.ScheduledDate });
        });
    }
}