using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaff;

public sealed class GetVeterinariansQueryHandler : ICommandHandler<GetVeterinariansQuery, Result<List<VeterinarianResponse>>>
{
    private readonly IUserRepository _userRepository;

    public GetVeterinariansQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<List<VeterinarianResponse>>> Handle(GetVeterinariansQuery query, CancellationToken cancellationToken)
    {
        var veterinarians = await _userRepository.GetActiveByRoleAsync(query.ClinicId, UserRole.Veterinarian, cancellationToken);

        var response = veterinarians
            .Select(v => new VeterinarianResponse(v.Id, v.Name))
            .ToList();

        return Result<List<VeterinarianResponse>>.Success(response);
    }
}