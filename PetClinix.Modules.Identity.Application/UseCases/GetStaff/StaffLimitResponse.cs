namespace PetClinix.Modules.Identity.Application.UseCases.GetStaffLimit;

public sealed record StaffLimitResponse(int Used, int? Limit);