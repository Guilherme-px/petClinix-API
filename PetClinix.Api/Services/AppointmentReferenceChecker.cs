using Microsoft.EntityFrameworkCore;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using PetClinix.Modules.Pets.Infrastructure.Persistence;

namespace PetClinix.Api.Services;

public class AppointmentReferenceChecker : IAppointmentReferenceChecker
{
    private readonly PetsDbContext _petsDbContext;
    private readonly IdentityDbContext _identityDbContext;

    public AppointmentReferenceChecker(PetsDbContext petsDbContext, IdentityDbContext identityDbContext)
    {
        _petsDbContext = petsDbContext;
        _identityDbContext = identityDbContext;
    }

    public async Task<bool> TutorExistsInClinicAsync(Guid clinicId, Guid tutorId, CancellationToken cancellationToken = default)
    {
        return await _petsDbContext.Tutors.AnyAsync(
            t => t.ClinicId == clinicId && t.Id == tutorId && t.IsActive, cancellationToken);
    }

    public async Task<bool> PetExistsInClinicAsync(Guid clinicId, Guid petId, CancellationToken cancellationToken = default)
    {
        return await _petsDbContext.Pets.AnyAsync(
            p => p.ClinicId == clinicId && p.Id == petId && p.IsActive, cancellationToken);
    }

    public async Task<bool> VeterinarianExistsInClinicAsync(Guid clinicId, Guid veterinarianId, CancellationToken cancellationToken = default)
    {
        return await _identityDbContext.Users.AnyAsync(
            u => u.ClinicId == clinicId && u.Id == veterinarianId && u.Role == UserRole.Veterinarian && u.IsActive, cancellationToken);
    }
}