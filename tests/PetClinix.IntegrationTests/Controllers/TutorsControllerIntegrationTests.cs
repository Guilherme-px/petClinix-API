using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Domain.ValueObjects;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using PetClinix.Modules.Pets.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Controllers;

public class TutorsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TutorsControllerIntegrationTests(CustomWebApplicationFactory factory)
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
            TradeName = $"Clinica Tutor {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Tutor LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Tutor",
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
            var emailVo = Email.Create(email);
            var user = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);
            userId = user!.Id;
        }

        return (email, password, userId, clinicId);
    }

    [Fact]
    public async Task RegisterTutor_Should_Return_401_When_No_Token_Provided()
    {
        var tutorRequest = new
        {
            Name = "Tutor Teste",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/tutors", tutorRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterTutor_Should_Return_204_And_Save_Tutor_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "João da Silva",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Avenida Paulista",
            Number = "1000",
            Neighborhood = "Bela Vista",
            Complement = "Apto 10",
            City = "Sao Paulo",
            State = "SP",
            Notes = "Prefere contato por WhatsApp"
        };

        var response = await _client.PostAsJsonAsync("/api/tutors", tutorRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
        var savedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.ClinicId == clinicId);

        savedTutor.Should().NotBeNull();
        savedTutor!.Name.Should().Be("João da Silva");
        savedTutor.CreatedByUserId.Should().Be(userId);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterTutor_Should_Return_400_When_Cpf_Duplicated()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Duplicado",
            Cpf = "11122233344",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/tutors", tutorRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await _client.PostAsJsonAsync("/api/tutors", tutorRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("pets.tutor.cpf_already_exists");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutors_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync("/api/tutors");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTutors_Should_Return_200_And_Tutor_List_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginRequest = new { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Lista",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };

        await _client.PostAsJsonAsync("/api/tutors", tutorRequest);

        var response = await _client.GetAsync("/api/tutors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedTutorResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutors_Should_Not_Return_Tutors_From_Other_Clinics()
    {
        var (emailA, passwordA, userIdA, clinicIdA) = await SetupAdminAsync();
        var loginResponseA = await _client.PostAsJsonAsync("/api/users/login", new { Email = emailA, Password = passwordA });
        var loginResultA = await loginResponseA.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResultA!.Token);

        var tutorA = new
        {
            Name = "Tutor Clinica A",
            Cpf = "11111111111",
            PhoneNumber = "11999990000",
            ZipCode = "01000-000",
            Street = "Rua A",
            Number = "1",
            Neighborhood = "Bairro A",
            City = "Cidade A",
            State = "SP"
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorA);
        _client.DefaultRequestHeaders.Authorization = null;

        var (emailB, passwordB, userIdB, clinicIdB) = await SetupAdminAsync();
        var loginResponseB = await _client.PostAsJsonAsync("/api/users/login", new { Email = emailB, Password = passwordB });
        var loginResultB = await loginResponseB.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResultB!.Token);

        var tutorB = new
        {
            Name = "Tutor Clinica B",
            Cpf = "22222222222",
            PhoneNumber = "11999990000",
            ZipCode = "01000-000",
            Street = "Rua B",
            Number = "2",
            Neighborhood = "Bairro B",
            City = "Cidade B",
            State = "SP"
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorB);

        var response = await _client.GetAsync("/api/tutors");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedTutorResponse>();

        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(t => t.Name == "Tutor Clinica B");
        result.Items.Should().NotContain(t => t.Name == "Tutor Clinica A");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutorById_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync($"/api/tutors/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTutorById_Should_Return_404_When_Tutor_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var response = await _client.GetAsync($"/api/tutors/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutorById_Should_Return_200_And_Tutor_Data_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Detalhe",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorRequest);

        Guid tutorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.ClinicId == clinicId);
            tutorId = savedTutor!.Id;
        }

        var response = await _client.GetAsync($"/api/tutors/{tutorId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TutorDetailResponse>();
        result.Should().NotBeNull();
        result.Should().NotBeNull();
        result!.Id.Should().Be(tutorId);
        result.Name.Should().Be("Tutor Detalhe");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateTutor_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            Name = "Nome Novo",
            Email = (string?)null,
            PhoneNumber = "11912345678",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTutor_Should_Return_NotFound_When_Tutor_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var updateRequest = new
        {
            Name = "Nome Novo",
            Email = (string?)null,
            PhoneNumber = "11912345678",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateTutor_Should_Return_204_And_Update_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Original",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Antiga",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorRequest);

        Guid tutorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.ClinicId == clinicId);
            tutorId = savedTutor!.Id;
        }

        var updateRequest = new
        {
            Name = "Tutor Atualizado",
            Email = "novo@email.com",
            PhoneNumber = "11900000000",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Nova",
            Number = "999",
            Neighborhood = "Bairro Novo",
            Complement = (string?)null,
            City = "Santos",
            State = "SP",
            Notes = "Notas atualizadas"
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{tutorId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var updatedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.Id == tutorId);

            updatedTutor.Should().NotBeNull();
            updatedTutor!.Name.Should().Be("Tutor Atualizado");
            updatedTutor.Street.Should().Be("Rua Nova");
            updatedTutor.City.Should().Be("Santos");
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeactivateTutor_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.DeleteAsync($"/api/tutors/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateTutor_Should_Return_NotFound_When_Tutor_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var response = await _client.DeleteAsync($"/api/tutors/{Guid.NewGuid()}");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeactivateTutor_Should_Return_204_And_Set_Inactive_In_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Delete",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorRequest);

        Guid tutorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.ClinicId == clinicId);
            tutorId = savedTutor!.Id;
        }

        var response = await _client.DeleteAsync($"/api/tutors/{tutorId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var deactivatedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.Id == tutorId);

            deactivatedTutor.Should().NotBeNull();
            deactivatedTutor!.IsActive.Should().BeFalse();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutors_Should_Find_Accented_Name_When_Searching_Without_Accents()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var firstTutor = new
        {
            Name = "José Administrador",
            Cpf = "11111111111",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua A",
            Number = "1",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", firstTutor);

        var secondTutor = new
        {
            Name = "Maria Auxiliar",
            Cpf = "22222222222",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua B",
            Number = "2",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", secondTutor);

        var response = await _client.GetAsync("/api/tutors?search=jose");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedTutorResponse>();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(t => t.Name == "José Administrador");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutors_Should_Return_Empty_When_Search_Has_No_Match()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Maria Auxiliar",
            Cpf = "12345678900",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", tutorRequest);

        var response = await _client.GetAsync("/api/tutors?search=cirurgia");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedTutorResponse>();
        result!.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetTutors_Should_Return_All_When_Search_Is_Empty()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var firstTutor = new
        {
            Name = "José Administrador",
            Cpf = "11111111111",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua A",
            Number = "1",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", firstTutor);

        var secondTutor = new
        {
            Name = "Maria Auxiliar",
            Cpf = "22222222222",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua B",
            Number = "2",
            Neighborhood = "Centro",
            Complement = (string?)null,
            City = "Sao Paulo",
            State = "SP",
            Notes = (string?)null
        };
        await _client.PostAsJsonAsync("/api/tutors", secondTutor);

        var response = await _client.GetAsync("/api/tutors?search=");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedTutorResponse>();
        result!.TotalCount.Should().Be(2);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}

public class PagedTutorResponse
{
    public List<TutorItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class TutorItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
}

public class TutorDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? SecondaryPhoneNumber { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}