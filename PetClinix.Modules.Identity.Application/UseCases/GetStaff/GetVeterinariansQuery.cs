using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaff;

public sealed record GetVeterinariansQuery(Guid ClinicId) : ICommand<Result<List<VeterinarianResponse>>>;

public sealed record VeterinarianResponse(Guid Id, string Name);