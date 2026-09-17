using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Catalog.Application.UseCases.GetServices;
using PetClinix.Modules.Catalog.Domain.Entities;
using PetClinix.Modules.Catalog.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetServicesQueryHandlerTests
{
    private readonly IServiceRepository _serviceRepositoryMock;
    private readonly GetServicesQueryHandler _handler;

    public GetServicesQueryHandlerTests()
    {
        _serviceRepositoryMock = Substitute.For<IServiceRepository>();
        _handler = new GetServicesQueryHandler(_serviceRepositoryMock);
    }

    private static Service CreateValidService(Guid clinicId)
    {
        return Service.Create(
            clinicId, Guid.NewGuid(), "Consulta", "Consulta Geral", 30, 150.0m, true
        );
    }

    [Fact]
    public async Task Handle_Should_Return_PagedResult_With_Correct_Data()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetServicesQuery(clinicId, 1, 10);

        var services = new List<Service>
    {
        CreateValidService(clinicId),
        CreateValidService(clinicId)
    };

        _serviceRepositoryMock.GetAllByClinicIdAsync(clinicId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((services, 2));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Services_Exist()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetServicesQuery(clinicId, 1, 10);

        _serviceRepositoryMock.GetAllByClinicIdAsync(clinicId, 1, 10, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new List<Service>(), 0));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Pass_Search_Term_To_Repository()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetServicesQuery(clinicId, 1, 10, "Consulta");

        _serviceRepositoryMock.GetAllByClinicIdAsync(clinicId, 1, 10, "Consulta", Arg.Any<CancellationToken>())
            .Returns((new List<Service>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _serviceRepositoryMock.Received(1).GetAllByClinicIdAsync(
            clinicId, 1, 10, "Consulta", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_Pass_Empty_Search_When_Not_Provided()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetServicesQuery(clinicId, 1, 10);

        _serviceRepositoryMock.GetAllByClinicIdAsync(clinicId, 1, 10, "", Arg.Any<CancellationToken>())
            .Returns((new List<Service>(), 0));

        await _handler.Handle(query, CancellationToken.None);

        await _serviceRepositoryMock.Received(1).GetAllByClinicIdAsync(
            clinicId, 1, 10, "", Arg.Any<CancellationToken>());
    }
}