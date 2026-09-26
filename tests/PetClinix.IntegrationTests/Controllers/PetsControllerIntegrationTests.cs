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

public class PetsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PetsControllerIntegrationTests(CustomWebApplicationFactory factory)
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
            TradeName = $"Clinica Pet {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Pet LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Pet",
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
    public async Task RegisterPet_Should_Return_401_When_No_Token_Provided()
    {
        var petRequest = new
        {
            Name = "Rex",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 15.5,
            IsNeutered = true,
            Notes = "Nenhuma"
        };

        var response = await _client.PostAsJsonAsync($"/api/tutors/{Guid.NewGuid()}/pets", petRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterPet_Should_Return_400_When_Tutor_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var petRequest = new
        {
            Name = "Rex",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 15.5,
            IsNeutered = true,
            Notes = "Nenhuma"
        };

        var response = await _client.PostAsJsonAsync($"/api/tutors/{Guid.NewGuid()}/pets", petRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("pets.tutor.not_found");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterPet_Should_Return_204_And_Save_Pet_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor do Rex",
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

        var petRequest = new
        {
            Name = "Rex",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 15.5,
            IsNeutered = true,
            Notes = "Agressivo com outros machos"
        };

        var response = await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.TutorId == tutorId);

            savedPet.Should().NotBeNull();
            savedPet!.Name.Should().Be("Rex");
            savedPet.ClinicId.Should().Be(clinicId);
            savedPet.CreatedByUserId.Should().Be(userId);
            savedPet.IsNeutered.Should().BeTrue();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetPets_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync($"/api/tutors/{Guid.NewGuid()}/pets");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPets_Should_Return_200_And_Pet_List_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Lista Pets",
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

        var petRequest = new
        {
            Name = "Lista Pet Teste",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 10.0,
            IsNeutered = true,
            Notes = "Pet para teste de lista"
        };
        await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);

        var response = await _client.GetAsync($"/api/tutors/{tutorId}/pets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedPetResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.Items.Should().ContainSingle(p => p.Name == "Lista Pet Teste");
        result.Items.First().Species.Should().Be("Dog");
        result.Items.First().Sex.Should().Be("Male");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetPets_Should_Return_200_And_Empty_List_When_No_Pets_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Sem Pet",
            Cpf = "98765432100",
            Email = (string?)null,
            PhoneNumber = "11988887777",
            SecondaryPhoneNumber = (string?)null,
            ZipCode = "01001000",
            Street = "Rua Vazia",
            Number = "S/N",
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

        var response = await _client.GetAsync($"/api/tutors/{tutorId}/pets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedPetResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetPetById_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync($"/api/tutors/{Guid.NewGuid()}/pets/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPetById_Should_Return_404_When_Pet_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Pet 404",
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

        var response = await _client.GetAsync($"/api/tutors/{tutorId}/pets/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetPetById_Should_Return_200_And_Pet_Data_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Pet Detalhe",
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

        var petRequest = new
        {
            Name = "Pet Detalhe",
            Species = 1,
            Breed = "Labrador",
            BirthDate = new DateOnly(2019, 3, 15),
            Sex = 2,
            Weight = 20.0,
            IsNeutered = false,
            Notes = "Detalhes do pet"
        };
        await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);

        Guid petId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.TutorId == tutorId);
            petId = savedPet!.Id;
        }

        var response = await _client.GetAsync($"/api/tutors/{tutorId}/pets/{petId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PetDetailResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(petId);
        result.Name.Should().Be("Pet Detalhe");
        result.Species.Should().Be("Dog");
        result.Sex.Should().Be("Female");
        result.IsNeutered.Should().BeFalse();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdatePet_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            Name = "Rex Atualizado",
            Species = 1,
            Breed = "Labrador",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 16.0,
            IsNeutered = true,
            Notes = "Atualizado"
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{Guid.NewGuid()}/pets/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePet_Should_Return_NotFound_When_Pet_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Update Pet 404",
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

        var updateRequest = new
        {
            Name = "Rex Atualizado",
            Species = 1,
            Breed = "Labrador",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 16.0,
            IsNeutered = true,
            Notes = "Atualizado"
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{tutorId}/pets/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdatePet_Should_Return_204_And_Update_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Update Pet Sucesso",
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
        Guid petId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedTutor = await petsDb.Tutors.FirstOrDefaultAsync(t => t.ClinicId == clinicId);
            tutorId = savedTutor!.Id;
        }

        var petRequest = new
        {
            Name = "Rex Original",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 10.0,
            IsNeutered = true,
            Notes = "Original"
        };
        await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.TutorId == tutorId);
            petId = savedPet!.Id;
        }

        var updateRequest = new
        {
            Name = "Rex Atualizado",
            Species = 1,
            Breed = "Labrador",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 16.5,
            IsNeutered = true,
            Notes = "Peso e raça atualizados"
        };

        var response = await _client.PutAsJsonAsync($"/api/tutors/{tutorId}/pets/{petId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var updatedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.Id == petId);

            updatedPet.Should().NotBeNull();
            updatedPet!.Name.Should().Be("Rex Atualizado");
            updatedPet.Breed.Should().Be("Labrador");
            updatedPet.Weight.Should().Be(16.5);
            updatedPet.UpdatedByUserId.Should().Be(userId);
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeactivatePet_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.DeleteAsync($"/api/tutors/{Guid.NewGuid()}/pets/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivatePet_Should_Return_NotFound_When_Pet_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Delete Pet 404",
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

        var response = await _client.DeleteAsync($"/api/tutors/{tutorId}/pets/{Guid.NewGuid()}");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task DeactivatePet_Should_Return_204_And_Set_Inactive_In_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var tutorRequest = new
        {
            Name = "Tutor Delete Pet Sucesso",
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

        var petRequest = new
        {
            Name = "Pet Delete",
            Species = 1,
            Breed = "Vira Lata",
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = 1,
            Weight = 12.0,
            IsNeutered = true,
            Notes = "Pet para deletar"
        };
        await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);

        Guid petId;
        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var savedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.TutorId == tutorId);
            petId = savedPet!.Id;
        }

        var response = await _client.DeleteAsync($"/api/tutors/{tutorId}/pets/{petId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
            var deactivatedPet = await petsDb.Pets.FirstOrDefaultAsync(p => p.Id == petId);

            deactivatedPet.Should().NotBeNull();
            deactivatedPet!.IsActive.Should().BeFalse();
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }
}

public class PagedPetResponse
{
    public List<PetItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class PetItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Sex { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public bool IsNeutered { get; set; }
}

public class PetDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string Sex { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public bool IsNeutered { get; set; }
}