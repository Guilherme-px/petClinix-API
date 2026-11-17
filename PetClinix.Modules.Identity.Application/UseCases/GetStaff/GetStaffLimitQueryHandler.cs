using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Domain.Repositories;

namespace PetClinix.Modules.Identity.Application.UseCases.GetStaffLimit;

public sealed class GetStaffLimitQueryHandler : ICommandHandler<GetStaffLimitQuery, Result<StaffLimitResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ISubscriptionStatusService _subscriptionStatusService;

    public GetStaffLimitQueryHandler(
        IUserRepository userRepository,
        ISubscriptionStatusService subscriptionStatusService)
    {
        _userRepository = userRepository;
        _subscriptionStatusService = subscriptionStatusService;
    }

    public async Task<Result<StaffLimitResponse>> Handle(GetStaffLimitQuery query, CancellationToken cancellationToken)
    {
        var used = await _userRepository.CountActiveVeterinariansAsync(query.ClinicId, cancellationToken);
        var staffLimit = await _subscriptionStatusService.GetStaffLimitAsync(query.ClinicId, cancellationToken);

        int? limit = staffLimit is > 0 and not int.MaxValue ? staffLimit : null;

        return Result<StaffLimitResponse>.Success(new StaffLimitResponse(used, limit));
    }
}