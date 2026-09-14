using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.UseCases.UpdateAccount;
using System.Security.Claims;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly ICommandHandler<UpdateAccountCommand, Result> _updateAccountHandler;

    public AccountController(ICommandHandler<UpdateAccountCommand, Result> updateAccountHandler)
    {
        _updateAccountHandler = updateAccountHandler;
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateAccount([FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem IDs necessários." });
        }

        var command = new UpdateAccountCommand(
            userId, clinicId,
            request.UserName, request.UserPhoneNumber, request.UserBirthDate,
            request.NewPassword,
            request.ClinicTradeName, request.ClinicLegalName, request.ClinicDocumentNumber,
            request.ClinicEmail, request.ClinicPhoneNumber,
            request.ClinicZipCode, request.ClinicStreet, request.ClinicNumber, request.ClinicNeighborhood,
            request.ClinicComplement, request.ClinicCity, request.ClinicState);

        var result = await _updateAccountHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }
}

public record UpdateAccountRequest(
    string UserName, string UserPhoneNumber, DateOnly UserBirthDate,
    string? NewPassword,
    string ClinicTradeName, string ClinicLegalName, string ClinicDocumentNumber,
    string ClinicEmail, string ClinicPhoneNumber,
    string ClinicZipCode, string ClinicStreet, string ClinicNumber, string ClinicNeighborhood,
    string? ClinicComplement, string ClinicCity, string ClinicState);