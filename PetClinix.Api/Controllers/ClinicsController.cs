using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.UseCases.RegisterStaff;
using PetClinix.Modules.Identity.Application.UseCases.UpdateClinic;
using PetClinix.Modules.Identity.Application.UseCases.GetStaff;
using PetClinix.Modules.Identity.Application.UseCases.UpdateStaff;
using PetClinix.Modules.Identity.Application.UseCases.DeactivateStaff;
using PetClinix.Modules.Identity.Domain.Enums;
using System.Security.Claims;
using PetClinix.Api.Extensions;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/clinics")]
public class ClinicsController : ControllerBase
{
    private readonly ICommandHandler<UpdateClinicCommand, Result> _updateClinicHandler;
    private readonly ICommandHandler<RegisterStaffCommand, Result<RegisterStaffResponse>> _registerStaffHandler;
    private readonly ICommandHandler<GetStaffQuery, Result<PagedResult<StaffResponse>>> _getStaffHandler;
    private readonly ICommandHandler<GetStaffByIdQuery, Result<StaffResponse>> _getStaffByIdHandler;
    private readonly ICommandHandler<UpdateStaffCommand, Result> _updateStaffHandler;
    private readonly ICommandHandler<DeactivateStaffCommand, Result> _deactivateStaffHandler;

    public ClinicsController(
        ICommandHandler<UpdateClinicCommand, Result> updateClinicHandler,
        ICommandHandler<RegisterStaffCommand, Result<RegisterStaffResponse>> registerStaffHandler,
        ICommandHandler<GetStaffQuery, Result<PagedResult<StaffResponse>>> getStaffHandler,
        ICommandHandler<GetStaffByIdQuery, Result<StaffResponse>> getStaffByIdHandler,
        ICommandHandler<UpdateStaffCommand, Result> updateStaffHandler,
         ICommandHandler<DeactivateStaffCommand, Result> deactivateStaffHandler)
    {
        _updateClinicHandler = updateClinicHandler;
        _registerStaffHandler = registerStaffHandler;
        _getStaffHandler = getStaffHandler;
        _getStaffByIdHandler = getStaffByIdHandler;
        _updateStaffHandler = updateStaffHandler;
        _deactivateStaffHandler = deactivateStaffHandler;
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateClinic([FromBody] UpdateClinicRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new UpdateClinicCommand(
            clinicId,
            request.TradeName, request.LegalName, request.DocumentNumber,
            request.Email, request.PhoneNumber,
            request.ZipCode, request.Street, request.Number, request.Neighborhood,
            request.Complement, request.City, request.State);

        var result = await _updateClinicHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("me/staff")]
    public async Task<IActionResult> RegisterStaff([FromBody] RegisterStaffRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Admin)
        {
            return BadRequest(new { error = "invalid_role", message = "Role inválida. Use Veterinarian ou Receptionist." });
        }

        var command = new RegisterStaffCommand(
            clinicId, userId,
            request.Name, request.Email, request.DocumentNumber,
            request.PhoneNumber, request.BirthDate, role
        );

        var result = await _registerStaffHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("me/staff")]
    public async Task<IActionResult> GetStaff(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 1,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClinicId(out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetStaffQuery(clinicId, pageNumber, pageSize, search ?? "");
        var result = await _getStaffHandler.Handle(query, cancellationToken);

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("me/staff/{userId}")]
    public async Task<IActionResult> GetStaffById(Guid userId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetStaffByIdQuery(clinicId, userId);
        var result = await _getStaffByIdHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("me/staff/{userId}")]
    public async Task<IActionResult> UpdateStaff(Guid userId, [FromBody] UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || role == UserRole.Admin)
        {
            return BadRequest(new { error = "invalid_role", message = "Role inválida. Use Veterinarian ou Receptionist." });
        }

        var command = new UpdateStaffCommand(clinicId, userId, userId, request.Name, request.PhoneNumber, request.BirthDate, role);
        var result = await _updateStaffHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("me/staff/{userId}")]
    public async Task<IActionResult> DeactivateStaff(Guid userId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new DeactivateStaffCommand(clinicId, userId);
        var result = await _deactivateStaffHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }
}

public record UpdateClinicRequest(
    string TradeName, string LegalName, string DocumentNumber,
    string Email, string PhoneNumber,
    string ZipCode, string Street, string Number, string Neighborhood,
    string? Complement, string City, string State);

public record RegisterStaffRequest(
    string Name, string Email, string DocumentNumber,
    string PhoneNumber, DateOnly BirthDate, string Role);

public record UpdateStaffRequest(
    string Name, string PhoneNumber, DateOnly BirthDate, string Role);