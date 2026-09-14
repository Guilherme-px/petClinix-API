using PetClinix.BuildingBlocks.Domain;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Exceptions;
using PetClinix.Modules.Identity.Domain.ValueObjects;

namespace PetClinix.Modules.Identity.Domain.Entities;

public sealed class User : AggregateRoot
{
    public Guid ClinicId { get; private set; }
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public string DocumentNumber { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAtUtc { get; private set; }
    public string? RefreshToken { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; private set; }

    private User(
        Guid clinicId, Guid createdByUserId, string name, Email email, string? passwordHash,
        string documentNumber, PhoneNumber phoneNumber, DateOnly birthDate, UserRole role)
    {
        if (clinicId == Guid.Empty)
            throw new IdentityDomainException("identity.user.clinic_id_required", "A clínica do usuário é obrigatória.");
        if (string.IsNullOrWhiteSpace(name))
            throw new IdentityDomainException("identity.user.name_required", "O nome do usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new IdentityDomainException("identity.user.document_required", "O CPF do usuário é obrigatório.");

        Id = Guid.NewGuid();
        ClinicId = clinicId;
        CreatedByUserId = createdByUserId;
        Name = name.Trim();
        Email = email;
        PasswordHash = passwordHash;
        DocumentNumber = documentNumber.Trim();
        PhoneNumber = phoneNumber;
        BirthDate = birthDate;
        Role = role;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static User CreateAdmin(
        Guid clinicId, Guid createdByUserId, string name, string email, string? passwordHash,
        string documentNumber, string phoneNumber, DateOnly birthDate)
    {
        return new User(
            clinicId, createdByUserId, name, Email.Create(email), passwordHash,
            documentNumber, PhoneNumber.Create(phoneNumber), birthDate, UserRole.Admin
        );
    }

    public static User CreateStaff(
        Guid clinicId, Guid createdByUserId, string name, string email, string? passwordHash,
        string documentNumber, string phoneNumber, DateOnly birthDate, UserRole role)
    {
        if (role == UserRole.Admin)
            throw new IdentityDomainException(
                "identity.user.invalid_staff_role",
                "Use a criação de administrador para cadastrar um usuário administrador.");

        return new User(clinicId, createdByUserId, name, Email.Create(email), passwordHash,
            documentNumber, PhoneNumber.Create(phoneNumber), birthDate, role
        );
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new IdentityDomainException(
                "identity.user.name_required",
                "O nome do usuário é obrigatório.");

        Name = name.Trim();
    }

    public string GeneratePasswordResetToken()
    {
        PasswordResetToken = Guid.NewGuid().ToString("N");
        PasswordResetTokenExpiresAtUtc = DateTime.UtcNow.AddHours(24);
        return PasswordResetToken;
    }

    public void SetPassword(string token, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(PasswordResetToken) || PasswordResetToken != token)
            throw new IdentityDomainException("identity.user.invalid_token", "Token da redefinição inválido.");

        if (PasswordResetTokenExpiresAtUtc == null || PasswordResetTokenExpiresAtUtc < DateTime.UtcNow)
            throw new IdentityDomainException("identity.user.expired_token", "Token de redefinição expirado.");

        PasswordHash = passwordHash;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAtUtc = null;
    }

    public string GenerateRefreshToken()
    {
        RefreshToken = Guid.NewGuid().ToString("N");
        RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        return RefreshToken;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAtUtc = null;
    }

    public bool IsValidRefreshToken(string token)
    {
        return !string.IsNullOrWhiteSpace(RefreshToken) &&
               RefreshToken == token &&
               RefreshTokenExpiresAtUtc.HasValue &&
               RefreshTokenExpiresAtUtc > DateTime.UtcNow;
    }

    public void UpdatePersonalInfo(string name, string phoneNumber, DateOnly birthDate, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new IdentityDomainException("identity.user.name_required", "O nome do usuário é obrigatório.");

        Name = name.Trim();
        PhoneNumber = PhoneNumber.Create(phoneNumber);
        BirthDate = birthDate;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateStaffInfo(string name, string phoneNumber, DateOnly birthDate, UserRole role, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new IdentityDomainException("identity.user.name_required", "O nome do usuário é obrigatório.");

        Name = name.Trim();
        PhoneNumber = PhoneNumber.Create(phoneNumber);
        BirthDate = birthDate;
        Role = role;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new IdentityDomainException("identity.user.password_hash_required", "A nova senha é obrigatória.");

        PasswordHash = newPasswordHash;
    }
}