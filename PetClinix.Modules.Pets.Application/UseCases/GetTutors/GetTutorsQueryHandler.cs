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
        var (tutors, totalCount) = await _tutorRepository.GetAllByClinicIdAsync(
            query.ClinicId, query.PageNumber, query.PageSize, query.Search, cancellationToken);

        var response = tutors.Select(t => new TutorResponse(
            Id: t.Id,
            Name: t.Name,
            Cpf: t.Cpf.Value,
            Email: t.Email,
            PhoneNumber: t.PhoneNumber,
            SecondaryPhoneNumber: t.SecondaryPhoneNumber,
            ZipCode: t.ZipCode,
            Street: t.Street,
            Number: t.Number,
            Neighborhood: t.Neighborhood,
            Complement: t.Complement,
            City: t.City,
            State: t.State,
            Notes: t.Notes,
            IsActive: t.IsActive
        )).ToList();

        var pagedResult = new PagedResult<TutorResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<TutorResponse>>.Success(pagedResult);
    }
}