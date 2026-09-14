using FluentAssertions;
using NSubstitute;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Application.UseCases.UpdateAccount;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Repositories;
using PetClinix.Modules.Identity.Domain.ValueObjects;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetClinix.UnitTests.Handlers;

public class UpdateAccountCommandHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IClinicRepository _clinicRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IPasswordHasher _passwordHasherMock;
    private readonly UpdateAccountCommandHandler _handler;

    public UpdateAccountCommandHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _clinicRepositoryMock = Substitute.For<IClinicRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _passwordHasherMock = Substitute.For<IPasswordHasher>();
        _handler = new UpdateAccountCommandHandler(_userRepositoryMock, _clinicRepositoryMock, _unitOfWorkMock, _passwordHasherMock);
    }

    private static User CreateValidUser(Guid clinicId)
    {
        return User.CreateAdmin(
            clinicId, Guid.NewGuid(), "Admin", "admin@test.com", "hash", "12345678900",
            "11999990000", new DateOnly(1990, 1, 1)
        );
    }

    private static Clinic CreateValidClinic(Guid clinicId)
    {
        return Clinic.Create(
            "Clinica Teste", "Teste LTDA", "12345678000199",
            ClinicSlug.Create("clinica-teste"),
            "clinica@teste.com", "11988887777",
            "01001000", "Rua Teste", "123", "Centro",
            null, "SP", "SP"
        );
    }

    private static UpdateAccountCommand CreateValidCommand(Guid userId, Guid clinicId) => new(
        userId, clinicId,
        "Novo Nome", "1188887777", new DateOnly(1991, 5, 10),
        "NewPassword@123",
        "Nova Clinica", "Nova Razao", "98765432000111",
        "novo@email.com", "1177778888",
        "01001000", "Nova Rua", "999", "Novo Bairro",
        null, "Santos", "SP"
    );

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_User_Does_Not_Exist()
    {
        var command = CreateValidCommand(Guid.NewGuid(), Guid.NewGuid());

        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.user.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Clinic_Does_Not_Exist()
    {
        var clinicId = Guid.NewGuid();
        var user = CreateValidUser(clinicId);
        var command = CreateValidCommand(user.Id, clinicId);

        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _clinicRepositoryMock.GetByIdAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns((Clinic?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("identity.clinic.not_found");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnSuccess_And_Call_SaveChanges_When_Valid()
    {
        var clinicId = Guid.NewGuid();
        var user = CreateValidUser(clinicId);
        var clinic = CreateValidClinic(clinicId);
        var command = CreateValidCommand(user.Id, clinicId);

        _userRepositoryMock.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _clinicRepositoryMock.GetByIdAsync(command.ClinicId, Arg.Any<CancellationToken>()).Returns(clinic);
        _passwordHasherMock.Hash(Arg.Any<string>()).Returns("hashed-password");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Name.Should().Be(command.UserName);
        clinic.TradeName.Should().Be(command.ClinicTradeName);

        await _userRepositoryMock.Received(1).UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _clinicRepositoryMock.Received(1).UpdateAsync(Arg.Any<Clinic>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}