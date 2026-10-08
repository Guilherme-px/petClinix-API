using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Domain.Exceptions;
using PetClinix.Modules.Appointments.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointment;

public sealed class UpdateAppointmentCommandHandler : ICommandHandler<UpdateAppointmentCommand, Result>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IServiceCatalogService _serviceCatalogService;
    private readonly IClinicScheduleService _clinicScheduleService;
    private readonly IAppointmentsUnitOfWork _unitOfWork;

    public UpdateAppointmentCommandHandler(
        IAppointmentRepository appointmentRepository,
        IServiceCatalogService serviceCatalogService,
        IClinicScheduleService clinicScheduleService,
        IAppointmentsUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _serviceCatalogService = serviceCatalogService;
        _clinicScheduleService = clinicScheduleService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateAppointmentCommand command, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(command.AppointmentId, cancellationToken);

        if (appointment == null || appointment.ClinicId != command.ClinicId)
        {
            return Result.Failure("appointments.appt.not_found", "Agendamento não encontrado.");
        }

        var selectedDuration = await _serviceCatalogService.GetDurationInMinutesAsync(command.ServiceId, cancellationToken);
        if (selectedDuration <= 0)
            return Result.Failure("appointments.appt.invalid_service", "Serviço inválido ou sem duração definida.");

        var (startTime, endTime) = _clinicScheduleService.GetWorkingHours();

        if (command.Time < startTime || command.Time.AddMinutes(selectedDuration) > endTime)
            return Result.Failure("appointments.appt.outside_working_hours", $"O agendamento deve ocorrer entre {startTime:hh\\:mm} e {endTime:hh\\:mm}.");

        var existingAppointments = await _appointmentRepository.GetByVeterinarianAndDateAsync(command.VeterinarianId, command.Date, cancellationToken);

        var serviceIds = existingAppointments.Select(a => a.ServiceId).Distinct().ToList();
        var durations = await _serviceCatalogService.GetDurationsInMinutesAsync(serviceIds, cancellationToken);

        var newStart = command.Time;
        var newEnd = command.Time.AddMinutes(selectedDuration);

        var hasConflict = existingAppointments.Any(a =>
        {
            if (a.Id == command.AppointmentId) return false;

            var apptDuration = durations.TryGetValue(a.ServiceId, out var d) ? d : 0;
            return newStart < a.ScheduledTime.AddMinutes(apptDuration) && newEnd > a.ScheduledTime;
        });

        if (hasConflict)
        {
            return Result.Failure("appointments.appt.slot_taken", "O veterinário já possui um agendamento que conflita com este horário.");
        }

        try
        {
            appointment.UpdateDetails(
                command.UpdatedByUserId,
                command.VeterinarianId,
                command.ServiceId,
                command.Date,
                command.Time,
                command.Notes
            );

            await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("appointments.appt.conflict", "Este agendamento foi modificado por outro usuário. Recarregue os dados e tente novamente.");
        }
        catch (AppointmentsDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}