using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Modules.Identity.Application.UseCases.UpdateAccount;

public sealed record UpdateAccountCommand(
    Guid UserId,
    Guid ClinicId,
    string UserName, string UserPhoneNumber, DateOnly UserBirthDate,
    string? NewPassword,
    string ClinicTradeName, string ClinicLegalName, string ClinicDocumentNumber,
    string ClinicEmail, string ClinicPhoneNumber,
    string ClinicZipCode, string ClinicStreet, string ClinicNumber, string ClinicNeighborhood,
    string? ClinicComplement, string ClinicCity, string ClinicState) : ICommand<Result>;