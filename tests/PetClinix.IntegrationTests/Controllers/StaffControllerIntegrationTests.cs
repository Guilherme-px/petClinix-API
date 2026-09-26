using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Controllers;

public class StaffControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public StaffControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Email, string Password)> SetupAdminWithSubscriptionAsync()
    {
        var email = $"admin_{Guid.NewGuid()}@teste.com";
        var password = "SenhaNova@123";

        var clinicRequest = new
        {
            TradeName = $"Clinica Staff {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Staff LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Staff",
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
            var subscription = Subscription.Create(
                clinicId, $"cus_test_{Guid.NewGuid()}", $"sub_test_{Guid.NewGuid()}", PlanTier.Basic);
            await billingDb.Subscriptions.AddAsync(subscription);
            await billingDb.SaveChangesAsync();
        }

        var tokenResponse = await _client.GetAsync($"/api/users/{email}/generate-reset-token");
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var setPasswordRequest = new { Token = tokenResult!.Token, Password = password };
        await _client.PostAsJsonAsync("/api/users/set-password", setPasswordRequest);

        return (email, password);
    }

    private record AuthenticatedContext(Guid UserId, Guid ClinicId);

    private async Task<AuthenticatedContext> AuthenticateAsync()
    {
        var (email, password, userId, clinicId) = await SetupAdminWithStaffAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return new AuthenticatedContext(userId, clinicId);
    }

    private async Task<(string Email, string Password, Guid UserId, Guid ClinicId)> SetupAdminWithStaffAsync()
    {
        var (email, password) = await SetupAdminWithSubscriptionAsync();

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(email);
        var user = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);

        return (email, password, user!.Id, user.ClinicId);
    }

    private Task<HttpResponseMessage> RegisterStaffAsync(string name, string email) =>
        _client.PostAsJsonAsync("/api/clinics/me/staff", new
        {
            Name = name,
            Email = email,
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            PhoneNumber = "11999990000",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = "Veterinarian"
        });

    private async Task<Guid> GetStaffUserIdByEmailAsync(string staffEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(staffEmail);
        var savedUser = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);
        return savedUser!.Id;
    }

    [Fact]
    public async Task RegisterStaff_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.PostAsJsonAsync("/api/clinics/me/staff", new
        {
            Name = "Dr. Teste",
            Email = "dr@teste.com",
            DocumentNumber = "12345678900",
            PhoneNumber = "11999990000",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = "Veterinarian"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterStaff_Should_Return_200_And_Create_User_When_Valid()
    {
        await AuthenticateAsync();

        var staffEmail = $"vet_{Guid.NewGuid()}@teste.com";
        var response = await RegisterStaffAsync("Dr. Dolittle", staffEmail);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(staffEmail);
        var savedUser = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);

        savedUser.Should().NotBeNull();
        savedUser!.Role.Should().Be(UserRole.Veterinarian);
        savedUser.PasswordHash.Should().BeNull();
    }

    [Fact]
    public async Task RegisterStaff_Should_Return_400_When_Plan_Limit_Reached()
    {
        await AuthenticateAsync();

        for (int i = 0; i < 3; i++)
        {
            await RegisterStaffAsync($"Staff {i}", $"staff_{i}_{Guid.NewGuid()}@teste.com");
        }

        var limitResponse = await RegisterStaffAsync("Staff 5", $"staff5_{Guid.NewGuid()}@teste.com");

        limitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await limitResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("identity.staff_limit_reached");
    }

    [Fact]
    public async Task GetStaff_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync("/api/clinics/me/staff");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStaff_Should_Return_200_And_Staff_List_When_Valid()
    {
        await AuthenticateAsync();

        await RegisterStaffAsync("Dr. Alguém", $"vet_{Guid.NewGuid()}@teste.com");

        var response = await _client.GetAsync("/api/clinics/me/staff");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedStaffResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetStaff_Should_Return_Only_Matching_When_Search_Provided()
    {
        await AuthenticateAsync();

        await RegisterStaffAsync("Joao Veterinario", $"joao_{Guid.NewGuid()}@teste.com");
        await RegisterStaffAsync("Maria Auxiliar", $"maria_{Guid.NewGuid()}@teste.com");

        var response = await _client.GetAsync("/api/clinics/me/staff?search=joao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedStaffResponse>();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(s => s.Name == "Joao Veterinario");
    }

    [Fact]
    public async Task GetStaff_Should_Return_All_When_Search_Is_Empty()
    {
        await AuthenticateAsync();

        await RegisterStaffAsync("João Veterinário", $"joao_{Guid.NewGuid()}@teste.com");
        await RegisterStaffAsync("Maria Auxiliar", $"maria_{Guid.NewGuid()}@teste.com");

        var response = await _client.GetAsync("/api/clinics/me/staff?search=");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedStaffResponse>();
        result!.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetStaffById_Should_Return_404_When_User_Does_Not_Exist()
    {
        await AuthenticateAsync();

        var fakeUserId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/clinics/me/staff/{fakeUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateStaff_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            Name = "Nome Novo",
            PhoneNumber = "11912345678",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = "Veterinarian"
        };

        var response = await _client.PutAsJsonAsync($"/api/clinics/me/staff/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateStaff_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        await AuthenticateAsync();

        var updateRequest = new
        {
            Name = "Nome Novo",
            PhoneNumber = "11912345678",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = "Veterinarian"
        };

        var fakeUserId = Guid.NewGuid();
        var response = await _client.PutAsJsonAsync($"/api/clinics/me/staff/{fakeUserId}", updateRequest);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateStaff_Should_Return_204_And_Update_Db_When_Valid()
    {
        await AuthenticateAsync();

        var staffEmail = $"vet_{Guid.NewGuid()}@teste.com";
        await RegisterStaffAsync("Dr. Original", staffEmail);
        var staffUserId = await GetStaffUserIdByEmailAsync(staffEmail);

        var updateRequest = new
        {
            Name = "Dr. Atualizado",
            PhoneNumber = "11900000000",
            BirthDate = new DateOnly(1986, 6, 11),
            Role = "Receptionist"
        };

        var response = await _client.PutAsJsonAsync($"/api/clinics/me/staff/{staffUserId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var updatedUser = await identityDb.Users.FirstOrDefaultAsync(u => u.Id == staffUserId);

        updatedUser.Should().NotBeNull();
        updatedUser!.Name.Should().Be("Dr. Atualizado");
        updatedUser.Role.Should().Be(UserRole.Receptionist);
    }

    [Fact]
    public async Task DeactivateStaff_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.DeleteAsync($"/api/clinics/me/staff/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateStaff_Should_Return_NotFound_When_User_Does_Not_Exist()
    {
        await AuthenticateAsync();

        var fakeUserId = Guid.NewGuid();
        var response = await _client.DeleteAsync($"/api/clinics/me/staff/{fakeUserId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeactivateStaff_Should_Return_204_And_Set_Inactive_In_Db_When_Valid()
    {
        await AuthenticateAsync();

        var staffEmail = $"vet_{Guid.NewGuid()}@teste.com";
        await RegisterStaffAsync("Dr. Teste Delete", staffEmail);
        var staffUserId = await GetStaffUserIdByEmailAsync(staffEmail);

        var response = await _client.DeleteAsync($"/api/clinics/me/staff/{staffUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var deactivatedUser = await identityDb.Users.FirstOrDefaultAsync(u => u.Id == staffUserId);

        deactivatedUser.Should().NotBeNull();
        deactivatedUser!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetStaff_Should_Find_Accented_Name_When_Searching_Without_Accents()
    {
        await AuthenticateAsync();

        await RegisterStaffAsync("João Veterinário", $"joao_{Guid.NewGuid()}@teste.com");
        await RegisterStaffAsync("Maria Auxiliar", $"maria_{Guid.NewGuid()}@teste.com");

        var response = await _client.GetAsync("/api/clinics/me/staff?search=joao");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedStaffResponse>();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(s => s.Name == "João Veterinário");
    }

    [Fact]
    public async Task GetStaff_Should_Return_Role_As_String_When_Valid()
    {
        await AuthenticateAsync();

        await RegisterStaffAsync("Dr. Teste Role", $"vet_{Guid.NewGuid()}@teste.com");

        var response = await _client.GetAsync("/api/clinics/me/staff");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedStaffResponse>();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(s => s.Role == "Veterinarian");
    }
}

public class StaffItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class PagedStaffResponse
{
    public List<StaffItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}