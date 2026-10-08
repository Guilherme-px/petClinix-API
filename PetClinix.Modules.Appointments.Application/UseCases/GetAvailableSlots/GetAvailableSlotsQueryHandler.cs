using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Domain.Repositories;

namespace PetClinix.Modules.Appointments.Application.UseCases.GetAvailableSlots;

public sealed class GetAvailableSlotsQueryHandler : ICommandHandler<GetAvailableSlotsQuery, Result<List<string>>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IServiceCatalogService _serviceCatalogService;
    private readonly IClinicScheduleService _clinicScheduleService;

    public GetAvailableSlotsQueryHandler(
        IAppointmentRepository appointmentRepository,
        IServiceCatalogService serviceCatalogService,
        IClinicScheduleService clinicScheduleService)
    {
        _appointmentRepository = appointmentRepository;
        _serviceCatalogService = serviceCatalogService;
        _clinicScheduleService = clinicScheduleService;
    }

    public async Task<Result<List<string>>> Handle(GetAvailableSlotsQuery query, CancellationToken cancellationToken)
    {
        var durationMinutes = await _serviceCatalogService.GetDurationInMinutesAsync(query.ServiceId, cancellationToken);
        if (durationMinutes <= 0)
            return Result<List<string>>.Failure("appointments.slots.invalid_duration", "Duração do serviço inválida.");

        var (startTime, endTime) = _clinicScheduleService.GetWorkingHours();

        var appointments = await _appointmentRepository.GetByVeterinarianAndDateAsync(query.VeterinarianId, query.Date, cancellationToken);

        var serviceIds = appointments.Select(a => a.ServiceId).Distinct().ToList();
        var durations = await _serviceCatalogService.GetDurationsInMinutesAsync(serviceIds, cancellationToken);

        var busyRanges = appointments
            .Select(a =>
            {
                var apptDuration = durations.TryGetValue(a.ServiceId, out var d) ? d : 0;
                return (Start: a.ScheduledTime, End: a.ScheduledTime.AddMinutes(apptDuration));
            })
            .Where(r => r.End > r.Start)
            .ToList();

        var availableSlots = new List<string>();
        var slotStart = startTime;

        while (slotStart.AddMinutes(durationMinutes) <= endTime)
        {
            var slotEnd = slotStart.AddMinutes(durationMinutes);

            var hasConflict = busyRanges.Any(b => slotStart < b.End && slotEnd > b.Start);

            if (!hasConflict)
                availableSlots.Add(slotStart.ToString("HH:mm"));

            slotStart = slotEnd;
        }

        return Result<List<string>>.Success(availableSlots);
    }
}