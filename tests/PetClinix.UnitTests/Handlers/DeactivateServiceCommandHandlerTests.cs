using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Catalog.Application.Contracts;
using PetClinix.Modules.Catalog.Application.UseCases.DeactivateService;
using PetClinix.Modules.Catalog.Domain.Entities;
using PetClinix.Modules.Catalog.Domain.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class DeactivateServiceCommandHandlerTests
{
    private readonly IServiceRepository _serviceRepositoryMock;
    private readonly ICatalogUnitOfWork _unitOfWorkMock;
    private readonly DeactivateServiceCommandHandler _handler;
    private readonly IAppointmentDependencyChecker _appointmentDependencyCheckerMock;

    public DeactivateServiceCommandHandlerTests()
    {
        _serviceRepositoryMock = Substitute.For<IServiceRepository>();
        _unitOfWorkMock = Substitute.For<ICatalogUnitOfWork>();
        _appointmentDependencyCheckerMock = Substitute.For<IAppointmentDependencyChecker>();
        _handler = new DeactivateServiceCommandHandler(
            _serviceRepositoryMock,
            _unitOfWorkMock,
            _appointmentDependencyCheckerMock
        );
    }

    private static Service CreateValidService(Guid clinicId)
    {
        return Service.Create(
            clinicId, Guid.NewGuid(), "Consulta Teste", "Descrição", 30, 100.0m, true
        );
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Service_Does_Not_Exist()
    {
        var command = new DeactivateServiceCommand(Guid.NewGuid(), Guid.NewGuid());

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns((Service?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.not_found");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Service_Belongs_To_Another_Clinic()
    {
        var myClinicId = Guid.NewGuid();
        var otherClinicId = Guid.NewGuid();
        var service = CreateValidService(otherClinicId);
        var command = new DeactivateServiceCommand(myClinicId, service.Id);

        _serviceRepositoryMock
            .GetByIdAsync(myClinicId, service.Id, Arg.Any<CancellationToken>())
            .Returns((Service?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.not_found");

        await _appointmentDependencyCheckerMock.DidNotReceive()
            .HasFutureAppointmentsForServiceAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Service_Has_Future_Appointments()
    {
        var clinicId = Guid.NewGuid();
        var service = CreateValidService(clinicId);
        var command = new DeactivateServiceCommand(clinicId, service.Id);

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns(service);
        _appointmentDependencyCheckerMock
            .HasFutureAppointmentsForServiceAsync(command.ClinicId, service.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("catalog.service.has_future_appointments");
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Deactivate_Service_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var service = CreateValidService(clinicId);
        var command = new DeactivateServiceCommand(clinicId, service.Id);

        _serviceRepositoryMock
            .GetByIdAsync(command.ClinicId, command.ServiceId, Arg.Any<CancellationToken>())
            .Returns(service);
        _appointmentDependencyCheckerMock
            .HasFutureAppointmentsForServiceAsync(command.ClinicId, service.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        service.IsActive.Should().BeFalse();
    }
}