using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaff;

public sealed record GetStaffQuery(Guid ClinicId, int PageNumber, int PageSize, string Search = "") : ICommand<Result<PagedResult<StaffResponse>>>;