using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Infrastructure.Persistence;

namespace PetClinix.IntegrationTests.Controllers;

public class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Email, string Password)> SetupUserWithPasswordAsync()
    {
        var email = $"login_{Guid.NewGuid()}@teste.com";
        var password = "SenhaForte@123";

        var clinicRequest = new
        {
            TradeName = $"Clinica Login {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Login LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Login",
            AdminEmail = email,
            AdminDocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            AdminPhoneNumber = "11999990000",
            AdminBirthDate = new DateOnly(1990, 1, 1)
        };

        var clinicResponse = await _client.PostAsJsonAsync("/api/clinics", clinicRequest);

        if (!clinicResponse.IsSuccessStatusCode)
        {
            var error = await clinicResponse.Content.ReadAsStringAsync();
            throw new Exception($"Falha ao cadastrar clínica no setup do teste: {error}");
        }

        var clinicResult = await clinicResponse.Content.ReadFromJsonAsync<RegisterClinicResponse>();
        var clinicId = clinicResult!.ClinicId;

        using (var scope = _factory.Services.CreateScope())
        {
            var billingDb = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subscription = Subscription.Create(clinicId, $"cus_test_{Guid.NewGuid()}", $"sub_test_{Guid.NewGuid()}", PlanTier.Essential);
            await billingDb.Subscriptions.AddAsync(subscription);
            await billingDb.SaveChangesAsync();
        }

        var tokenResponse = await _client.GetAsync($"/api/users/{email}/generate-reset-token");
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var setPasswordRequest = new { Token = tokenResult!.Token, Password = password };
        await _client.PostAsJsonAsync("/api/users/set-password", setPasswordRequest);

        return (email, password);
    }

    [Fact]
    public async Task Login_Should_Return_200_And_Token_When_Credentials_Are_Valid()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(email);
        result.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_Should_Return_401_When_Password_Is_Invalid()
    {
        var (email, _) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = "SenhaErrada@123" };
        var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_Should_Return_200_And_NewTokens_When_Valid()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new { RefreshToken = loginResult!.RefreshToken };
        var response = await _client.PostAsJsonAsync("/api/users/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        result.RefreshToken.Should().NotBe(loginResult.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_Return_401_When_Invalid()
    {
        var refreshRequest = new { RefreshToken = "token-invalido-fake-123" };
        var response = await _client.PostAsJsonAsync("/api/users/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_Should_Return_200_And_Profile_Data_When_Token_Is_Valid()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        result.Should().NotBeNull();
        result!.Email.Should().Be(email);
        result.Role.Should().Be("Admin");
        result.Clinic.Should().NotBeNull();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            Name = "Novo Nome",
            PhoneNumber = "11912345678",
            BirthDate = new DateOnly(1990, 1, 1)
        };

        var response = await _client.PutAsJsonAsync("/api/users/me", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_Should_Return_204_And_Update_Db_When_Valid()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var updateRequest = new
        {
            Name = "Nome Alterado no Teste",
            PhoneNumber = "11912345678",
            BirthDate = new DateOnly(1991, 5, 15)
        };

        var response = await _client.PutAsJsonAsync("/api/users/me", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(email);
        var savedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == emailVo);

        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Nome Alterado no Teste");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateClinic_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            TradeName = "Novo Nome",
            LegalName = "Nova Razao",
            DocumentNumber = "12345678000199",
            Email = "novo@clinica.com",
            PhoneNumber = "11912345678",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = "",
            City = "SP",
            State = "SP"
        };

        var response = await _client.PutAsJsonAsync("/api/clinics/me", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateClinic_Should_Return_204_And_Update_Db_When_Valid_Admin()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var updateRequest = new
        {
            TradeName = "Clinica Alterada no Teste",
            LegalName = "Razao Alterada",
            DocumentNumber = "12345678000199",
            Email = "alterada@clinica.com",
            PhoneNumber = "11912345678",
            ZipCode = "01001000",
            Street = "Rua Nova",
            Number = "100",
            Neighborhood = "Centro",
            Complement = "Sala 2",
            City = "Sao Paulo",
            State = "SP"
        };

        var response = await _client.PutAsJsonAsync("/api/clinics/me", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create("alterada@clinica.com");
        var savedClinic = await db.Clinics.FirstOrDefaultAsync(c => c.Email == emailVo);

        savedClinic.Should().NotBeNull();
        savedClinic!.TradeName.Should().Be("Clinica Alterada no Teste");
        savedClinic.City.Should().Be("Sao Paulo");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAccount_Should_Return_204_And_Update_Both_User_And_Clinic_In_Db()
    {
        var (email, password) = await SetupUserWithPasswordAsync();

        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var updateRequest = new
        {
            UserName = "Nome Alterado",
            UserPhoneNumber = "11999998888",
            UserBirthDate = new DateOnly(1991, 5, 15),
            ClinicTradeName = "Clinica Alterada no Teste",
            ClinicLegalName = "Razao Alterada",
            ClinicDocumentNumber = "12345678000199",
            ClinicEmail = "alterada@clinica.com",
            ClinicPhoneNumber = "11912345678",
            ClinicZipCode = "01001000",
            ClinicStreet = "Rua Nova",
            ClinicNumber = "100",
            ClinicNeighborhood = "Centro",
            ClinicComplement = "Sala 2",
            ClinicCity = "Sao Paulo",
            ClinicState = "SP"
        };

        var response = await _client.PutAsJsonAsync("/api/account/me", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(email);
        var savedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == emailVo);

        var clinicEmailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create("alterada@clinica.com");
        var savedClinic = await db.Clinics.FirstOrDefaultAsync(c => c.Email == clinicEmailVo);

        savedUser.Should().NotBeNull();
        savedUser!.Name.Should().Be("Nome Alterado");

        savedClinic.Should().NotBeNull();
        savedClinic!.TradeName.Should().Be("Clinica Alterada no Teste");

        _client.DefaultRequestHeaders.Authorization = null;
    }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}


public class ProfileResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public ClinicResponse Clinic { get; set; } = new();
}

public class ClinicResponse
{
    public Guid ClinicId { get; set; }
    public string TradeName { get; set; } = string.Empty;
}