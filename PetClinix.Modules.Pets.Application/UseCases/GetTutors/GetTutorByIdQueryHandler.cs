using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.GetTutors;

public sealed class GetTutorByIdQueryHandler : ICommandHandler<GetTutorByIdQuery, Result<TutorResponse>>
{
    private readonly ITutorRepository _tutorRepository;

    public GetTutorByIdQueryHandler(ITutorRepository tutorRepository)
    {
        _tutorRepository = tutorRepository;
    }

    public async Task<Result<TutorResponse>> Handle(GetTutorByIdQuery query, CancellationToken cancellationToken)
    {
        var tutor = await _tutorRepository.GetByIdAsync(query.TutorId, cancellationToken);

        if (tutor == null || tutor.ClinicId != query.ClinicId)
        {
            return Result<TutorResponse>.Failure("pets.tutor.not_found", "Tutor não encontrado nesta clínica.");
        }

        var response = new TutorResponse(
            tutor.Id,
            tutor.Name,
            tutor.Cpf.Value,
            tutor.Email,
            tutor.PhoneNumber,
            tutor.SecondaryPhoneNumber,
            tutor.ZipCode,
            tutor.Street,
            tutor.Number,
            tutor.Neighborhood,
            tutor.Complement,
            tutor.City,
            tutor.State,
            tutor.Notes,
            tutor.IsActive
        );

        return Result<TutorResponse>.Success(response);
    }
}