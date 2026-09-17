using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetClinix.Api.Extensions;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Application.UseCases.RegisterService;
using PetClinix.Modules.Catalog.Application.UseCases.GetServices;
using PetClinix.Modules.Catalog.Application.UseCases.UpdateService;
using PetClinix.Modules.Catalog.Application.UseCases.DeactivateService;

namespace PetClinix.Api.Controllers;

[ApiController]
[Route("api/services")]
[Authorize]
public class ServicesController : ControllerBase
{
    private readonly ICommandHandler<RegisterServiceCommand, Result> _registerServiceHandler;
    private readonly ICommandHandler<GetServicesQuery, Result<PagedResult<ServiceResponse>>> _getServicesHandler;
    private readonly ICommandHandler<UpdateServiceCommand, Result> _updateServiceHandler;
    private readonly ICommandHandler<DeactivateServiceCommand, Result> _deactivateServiceHandler;

    public ServicesController(
        ICommandHandler<RegisterServiceCommand, Result> registerServiceHandler,
        ICommandHandler<GetServicesQuery, Result<PagedResult<ServiceResponse>>> getServicesHandler,
        ICommandHandler<UpdateServiceCommand, Result> updateServiceHandler,
        ICommandHandler<DeactivateServiceCommand, Result> deactivateServiceHandler)
    {
        _registerServiceHandler = registerServiceHandler;
        _getServicesHandler = getServicesHandler;
        _updateServiceHandler = updateServiceHandler;
        _deactivateServiceHandler = deactivateServiceHandler;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterService([FromBody] RegisterServiceRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetClinicId(out var clinicId) || !User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new RegisterServiceCommand(
            clinicId,
            userId,
            request.Name,
            request.Description,
            request.DurationInMinutes,
            request.Price,
            request.RequiresVeterinarian
        );

        var result = await _registerServiceHandler.Handle(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet]
    public async Task<IActionResult> GetServices(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetClinicId(out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var query = new GetServicesQuery(clinicId, pageNumber, pageSize, search ?? "");
        var result = await _getServicesHandler.Handle(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpPut("{serviceId}")]
    public async Task<IActionResult> UpdateService(Guid serviceId, [FromBody] UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetClinicId(out var clinicId) || !User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Token inválido." });
        }

        var command = new UpdateServiceCommand(
            clinicId, serviceId, userId,
            request.Name, request.Description, request.DurationInMinutes, request.Price, request.RequiresVeterinarian);

        var result = await _updateServiceHandler.Handle(command, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{serviceId}")]
    public async Task<IActionResult> DeactivateService(Guid serviceId, CancellationToken cancellationToken)
    {
        if (!User.TryGetClinicId(out var clinicId))
        {
            return Unauthorized(new { message = "Token inválido ou sem ID da clínica." });
        }

        var command = new DeactivateServiceCommand(clinicId, serviceId);
        var result = await _deactivateServiceHandler.Handle(command, cancellationToken);

        return result.ToActionResult();
    }
}

public record RegisterServiceRequest(
    string Name,
    string? Description,
    int DurationInMinutes,
    decimal Price,
    bool RequiresVeterinarian
);

public record UpdateServiceRequest(
    string Name,
    string? Description,
    int DurationInMinutes,
    decimal Price,
    bool RequiresVeterinarian
);