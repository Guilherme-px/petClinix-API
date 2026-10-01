namespace PetClinix.Modules.Pets.Application.UseCases.GetTutors;

public sealed record TutorResponse(
    Guid Id,
    string Name,
    string Cpf,
    string? Email,
    string PhoneNumber,
    string? SecondaryPhoneNumber,
    string ZipCode,
    string Street,
    string Number,
    string Neighborhood,
    string? Complement,
    string City,
    string? State,
    string? Notes,
    bool IsActive
);