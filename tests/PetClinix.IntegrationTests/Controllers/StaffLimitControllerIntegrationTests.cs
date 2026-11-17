using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Billing.Domain.Entities;
using PetClinix.Modules.Billing.Domain.Enums;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Domain.Entities;
using PetClinix.Modules.Identity.Domain.Enums;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Controllers;

public class StaffLimitControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    private const int ESSENTIAL_STAFF_LIMIT = 2;
    private const int PROFESSIONAL_STAFF_LIMIT = 5;
    private const string STAFF_LIMIT_URL = "/api/clinics/me/staff-limit";

    public StaffLimitControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Email, string Password)> CreateClinicAdminAsync(PlanTier? planTier)
    {
        var email = $"admin_{Guid.NewGuid()}@teste.com";
        var password = "SenhaNova@123";

        var clinicRequest = new
        {
            TradeName = $"Clinica Staff Limit {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Staff Limit LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Staff Limit",
            AdminEmail = email,
            AdminDocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            AdminPhoneNumber = "11999990000",
            AdminBirthDate = new DateOnly(1990, 1, 1)
        };

        var clinicResponse = await _client.PostAsJsonAsync("/api/clinics", clinicRequest);
        var clinicResult = await clinicResponse.Content.ReadFromJsonAsync<RegisterClinicResponse>();
        var clinicId = clinicResult!.ClinicId;

        if (planTier.HasValue)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var billingDb = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
                var subscription = Subscription.Create(
                    clinicId, $"cus_test_{Guid.NewGuid()}", $"sub_test_{Guid.NewGuid()}", planTier.Value);
                await billingDb.Subscriptions.AddAsync(subscription);
                await billingDb.SaveChangesAsync();
            }
        }

        var tokenResponse = await _client.GetAsync($"/api/users/{email}/generate-reset-token");
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var setPasswordRequest = new { Token = tokenResult!.Token, Password = password };
        await _client.PostAsJsonAsync("/api/users/set-password", setPasswordRequest);

        return (email, password);
    }

    private record AuthenticatedContext(Guid UserId, Guid ClinicId);

    private async Task<AuthenticatedContext> AuthenticateAsync(PlanTier? planTier = PlanTier.Professional)
    {
        var (email, password, userId, clinicId) = await SetupAdminWithStaffAsync(planTier);
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
        return new AuthenticatedContext(userId, clinicId);
    }

    private async Task<(string Email, string Password, Guid UserId, Guid ClinicId)> SetupAdminWithStaffAsync(PlanTier? planTier)
    {
        var (email, password) = await CreateClinicAdminAsync(planTier);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(email);
        var user = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);

        return (email, password, user!.Id, user.ClinicId);
    }

    private Task<HttpResponseMessage> RegisterStaffAsync(string name, string email, string role = "Veterinarian") =>
        _client.PostAsJsonAsync("/api/clinics/me/staff", new
        {
            Name = name,
            Email = email,
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            PhoneNumber = "11999990000",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = role
        });

    private async Task SeedStaffAsync(Guid clinicId, Guid createdByUserId, int quantity)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        for (int i = 0; i < quantity; i++)
        {
            var staffEmail = $"seed_staff_{i}_{Guid.NewGuid()}@teste.com";
            var staffUser = User.CreateStaff(
                clinicId,
                createdByUserId,
                $"Seed Staff {i}",
                staffEmail,
                null,
                Guid.NewGuid().ToString("N").Substring(0, 11),
                "11999990000",
                new DateOnly(1990, 1, 1),
                UserRole.Veterinarian);
            await identityDb.Users.AddAsync(staffUser);
        }

        await identityDb.SaveChangesAsync();
    }

    private async Task<Guid> GetStaffUserIdByEmailAsync(string staffEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(staffEmail);
        var savedUser = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);
        return savedUser!.Id;
    }

    private async Task LoginStaffAsync(string staffEmail)
    {
        var tokenResponse = await _client.GetAsync($"/api/users/{staffEmail}/generate-reset-token");
        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
        await _client.PostAsJsonAsync("/api/users/set-password", new
        {
            Token = tokenResult!.Token,
            Password = "SenhaStaff@123"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
        {
            Email = staffEmail,
            Password = "SenhaStaff@123"
        });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Return_403_When_Staff_Role_Calls()
    {
        await AuthenticateAsync();

        var staffEmail = $"vet_{Guid.NewGuid()}@teste.com";
        await RegisterStaffAsync("Dr. Staff Limite", staffEmail);
        await LoginStaffAsync(staffEmail);

        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Return_Used_And_Limit_When_Plan_Has_Limit()
    {
        var context = await AuthenticateAsync(PlanTier.Essential);

        await SeedStaffAsync(context.ClinicId, context.UserId, ESSENTIAL_STAFF_LIMIT);

        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StaffLimitPayload>();
        result!.Used.Should().Be(ESSENTIAL_STAFF_LIMIT);
        result.Limit.Should().Be(ESSENTIAL_STAFF_LIMIT);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Count_Only_Veterinarians_When_Receptionist_Registered()
    {
        var context = await AuthenticateAsync(PlanTier.Essential);

        await SeedStaffAsync(context.ClinicId, context.UserId, ESSENTIAL_STAFF_LIMIT);

        var receptionistResponse = await RegisterStaffAsync("Recepcao Contador", $"recepcao_{Guid.NewGuid()}@teste.com", "Receptionist");
        receptionistResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        var result = await response.Content.ReadFromJsonAsync<StaffLimitPayload>();
        result!.Used.Should().Be(ESSENTIAL_STAFF_LIMIT);
        result.Limit.Should().Be(ESSENTIAL_STAFF_LIMIT);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Decrement_Used_When_Vet_Is_Deactivated()
    {
        await AuthenticateAsync(PlanTier.Essential);

        var firstEmail = $"vet_{Guid.NewGuid()}@teste.com";
        await RegisterStaffAsync("Dr. Um", firstEmail);
        var secondEmail = $"vet_{Guid.NewGuid()}@teste.com";
        await RegisterStaffAsync("Dr. Dois", secondEmail);

        var beforeResponse = await _client.GetAsync(STAFF_LIMIT_URL);
        var before = await beforeResponse.Content.ReadFromJsonAsync<StaffLimitPayload>();
        before!.Used.Should().Be(ESSENTIAL_STAFF_LIMIT);

        var secondVetId = await GetStaffUserIdByEmailAsync(secondEmail);
        var deleteResponse = await _client.DeleteAsync($"/api/clinics/me/staff/{secondVetId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterResponse = await _client.GetAsync(STAFF_LIMIT_URL);

        var after = await afterResponse.Content.ReadFromJsonAsync<StaffLimitPayload>();
        after!.Used.Should().Be(ESSENTIAL_STAFF_LIMIT - 1);
        after.Limit.Should().Be(ESSENTIAL_STAFF_LIMIT);
    }

    [Fact]
    public async Task GetStaffLimit_Should_Return_Null_Limit_When_Plan_Is_Premium()
    {
        var context = await AuthenticateAsync(PlanTier.Premium);

        await SeedStaffAsync(context.ClinicId, context.UserId, PROFESSIONAL_STAFF_LIMIT);

        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StaffLimitPayload>();
        result!.Used.Should().Be(PROFESSIONAL_STAFF_LIMIT);
        result.Limit.Should().BeNull();
    }

    [Fact]
    public async Task GetStaffLimit_Should_Return_Null_Limit_When_Clinic_Has_No_Active_Subscription()
    {
        var context = await AuthenticateAsync(PlanTier.Essential);

        await SeedStaffAsync(context.ClinicId, context.UserId, ESSENTIAL_STAFF_LIMIT);

        using (var scope = _factory.Services.CreateScope())
        {
            var billingDb = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subscription = await billingDb.Subscriptions.FirstAsync(s => s.ClinicId == context.ClinicId);
            billingDb.Subscriptions.Remove(subscription);
            await billingDb.SaveChangesAsync();
        }

        var response = await _client.GetAsync(STAFF_LIMIT_URL);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StaffLimitPayload>();
        result!.Used.Should().Be(ESSENTIAL_STAFF_LIMIT);
        result.Limit.Should().BeNull();
    }

    [Fact]
    public async Task GetStaffLimit_Should_Count_Only_Own_Clinic_When_Other_Clinic_Has_Staff()
    {
        var first = await AuthenticateAsync(PlanTier.Essential);
        await SeedStaffAsync(first.ClinicId, first.UserId, 1);

        var firstResponse = await _client.GetAsync(STAFF_LIMIT_URL);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<StaffLimitPayload>();
        firstResult!.Used.Should().Be(1);

        _client.DefaultRequestHeaders.Authorization = null;

        var second = await AuthenticateAsync();
        await SeedStaffAsync(second.ClinicId, second.UserId, 3);

        var secondResponse = await _client.GetAsync(STAFF_LIMIT_URL);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<StaffLimitPayload>();
        secondResult!.Used.Should().Be(3);
        secondResult.Limit.Should().Be(PROFESSIONAL_STAFF_LIMIT);
    }
}

public class StaffLimitPayload
{
    public int Used { get; set; }
    public int? Limit { get; set; }
}