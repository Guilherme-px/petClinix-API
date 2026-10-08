using PetClinix.BuildingBlocks.Domain;
using PetClinix.Modules.Appointments.Domain.Enums;
using PetClinix.Modules.Appointments.Domain.Exceptions;

namespace PetClinix.Modules.Appointments.Domain.Entities;

public sealed class Appointment : AggregateRoot
{
    public Guid ClinicId { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid PetId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Guid VeterinarianId { get; private set; }
    public DateOnly ScheduledDate { get; private set; }
    public TimeOnly ScheduledTime { get; private set; }
    public string? Notes { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; }

#pragma warning disable CS8618
    private Appointment() { }

    private Appointment(
        Guid clinicId, Guid tutorId, Guid petId, Guid serviceId, Guid veterinarianId,
        DateOnly scheduledDate, TimeOnly scheduledTime, string? notes, Guid createdByUserId)
    {
        if (clinicId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.clinic_id_required", "Clínica é obrigatória.");
        if (tutorId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.tutor_id_required", "Tutor é obrigatório.");
        if (petId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.pet_id_required", "Pet é obrigatório.");
        if (serviceId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.service_id_required", "Serviço é obrigatório.");
        if (veterinarianId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.vet_id_required", "Veterinário é obrigatório.");
        if (createdByUserId == Guid.Empty) throw new AppointmentsDomainException("appointments.appt.created_by_required", "Usuário criador é obrigatório.");

        if (scheduledDate.ToDateTime(scheduledTime) <= ClinicClock.Now())
            throw new AppointmentsDomainException("appointments.appt.past_date", "Não é possível agendar para uma data no passado.");

        Id = Guid.NewGuid();
        ClinicId = clinicId;
        TutorId = tutorId;
        PetId = petId;
        ServiceId = serviceId;
        VeterinarianId = veterinarianId;
        ScheduledDate = scheduledDate;
        ScheduledTime = scheduledTime;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;
        Status = AppointmentStatus.Scheduled;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Appointment Create(
        Guid clinicId, Guid tutorId, Guid petId, Guid serviceId, Guid veterinarianId,
        DateOnly scheduledDate, TimeOnly scheduledTime, string? notes, Guid createdByUserId)
    {
        return new Appointment(clinicId, tutorId, petId, serviceId, veterinarianId, scheduledDate, scheduledTime, notes, createdByUserId);
    }

    public void Confirm(Guid updatedByUserId)
    {
        if (Status != AppointmentStatus.Scheduled)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Apenas agendamentos com status 'Scheduled' podem ser confirmados.");

        Status = AppointmentStatus.Confirmed;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Complete(Guid updatedByUserId)
    {
        if (Status != AppointmentStatus.Scheduled && Status != AppointmentStatus.Confirmed)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Apenas agendamentos ativos podem ser concluídos.");

        Status = AppointmentStatus.Completed;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsNoShow(Guid updatedByUserId)
    {
        if (Status != AppointmentStatus.Scheduled && Status != AppointmentStatus.Confirmed)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Apenas agendamentos ativos podem ser marcados como falta.");

        Status = AppointmentStatus.NoShow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(Guid updatedByUserId)
    {
        if (Status == AppointmentStatus.Completed || Status == AppointmentStatus.Canceled)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Agendamentos concluídos ou já cancelados não podem ser cancelados.");

        Status = AppointmentStatus.Canceled;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reschedule(Guid updatedByUserId, DateOnly newDate, TimeOnly newTime)
    {
        if (Status == AppointmentStatus.Completed || Status == AppointmentStatus.Canceled)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Agendamentos concluídos ou cancelados não podem ser remarcados.");

        if (newDate.ToDateTime(newTime) <= ClinicClock.Now())
            throw new AppointmentsDomainException("appointments.appt.past_date", "Não é possível remarcar para uma data no passado.");

        ScheduledDate = newDate;
        ScheduledTime = newTime;
        Status = AppointmentStatus.Scheduled;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(
        Guid updatedByUserId, Guid veterinarianId, Guid serviceId, DateOnly newDate, TimeOnly newTime, string? notes)
    {
        if (Status == AppointmentStatus.Completed || Status == AppointmentStatus.Canceled)
            throw new AppointmentsDomainException("appointments.appt.invalid_status", "Agendamentos concluídos ou cancelados não podem ser editados.");

        if (newDate.ToDateTime(newTime) <= ClinicClock.Now())
            throw new AppointmentsDomainException("appointments.appt.past_date", "Não é possível remarcar para uma data no passado.");

        VeterinarianId = veterinarianId;
        ServiceId = serviceId;
        ScheduledDate = newDate;
        ScheduledTime = newTime;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (Status == AppointmentStatus.Confirmed)
            Status = AppointmentStatus.Scheduled;

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}