using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Domain.Enums;
using PetClinix.Modules.Appointments.Domain.Exceptions;
using PetClinix.Modules.Appointments.Domain.Repositories;

namespace PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointmentStatus;

public sealed class UpdateAppointmentStatusCommandHandler : ICommandHandler<UpdateAppointmentStatusCommand, Result>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentsUnitOfWork _unitOfWork;

    public UpdateAppointmentStatusCommandHandler(IAppointmentRepository appointmentRepository, IAppointmentsUnitOfWork unitOfWork)
    {
        _appointmentRepository = appointmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateAppointmentStatusCommand command, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(command.ClinicId, command.AppointmentId, cancellationToken);

        if (appointment == null)
        {
            return Result.Failure("appointments.appt.not_found", "Agendamento não encontrado.");
        }

        try
        {
            switch (command.NewStatus)
            {
                case AppointmentStatus.Confirmed:
                    appointment.Confirm(command.UpdatedByUserId);
                    break;
                case AppointmentStatus.Completed:
                    appointment.Complete(command.UpdatedByUserId);
                    break;
                case AppointmentStatus.NoShow:
                    appointment.MarkAsNoShow(command.UpdatedByUserId);
                    break;
                case AppointmentStatus.Canceled:
                    appointment.Cancel(command.UpdatedByUserId);
                    break;
                default:
                    return Result.Failure("appointments.appt.invalid_status", "Ação de status inválida ou não suportada.");
            }

            await _appointmentRepository.UpdateAsync(appointment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (AppointmentsDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}