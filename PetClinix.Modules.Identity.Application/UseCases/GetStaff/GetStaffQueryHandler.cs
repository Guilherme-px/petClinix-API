using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaff;

public sealed class GetStaffQueryHandler : ICommandHandler<GetStaffQuery, Result<PagedResult<StaffResponse>>>
{
    private readonly IUserRepository _userRepository;

    public GetStaffQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PagedResult<StaffResponse>>> Handle(GetStaffQuery query, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _userRepository.GetAllByClinicIdAsync(query.ClinicId, query.PageNumber, query.PageSize, query.Search, cancellationToken);

        var response = users.Select(u => new StaffResponse(
            u.Id,
            u.Name,
            u.Email.Value,
            u.PhoneNumber.Value,
            u.DocumentNumber,
            u.BirthDate,
            u.Role,
            u.IsActive)).ToList();

        var pagedResult = new PagedResult<StaffResponse>(response, totalCount, query.PageNumber, query.PageSize);

        return Result<PagedResult<StaffResponse>>.Success(pagedResult);
    }
}