using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaffLimit;

public sealed record GetStaffLimitQuery(Guid ClinicId) : ICommand<Result<StaffLimitResponse>>;