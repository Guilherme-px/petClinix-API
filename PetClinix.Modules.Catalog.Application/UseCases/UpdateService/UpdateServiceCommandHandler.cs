using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Application.Contracts;
using PetClinix.Modules.Catalog.Domain.Exceptions;
using PetClinix.Modules.Catalog.Domain.Repositories;

namespace PetClinix.Modules.Catalog.Application.UseCases.UpdateService;

public sealed class UpdateServiceCommandHandler : ICommandHandler<UpdateServiceCommand, Result>
{
    private readonly IServiceRepository _serviceRepository;
    private readonly ICatalogUnitOfWork _unitOfWork;

    public UpdateServiceCommandHandler(IServiceRepository serviceRepository, ICatalogUnitOfWork unitOfWork)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        var service = await _serviceRepository.GetByIdAsync(command.ClinicId, command.ServiceId, cancellationToken);

        if (service == null)
        {
            return Result.Failure("catalog.service.not_found", "Serviço não encontrado nesta clínica.");
        }

        try
        {
            service.UpdateInfo(
                command.UpdatedByUserId,
                command.Name,
                command.Description,
                command.DurationInMinutes,
                command.Price,
                command.RequiresVeterinarian
            );

            await _serviceRepository.UpdateAsync(service, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (CatalogDomainException ex)
        {
            return Result.Failure(ex.Code, ex.Message);
        }
    }
}