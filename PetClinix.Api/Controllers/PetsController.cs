using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Application.UseCases.RegisterPet;
using PetClinix.Modules.Pets.Application.UseCases.GetPets;
using PetClinix.Modules.Pets.Application.UseCases.UpdatePet;
using PetClinix.Modules.Pets.Application.UseCases.DeactivatePet;
using PetClinix.Modules.Pets.Domain.Enums;
using System.Security.Claims;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/tutors/{tutorId}/pets")]
[Authorize]
public class PetsController : ControllerBase
{
    private readonly ICommandHandler<RegisterPetCommand, Result> _registerPetHandler;
    private readonly ICommandHandler<GetPetsQuery, Result<PagedResult<PetResponse>>> _getPetsHandler;
    private readonly ICommandHandler<GetPetByIdQuery, Result<PetResponse>> _getPetByIdHandler;
    private readonly ICommandHandler<UpdatePetCommand, Result> _updatePetHandler;
    private readonly ICommandHandler<DeactivatePetCommand, Result> _deactivatePetHandler;

    public PetsController(
        ICommandHandler<RegisterPetCommand, Result> registerPetHandler,
        ICommandHandler<GetPetsQuery, Result<PagedResult<PetResponse>>> getPetsHandler,
        ICommandHandler<GetPetByIdQuery, Result<PetResponse>> getPetByIdHandler,
        ICommandHandler<UpdatePetCommand, Result> updatePetHandler,
        ICommandHandler<DeactivatePetCommand, Result> deactivatePetHandler)
    {
        _registerPetHandler = registerPetHandler;
        _getPetsHandler = getPetsHandler;
        _getPetByIdHandler = getPetByIdHandler;
        _updatePetHandler = updatePetHandler;
        _deactivatePetHandler = deactivatePetHandler;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterPet(Guid tutorId, [FromBody] RegisterPetRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new RegisterPetCommand(
            clinicId,
            tutorId,
            userId,
            request.Name,
            request.Species,
            request.Breed,
            request.BirthDate,
            request.Sex,
            request.Weight,
            request.IsNeutered,
            request.Notes
        );

        var result = await _registerPetHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetPets(
        Guid tutorId,
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

        var query = new GetPetsQuery(clinicId, tutorId, pageNumber, pageSize, search ?? "");
        var result = await _getPetsHandler.Handle(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{petId}")]
    public async Task<IActionResult> GetPetById(Guid tutorId, Guid petId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetPetByIdQuery(clinicId, tutorId, petId);
        var result = await _getPetByIdHandler.Handle(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { result.ErrorCode, result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpPut("{petId}")]
    public async Task<IActionResult> UpdatePet(Guid tutorId, Guid petId, [FromBody] UpdatePetRequest request, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(clinicIdClaim, out var clinicId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new UpdatePetCommand(
            clinicId, tutorId, petId, userId,
            request.Name, request.Species, request.Breed, request.BirthDate,
            request.Sex, request.Weight, request.IsNeutered, request.Notes);

        var result = await _updatePetHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }

    [HttpDelete("{petId}")]
    public async Task<IActionResult> DeactivatePet(Guid tutorId, Guid petId, CancellationToken cancellationToken)
    {
        var clinicIdClaim = User.FindFirst("clinic_id")?.Value;
        if (!Guid.TryParse(clinicIdClaim, out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new DeactivatePetCommand(clinicId, tutorId, petId);
        var result = await _deactivatePetHandler.Handle(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { result.ErrorCode, result.ErrorMessage });
        }

        return NoContent();
    }
}

public record RegisterPetRequest(
    string Name,
    Species Species,
    string? Breed,
    DateOnly? BirthDate,
    PetSex Sex,
    double? Weight,
    bool IsNeutered,
    string? Notes
);

public record UpdatePetRequest(
    string Name,
    Species Species,
    string? Breed,
    DateOnly? BirthDate,
    PetSex Sex,
    double? Weight,
    bool IsNeutered,
    string? Notes
);