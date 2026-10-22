using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Application.Contracts;
using PetClinix.Modules.Catalog.Application.UseCases.UpdateService;
using PetClinix.Modules.Catalog.Domain.Entities;
using PetClinix.Modules.Catalog.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdateServiceCommandHandlerTests
{
    private readonly IServiceRepository _serviceRepositoryMock;
    private readonly ICatalogUnitOfWork _unitOfWorkMock;
    private readonly UpdateServiceCommandHandler _handler;

    public UpdateServiceCommandHandlerTests()
    {
        _serviceRepositoryMock = Substitute.For<IServiceRepository>();
        _unitOfWorkMock = Substitute.For<ICatalogUnitOfWork>();
        _handler = new UpdateServiceCommandHandler(_serviceRepositoryMock, _unitOfWorkMock);
    }

    private static Service CreateValidService(Guid clinicId)
    {
        return Service.Create(
            clinicId, Guid.NewGuid(), "Consulta Original", "Desc Original", 30, 100.0m, true
        );
    }

    private static UpdateServiceCommand CreateValidCommand(Guid clinicId, Guid serviceId, Guid userId) => new(
        clinicId, serviceId, userId, "Consulta Atualizada", "Desc Nova", 45, 120.0m, false
    );

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Service_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns((Service?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Query_Repository_Scoped_To_Command_Clinic()
    {
        var clinicId = Guid.NewGuid();
        var service = CreateValidService(clinicId);
        var command = CreateValidCommand(clinicId, service.Id, Guid.NewGuid());

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns(service);

        await _handler.Handle(command, CancellationToken.None);

        await _serviceRepositoryMock.Received(1).GetByIdAsync(
            command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Service_Belongs_To_Another_Clinic()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var service = CreateValidService(otherClinicId);
        var command = CreateValidCommand(myClinicId, service.Id, Guid.NewGuid());

        _serviceRepositoryMock
            .GetByIdAsync(myClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns((Service?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Domain_Throws_Exception()
    {
        var clinicId = Guid.NewGuid();
        var service = CreateValidService(clinicId);
        var command = CreateValidCommand(clinicId, service.Id, Guid.NewGuid()) with { DurationInMinutes = 0 };

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns(service);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.invalid_duration");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Update_Service_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var service = CreateValidService(clinicId);
        var userId = Guid.NewGuid();
        var command = CreateValidCommand(clinicId, service.Id, userId);

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns(service);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        service.Name.Should().Be(command.Name);
        service.DurationInMinutes.Should().Be(command.DurationInMinutes);
        service.UpdatedByUserId.Should().Be(userId);

        await _serviceRepositoryMock.Received(1).UpdateAsync(Arg.Any<Service>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}