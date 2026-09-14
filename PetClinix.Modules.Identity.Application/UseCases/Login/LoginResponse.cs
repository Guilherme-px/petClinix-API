namespace PetClinix.Modules.Identity.Application.UseCases.Login;

public sealed record LoginResponse(
    string Token,
    string RefreshToken,
    string Email,
    string Role,
    string Name
);