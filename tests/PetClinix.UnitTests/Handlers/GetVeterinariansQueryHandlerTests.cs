using FluentAssertions;
using NSubstitute;
using PetClinix.Modules.Identity.Application.UseCases.GetStaff;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class GetVeterinariansQueryHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly GetVeterinariansQueryHandler _handler;

    public GetVeterinariansQueryHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _handler = new GetVeterinariansQueryHandler(_userRepositoryMock);
    }

    private static User CreateUser(Guid clinicId, string name) =>
    User.CreateStaff(
        clinicId, Guid.NewGuid(), name,
        $"{name.ToLower().Replace(" ", "")}@petclinix.com", "hash",
        "12345678900", "11999998888", new DateOnly(1990, 1, 1), UserRole.Veterinarian
    );

    [Fact]
    public async Task Handle_Should_ReturnMappedVeterinarians_When_ClinicHasVets()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetVeterinariansQuery(clinicId);

        _userRepositoryMock.GetActiveByRoleAsync(clinicId, UserRole.Veterinarian, Arg.Any<CancellationToken>())
            .Returns(new List<User>
            {
                CreateUser(clinicId, "Dr. João"),
                CreateUser(clinicId, "Dra. Maria"),
            });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Id.Should().NotBeEmpty();
        result.Value[0].Name.Should().Be("Dr. João");
        result.Value[1].Name.Should().Be("Dra. Maria");
    }

    [Fact]
    public async Task Handle_Should_QueryWithVeterinarianRole()
    {
        var clinicId = Guid.NewGuid();
        var query = new GetVeterinariansQuery(clinicId);

        _userRepositoryMock.GetActiveByRoleAsync(clinicId, UserRole.Veterinarian, Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        await _handler.Handle(query, CancellationToken.None);

        await _userRepositoryMock.Received(1).GetActiveByRoleAsync(
            clinicId, UserRole.Veterinarian, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_When_ClinicHasNoVets()
    {
        var query = new GetVeterinariansQuery(Guid.NewGuid());

        _userRepositoryMock.GetActiveByRoleAsync(Arg.Any<Guid>(), Arg.Any<UserRole>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}