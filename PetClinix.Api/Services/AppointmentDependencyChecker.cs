using Microsoft.EntityFrameworkCore;
using PetClinix.BuildingBlocks.Application;
using PetClinix.BuildingBlocks.Domain;
using PetClinix.Modules.Appointments.Domain.Enums;
using PetClinix.Modules.Appointments.Infrastructure.Persistence;

namespace PetClinix.Api.Services;

public class AppointmentDependencyChecker : IAppointmentDependencyChecker
{
    private readonly AppointmentsDbContext _context;

    public AppointmentDependencyChecker(AppointmentsDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasFutureAppointmentsForTutorAsync(Guid tutorId, CancellationToken cancellationToken = default)
    {
        var (today, nowTime) = GetClinicNow();
        return await _context.Appointments.AnyAsync(a =>
            a.TutorId == tutorId &&
            (a.ScheduledDate > today || (a.ScheduledDate == today && a.ScheduledTime >= nowTime)) &&
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed),
            cancellationToken
        );
    }

    public async Task<bool> HasFutureAppointmentsForPetAsync(Guid petId, CancellationToken cancellationToken = default)
    {
        var (today, nowTime) = GetClinicNow();
        return await _context.Appointments.AnyAsync(a =>
            a.PetId == petId &&
            (a.ScheduledDate > today || (a.ScheduledDate == today && a.ScheduledTime >= nowTime)) &&
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed),
            cancellationToken
        );
    }

    public async Task<bool> HasFutureAppointmentsForVetAsync(Guid vetId, CancellationToken cancellationToken = default)
    {
        var (today, nowTime) = GetClinicNow();
        return await _context.Appointments.AnyAsync(a =>
            a.VeterinarianId == vetId &&
            (a.ScheduledDate > today || (a.ScheduledDate == today && a.ScheduledTime >= nowTime)) &&
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed),
            cancellationToken
        );
    }

    public async Task<bool> HasFutureAppointmentsForServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var (today, nowTime) = GetClinicNow();
        return await _context.Appointments.AnyAsync(a =>
            a.ServiceId == serviceId &&
            (a.ScheduledDate > today || (a.ScheduledDate == today && a.ScheduledTime >= nowTime)) &&
            (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed),
            cancellationToken
        );
    }

    private static (DateOnly Today, TimeOnly NowTime) GetClinicNow()
    {
        var now = ClinicClock.Now();
        return (DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now));
    }
}