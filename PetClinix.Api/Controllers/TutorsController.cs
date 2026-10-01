using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.UseCases.RegisterTutor;
using PetClinix.Modules.Pets.Application.UseCases.GetTutors;
using PetClinix.Modules.Pets.Application.UseCases.UpdateTutor;
using PetClinix.Modules.Pets.Application.UseCases.DeactivateTutor;
using System.Security.Claims;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/tutors")]
[Authorize]
public class TutorsController : ControllerBase
{
    private readonly ICommandHandler<RegisterTutorCommand, Result> _registerTutorHandler;
    private readonly ICommandHandler<GetTutorsQuery, Result<PagedResult<TutorResponse>>> _getTutorsHandler;
    private readonly ICommandHandler<GetTutorByIdQuery, Result<TutorResponse>> _getTutorByIdHandler;
    private readonly ICommandHandler<UpdateTutorCommand, Result> _updateTutorHandler;
    private readonly ICommandHandler<DeactivateTutorCommand, Result> _deactivateTutorHandler;

    public TutorsController(
        ICommandHandler<RegisterTutorCommand, Result> registerTutorHandler,
        ICommandHandler<GetTutorsQuery, Result<PagedResult<TutorResponse>>> getTutorsHandler,
        ICommandHandler<GetTutorByIdQuery, Result<TutorResponse>> getTutorByIdHandler,
        ICommandHandler<UpdateTutorCommand, Result> updateTutorHandler,
        ICommandHandler<DeactivateTutorCommand, Result> deactivateTutorHandler)
    {
        _registerTutorHandler = registerTutorHandler;
        _getTutorsHandler = getTutorsHandler;
        _getTutorByIdHandler = getTutorByIdHandler;
        _updateTutorHandler = updateTutorHandler;
        _deactivateTutorHandler = deactivateTutorHandler;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterTutor([FromBody] RegisterTutorRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new RegisterTutorCommand(
            clinicId, userId, request.Name, request.Cpf, request.Email, request.PhoneNumber, request.SecondaryPhoneNumber,
            request.ZipCode, request.Street, request.Number, request.Neighborhood, request.Complement, request.City,
            request.State, request.Notes
        );

        var result = await _registerTutorHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetTutors(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetTutorsQuery(clinicId, pageNumber, pageSize, search ?? "");
        var result = await _getTutorsHandler.Handle(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{tutorId}")]
    public async Task<IActionResult> GetTutorById(Guid tutorId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetTutorByIdQuery(clinicId, tutorId);
        var result = await _getTutorByIdHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpPut("{tutorId}")]
    public async Task<IActionResult> UpdateTutor(Guid tutorId, [FromBody] UpdateTutorRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new UpdateTutorCommand(
            clinicId, tutorId, request.Name, request.Email, request.PhoneNumber, request.SecondaryPhoneNumber,
            request.ZipCode, request.Street, request.Number, request.Neighborhood, request.Complement,
            request.City, request.State, request.Notes
        );

        var result = await _updateTutorHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpDelete("{tutorId}")]
    public async Task<IActionResult> DeactivateTutor(Guid tutorId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new DeactivateTutorCommand(clinicId, tutorId);
        var result = await _deactivateTutorHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }
}

public record RegisterTutorRequest(
    string Name, string Cpf, string? Email, string PhoneNumber, string? SecondaryPhoneNumber,
    string ZipCode, string Street, string Number, string Neighborhood, string? Complement, string City, string State, string? Notes
);

public record UpdateTutorRequest(
    string Name, string? Email, string PhoneNumber, string? SecondaryPhoneNumber,
    string ZipCode, string Street, string Number, string Neighborhood, string? Complement, string City, string State, string? Notes
);