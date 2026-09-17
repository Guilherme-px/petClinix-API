using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Catalog.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Controllers;

public class ServicesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ServicesControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Email, string Password, Guid UserId, Guid ClinicId)> SetupAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid()}@teste.com";
        var password = "SenhaForte@123";

        var clinicRequest = new
        {
            TradeName = $"Clinica Serv {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Serv LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Serv",
            AdminEmail = email,
            AdminDocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            AdminPhoneNumber = "11999990000",
            AdminBirthDate = new DateOnly(1990, 1, 1)
        };

        var clinicResponse = await _client.PostAsJsonAsync("/api/clinics", clinicRequest);
        var clinicResult = await clinicResponse.Content.ReadFromJsonAsync<RegisterClinicResponse>();
        var clinicId = clinicResult!.ClinicId;

        using (var scope = _factory.Services.CreateScope())
        {
            var billingDb = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subscription = PetClinix.Modules.Billing.Domain.Entities.Subscription.Create(
                clinicId, $"cus_test_{Guid.NewGuid()}", $"sub_test_{Guid.NewGuid()}", PetClinix.Modules.Billing.Domain.Enums.PlanTier.Basic);
            await billingDb.Subscriptions.AddAsync(subscription);
            await billingDb.SaveChangesAsync();
        }

        var tokenResponse = await _client.GetAsync($"/api/users/{email}/generate-reset-token");
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var setPasswordRequest = new { Token = tokenResult!.Token, Password = password };
        await _client.PostAsJsonAsync("/api/users/set-password", setPasswordRequest);

        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(email);
            var user = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);
            userId = user!.Id;
        }

        return (email, password, userId, clinicId);
    }

    private record AuthenticatedContext(Guid UserId, Guid ClinicId);

    private async Task<AuthenticatedContext> AuthenticateAsync()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return new AuthenticatedContext(userId, clinicId);
    }

    private Task<HttpResponseMessage> RegisterServiceAsync(
        string name, string description, int durationInMinutes, decimal price, bool requiresVeterinarian) =>
        _client.PostAsJsonAsync("/api/services", new
        {
            Name = name,
            Description = description,
            DurationInMinutes = durationInMinutes,
            Price = price,
            RequiresVeterinarian = requiresVeterinarian
        });

    private async Task<Guid> GetFirstServiceIdAsync(Guid clinicId)
    {
        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var savedService = await catalogDb.Services.FirstOrDefaultAsync(s => s.ClinicId == clinicId);
        return savedService!.Id;
    }

    [Fact]
    public async Task RegisterService_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.PostAsJsonAsync("/api/services", new
        {
            Name = "Vacina V3",
            Description = "Vacina antirrábica",
            DurationInMinutes = 15,
            Price = 80.0m,
            RequiresVeterinarian = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterService_Should_Return_204_And_Save_Service_When_Valid()
    {
        var auth = await AuthenticateAsync();

        var response = await RegisterServiceAsync("Consulta Clínica Geral", "Consulta padrão com veterinário.", 30, 150.0m, true);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var savedService = await catalogDb.Services.FirstOrDefaultAsync(s => s.ClinicId == auth.ClinicId);

        savedService.Should().NotBeNull();
        savedService!.Name.Should().Be("Consulta Clínica Geral");
        savedService.DurationInMinutes.Should().Be(30);
        savedService.Price.Should().Be(150.0m);
        savedService.RequiresVeterinarian.Should().BeTrue();
        savedService.CreatedByUserId.Should().Be(auth.UserId);
    }

    [Fact]
    public async Task RegisterService_Should_Return_400_When_Name_Duplicated()
    {
        await AuthenticateAsync();

        var firstResponse = await RegisterServiceAsync("Vacina Duplicate", "Teste de duplicidade", 10, 50.0m, true);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await RegisterServiceAsync("Vacina Duplicate", "Teste de duplicidade", 10, 50.0m, true);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("catalog.service.name_already_exists");
    }

    [Fact]
    public async Task GetServices_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync("/api/services");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetServices_Should_Return_200_And_Service_List_When_Valid()
    {
        await AuthenticateAsync();

        await RegisterServiceAsync("Vacina V3 Listar", "Vacina para teste de lista", 15, 80.0m, true);

        var response = await _client.GetAsync("/api/services");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedServiceResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.Items.Should().ContainSingle(s => s.Name == "Vacina V3 Listar");
    }

    [Fact]
    public async Task GetServices_Should_Return_Only_Matching_When_Search_Provided()
    {
        await AuthenticateAsync();

        await RegisterServiceAsync("Banho e Tosa", "Higiene completa", 60, 90.0m, false);
        await RegisterServiceAsync("Consulta Veterinária", "Consulta geral", 30, 150.0m, true);
        await RegisterServiceAsync("Vacina V10", "Vacinação", 15, 80.0m, true);

        var response = await _client.GetAsync("/api/services?search=banho");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedServiceResponse>();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(s => s.Name == "Banho e Tosa");
    }

    [Fact]
    public async Task GetServices_Should_Return_TotalCount_Filtered_When_Search_Provided()
    {
        await AuthenticateAsync();

        await RegisterServiceAsync("Vacina V3", "Antirrábica", 15, 80.0m, true);
        await RegisterServiceAsync("Vacina V10", "Polivalente", 15, 90.0m, true);
        await RegisterServiceAsync("Banho", "Higiene", 45, 70.0m, false);

        var response = await _client.GetAsync("/api/services?search=vacina");

        var result = await response.Content.ReadFromJsonAsync<PagedServiceResponse>();
        result!.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(s => s.Name.Contains("Vacina"));
    }

    [Fact]
    public async Task GetServices_Should_Return_Empty_List_When_Search_Has_No_Match()
    {
        await AuthenticateAsync();

        await RegisterServiceAsync("Consulta", "Geral", 30, 150.0m, true);

        var response = await _client.GetAsync("/api/services?search=cirurgia");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedServiceResponse>();
        result!.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetServices_Should_Return_All_When_Search_Is_Empty()
    {
        await AuthenticateAsync();

        await RegisterServiceAsync("Banho", "Higiene", 45, 70.0m, false);
        await RegisterServiceAsync("Tosa", "Corte", 45, 70.0m, false);

        var response = await _client.GetAsync("/api/services?search=");

        var result = await response.Content.ReadFromJsonAsync<PagedServiceResponse>();
        result!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateService_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            Name = "Consulta Atualizada",
            Description = "Desc Atualizada",
            DurationInMinutes = 45,
            Price = 200.0m,
            RequiresVeterinarian = false
        };

        var response = await _client.PutAsJsonAsync($"/api/services/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateService_Should_Return_400_When_Service_Does_Not_Exist()
    {
        await AuthenticateAsync();

        var updateRequest = new
        {
            Name = "Consulta Atualizada",
            Description = "Desc Atualizada",
            DurationInMinutes = 45,
            Price = 200.0m,
            RequiresVeterinarian = false
        };

        var response = await _client.PutAsJsonAsync($"/api/services/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("catalog.service.not_found");
    }

    [Fact]
    public async Task UpdateService_Should_Return_204_And_Update_Db_When_Valid()
    {
        var auth = await AuthenticateAsync();

        await RegisterServiceAsync("Vacina Original", "Desc Original", 15, 80.0m, true);
        var serviceId = await GetFirstServiceIdAsync(auth.ClinicId);

        var updateRequest = new
        {
            Name = "Vacina Atualizada",
            Description = "Nova descrição",
            DurationInMinutes = 30,
            Price = 120.0m,
            RequiresVeterinarian = false
        };

        var response = await _client.PutAsJsonAsync($"/api/services/{serviceId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var updatedService = await catalogDb.Services.FirstOrDefaultAsync(s => s.Id == serviceId);

        updatedService.Should().NotBeNull();
        updatedService!.Name.Should().Be("Vacina Atualizada");
        updatedService.DurationInMinutes.Should().Be(30);
        updatedService.Price.Should().Be(120.0m);
        updatedService.RequiresVeterinarian.Should().BeFalse();
        updatedService.UpdatedByUserId.Should().Be(auth.UserId);
    }

    [Fact]
    public async Task DeactivateService_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.DeleteAsync($"/api/services/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateService_Should_Return_400_When_Service_Does_Not_Exist()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/services/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("catalog.service.not_found");
    }

    [Fact]
    public async Task DeactivateService_Should_Return_204_And_Set_Inactive_In_Db_When_Valid()
    {
        var auth = await AuthenticateAsync();

        await RegisterServiceAsync("Servico Delete", "Desc Delete", 15, 50.0m, true);
        var serviceId = await GetFirstServiceIdAsync(auth.ClinicId);

        var response = await _client.DeleteAsync($"/api/services/{serviceId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var deactivatedService = await catalogDb.Services.FirstOrDefaultAsync(s => s.Id == serviceId);

        deactivatedService.Should().NotBeNull();
        deactivatedService!.IsActive.Should().BeFalse();
    }
}

public class PagedServiceResponse
{
    public List<ServiceItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class ServiceItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationInMinutes { get; set; }
    public decimal Price { get; set; }
    public bool RequiresVeterinarian { get; set; }
}