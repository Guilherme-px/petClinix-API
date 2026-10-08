using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Appointments.Application.UseCases.RegisterAppointment;
using PetClinix.Modules.Appointments.Application.UseCases.GetAvailableSlots;
using PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointment;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointmentStatus;
using PetClinix.Modules.Appointments.Domain.Enums;
using System.Security.Claims;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly ICommandHandler<RegisterAppointmentCommand, Result> _registerAppointmentHandler;
    private readonly ICommandHandler<GetAvailableSlotsQuery, Result<List<string>>> _getSlotsHandler;
    private readonly ICommandHandler<GetAppointmentsQuery, Result<List<AppointmentResponse>>> _getAppointmentsHandler;
    private readonly ICommandHandler<GetAppointmentByIdQuery, Result<AppointmentResponse>> _getAppointmentByIdHandler;
    private readonly ICommandHandler<UpdateAppointmentCommand, Result> _updateAppointmentHandler;
    private readonly ICommandHandler<UpdateAppointmentStatusCommand, Result> _updateStatusHandler;

    public AppointmentsController(
        ICommandHandler<RegisterAppointmentCommand, Result> registerAppointmentHandler,
        ICommandHandler<GetAvailableSlotsQuery, Result<List<string>>> getSlotsHandler,
        ICommandHandler<GetAppointmentsQuery, Result<List<AppointmentResponse>>> getAppointmentsHandler,
        ICommandHandler<GetAppointmentByIdQuery, Result<AppointmentResponse>> getAppointmentByIdHandler,
        ICommandHandler<UpdateAppointmentCommand, Result> updateAppointmentHandler,
        ICommandHandler<UpdateAppointmentStatusCommand, Result> updateStatusHandler)
    {
        _registerAppointmentHandler = registerAppointmentHandler;
        _getSlotsHandler = getSlotsHandler;
        _getAppointmentsHandler = getAppointmentsHandler;
        _getAppointmentByIdHandler = getAppointmentByIdHandler;
        _updateAppointmentHandler = updateAppointmentHandler;
        _updateStatusHandler = updateStatusHandler;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterAppointment([FromBody] RegisterAppointmentRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new RegisterAppointmentCommand(
            clinicId, request.TutorId, request.PetId, request.ServiceId, request.VeterinarianId,
            request.Date, request.Time, request.Notes, userId
        );

        var result = await _registerAppointmentHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpGet("available-slots")]
    public async Task<IActionResult> GetAvailableSlots([FromQuery] Guid vetId, [FromQuery] Guid serviceId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var query = new GetAvailableSlotsQuery(clinicId, vetId, serviceId, date);
        var result = await _getSlotsHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetAppointmentsQuery(clinicId, startDate, endDate);
        var result = await _getAppointmentsHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpGet("{appointmentId:guid}")]
    public async Task<IActionResult> GetAppointmentById(Guid appointmentId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var query = new GetAppointmentByIdQuery(clinicId, appointmentId);
        var result = await _getAppointmentByIdHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpPut("{appointmentId:guid}")]
    public async Task<IActionResult> UpdateAppointment(Guid appointmentId, [FromBody] UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new UpdateAppointmentCommand(
            clinicId, appointmentId, userId,
            request.VeterinarianId, request.ServiceId, request.Date, request.Time, request.Notes
        );

        var result = await _updateAppointmentHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpPatch("{appointmentId:guid}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(Guid appointmentId, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new UpdateAppointmentStatusCommand(clinicId, appointmentId, userId, request.NewStatus);
        var result = await _updateStatusHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }
}

public record RegisterAppointmentRequest(
    Guid TutorId, Guid PetId, Guid ServiceId, Guid VeterinarianId,
    DateOnly Date, TimeOnly Time, string? Notes
);

public record UpdateAppointmentRequest(
    Guid VeterinarianId,
    Guid ServiceId,
    DateOnly Date,
    TimeOnly Time,
    string? Notes
);

public record UpdateStatusRequest(AppointmentStatus NewStatus);