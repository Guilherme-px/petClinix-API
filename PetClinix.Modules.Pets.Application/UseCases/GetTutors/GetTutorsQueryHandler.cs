using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Pets.Domain.Repositories;

namespace PetClinix.Modules.Pets.Application.UseCases.GetTutors;

public sealed class GetTutorsQueryHandler : ICommandHandler<GetTutorsQuery, Result<PagedResult<TutorResponse>>>
{
    private readonly ITutorRepository _tutorRepository;

    public GetTutorsQueryHandler(ITutorRepository tutorRepository)
    {
        _tutorRepository = tutorRepository;
    }

    public async Task<Result<PagedResult<TutorResponse>>> Handle(GetTutorsQuery query, CancellationToken cancellationToken)
    {
        var (tutors, totalCount) = await _tutorRepository.GetAllByClinicIdAsync(query.ClinicId, query.PageNumber, query.PageSize, cancellationToken);

        var response = tutors.Select(t => new TutorResponse(
            t.Id,
            t.Name,
            t.Cpf.Value,
            t.Email,
            t.PhoneNumber,
            t.SecondaryPhoneNumber,
            t.City,
            t.State,
            t.ZipCode,
            t.Street,
            t.Number,
            t.Neighborhood,
            t.Complement,
            t.Notes,
            t.IsActive
        )).ToList();

        var pagedResult = new PagedResult<TutorResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<TutorResponse>>.Success(pagedResult);
    }
}