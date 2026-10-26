using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Appointments.Infrastructure.Persistence;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Catalog.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using PetClinix.Modules.Pets.Infrastructure.Persistence;
using Xunit;

namespace PetClinix.IntegrationTests.Controllers;

public class AppointmentsControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentsControllerIntegrationTests(CustomWebApplicationFactory factory)
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
            TradeName = $"Clinica Appt {Guid.NewGuid().ToString().Substring(0, 8)}",
            LegalName = "Appt LTDA",
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 14),
            Email = $"clinica_{Guid.NewGuid()}@teste.com",
            PhoneNumber = "11988887777",
            ZipCode = "01001000",
            Street = "Rua Teste",
            Number = "123",
            Neighborhood = "Centro",
            City = "SP",
            State = "SP",
            AdminName = "Admin Appt",
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

    private async Task LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);
    }

    private async Task<Guid> CreateServiceAsync(Guid clinicId, int durationMinutes = 30)
    {
        var serviceRequest = new
        {
            Name = $"Servico Appt {Guid.NewGuid().ToString().Substring(0, 8)}",
            Description = "Servico para teste de agendamento",
            DurationInMinutes = durationMinutes,
            Price = 100.0m,
            RequiresVeterinarian = true
        };

        await _client.PostAsJsonAsync("/api/services", serviceRequest);

        using var scope = _factory.Services.CreateScope();
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var savedService = await catalogDb.Services
            .Where(s => s.ClinicId == clinicId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();
        return savedService!.Id;
    }

    private async Task<Guid> CreateTutorAsync(Guid clinicId)
    {
        var tutorName = $"Tutor Appt {Guid.NewGuid().ToString().Substring(0, 8)}";

        var tutorRequest = new
        {
            Name = tutorName,
            Cpf = "52998224725",
            Email = (string?)null,
            PhoneNumber = "11999990000",
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
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
        var tutor = await petsDb.Tutors
            .Where(t => t.ClinicId == clinicId && t.Name == tutorName)
            .FirstOrDefaultAsync();
        return tutor!.Id;
    }

    private async Task<Guid> CreatePetAsync(Guid tutorId)
    {
        var petName = $"Pet Appt {Guid.NewGuid().ToString().Substring(0, 8)}";

        var petRequest = new
        {
            Name = petName,
            Species = "Dog",
            Breed = (string?)null,
            BirthDate = new DateOnly(2020, 5, 10),
            Sex = "Male",
            Weight = 15.5,
            IsNeutered = true,
            Notes = (string?)null
        };

        var response = await _client.PostAsJsonAsync($"/api/tutors/{tutorId}/pets", petRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var petsDb = scope.ServiceProvider.GetRequiredService<PetsDbContext>();
        var pet = await petsDb.Pets
            .Where(p => p.TutorId == tutorId && p.Name == petName)
            .FirstOrDefaultAsync();
        return pet!.Id;
    }

    private async Task<Guid> CreateVeterinarianAsync()
    {
        var vetEmail = $"vet_{Guid.NewGuid()}@teste.com";

        var vetRequest = new
        {
            Name = "Dr. Vet Appt",
            Email = vetEmail,
            DocumentNumber = Guid.NewGuid().ToString("N").Substring(0, 11),
            PhoneNumber = "11999990000",
            BirthDate = new DateOnly(1990, 1, 1),
            Role = "Veterinarian"
        };

        var response = await _client.PostAsJsonAsync("/api/clinics/me/staff", vetRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailVo = PetClinix.Modules.Identity.Domain.ValueObjects.Email.Create(vetEmail);
        var vet = await identityDb.Users.FirstOrDefaultAsync(u => u.Email == emailVo);
        return vet!.Id;
    }

    private async Task<(Guid ClinicId, Guid ServiceId, Guid TutorId, Guid PetId, Guid VeterinarianId)> SetupAdminWithReferencesAsync()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var tutorId = await CreateTutorAsync(clinicId);
        var petId = await CreatePetAsync(tutorId);
        var veterinarianId = await CreateVeterinarianAsync();

        return (clinicId, serviceId, tutorId, petId, veterinarianId);
    }

    private Task<HttpResponseMessage> RegisterAppointmentAsync(Guid serviceId, Guid tutorId, Guid petId, Guid veterinarianId, DateOnly date, TimeOnly time, string notes) =>
        _client.PostAsJsonAsync("/api/appointments", new
        {
            TutorId = tutorId,
            PetId = petId,
            ServiceId = serviceId,
            VeterinarianId = veterinarianId,
            Date = date,
            Time = time,
            Notes = notes
        });

    private static DateOnly FutureDate(int daysAhead) => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));

    [Fact]
    public async Task RegisterAppointment_Should_Return_401_When_No_Token_Provided()
    {
        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            Date = FutureDate(1),
            Time = new TimeOnly(10, 0),
            Notes = "Sem token"
        };

        var response = await _client.PostAsJsonAsync("/api/appointments", apptRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_204_And_Save_In_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var tutorId = await CreateTutorAsync(clinicId);
        var petId = await CreatePetAsync(tutorId);
        var vetId = await CreateVeterinarianAsync();
        var apptDate = FutureDate(2);
        var apptTime = new TimeOnly(10, 0);

        var response = await RegisterAppointmentAsync(serviceId, tutorId, petId, vetId, apptDate, apptTime, "Consulta de rotina");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
        var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.VeterinarianId == vetId);

        savedAppt.Should().NotBeNull();
        savedAppt!.ClinicId.Should().Be(clinicId);
        savedAppt.ScheduledDate.Should().Be(apptDate);
        savedAppt.ScheduledTime.Should().Be(apptTime);
        savedAppt.CreatedByUserId.Should().Be(userId);
        savedAppt.Status.Should().Be(PetClinix.Modules.Appointments.Domain.Enums.AppointmentStatus.Scheduled);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_400_When_Double_Booking()
    {
        var (clinicId, serviceId, tutorId, petId, veterinarianId) = await SetupAdminWithReferencesAsync();
        var apptDate = FutureDate(3);
        var apptTime = new TimeOnly(10, 0);

        var firstResponse = await RegisterAppointmentAsync(serviceId, tutorId, petId, veterinarianId, apptDate, apptTime, "Primeiro agendamento");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondResponse = await RegisterAppointmentAsync(serviceId, tutorId, petId, veterinarianId, apptDate, apptTime, "Tentativa de conflito");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await secondResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.slot_taken");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAvailableSlots_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync($"/api/appointments/available-slots?vetId={Guid.NewGuid()}&serviceId={Guid.NewGuid()}&date={FutureDate(1):yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAvailableSlots_Should_Return_200_And_Skip_Conflicting_Slot()
    {
        var (clinicId, serviceId, tutorId, petId, veterinarianId) = await SetupAdminWithReferencesAsync();
        var testDate = FutureDate(2);

        var apptResponse = await RegisterAppointmentAsync(serviceId, tutorId, petId, veterinarianId, testDate, new TimeOnly(9, 0), "Ocupando slot das 09:00");
        apptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var query = $"/api/appointments/available-slots?vetId={veterinarianId}&serviceId={serviceId}&date={testDate:yyyy-MM-dd}";
        var response = await _client.GetAsync(query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var slots = await response.Content.ReadFromJsonAsync<List<string>>();
        slots.Should().NotBeNull();

        slots.Should().NotContain("09:00");
        slots.Should().Contain("08:00");
        slots.Should().Contain("09:30");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointments_Should_Return_401_When_No_Token_Provided()
    {
        var date = FutureDate(1);
        var response = await _client.GetAsync($"/api/appointments?startDate={date:yyyy-MM-dd}&endDate={date:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAppointments_Should_Return_200_And_List_When_Appointments_Exist()
    {
        var (clinicId, serviceId, tutorId, petId, veterinarianId) = await SetupAdminWithReferencesAsync();
        var apptDate = FutureDate(1);
        var apptTime = new TimeOnly(10, 0);

        await RegisterAppointmentAsync(serviceId, tutorId, petId, veterinarianId, apptDate, apptTime, "Agendamento de teste para listagem");

        var response = await _client.GetAsync($"/api/appointments?startDate={apptDate:yyyy-MM-dd}&endDate={apptDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<AppointmentItemResponse>>();
        result.Should().NotBeNull();
        result!.Should().ContainSingle(a => a.ScheduledDate == apptDate && a.ScheduledTime == apptTime);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointments_Should_Return_200_And_Empty_List_When_No_Appointments()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var start = FutureDate(10);
        var response = await _client.GetAsync($"/api/appointments?startDate={start:yyyy-MM-dd}&endDate={start.AddDays(5):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<AppointmentItemResponse>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointmentById_Should_Return_401_When_No_Token_Provided()
    {
        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAppointmentById_Should_Return_404_When_Appointment_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var response = await _client.GetAsync($"/api/appointments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointmentById_Should_Return_200_And_Appointment_Data_When_Valid()
    {
        var (clinicId, serviceId, tutorId, petId, veterinarianId) = await SetupAdminWithReferencesAsync();
        var apptDate = FutureDate(1);
        var apptTime = new TimeOnly(11, 0);

        await RegisterAppointmentAsync(serviceId, tutorId, petId, veterinarianId, apptDate, apptTime, "Agendamento para teste de detalhe");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.ClinicId == clinicId);
            appointmentId = savedAppt!.Id;
        }

        var response = await _client.GetAsync($"/api/appointments/{appointmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AppointmentDetailResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(appointmentId);
        result.ScheduledDate.Should().Be(apptDate);
        result.ScheduledTime.Should().Be(apptTime);
        result.Notes.Should().Be("Agendamento para teste de detalhe");
        result.Status.Should().Be("Scheduled");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointment_Should_Return_401_When_No_Token_Provided()
    {
        var updateRequest = new
        {
            VeterinarianId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            Date = FutureDate(2),
            Time = new TimeOnly(10, 0),
            Notes = "Sem token"
        };

        var response = await _client.PutAsJsonAsync($"/api/appointments/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAppointment_Should_Return_400_When_Appointment_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var updateRequest = new
        {
            VeterinarianId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            Date = FutureDate(2),
            Time = new TimeOnly(10, 0),
            Notes = "Agendamento inexistente"
        };

        var response = await _client.PutAsJsonAsync($"/api/appointments/{Guid.NewGuid()}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.not_found");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointment_Should_Return_204_And_Update_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var tutorId = await CreateTutorAsync(clinicId);
        var petId = await CreatePetAsync(tutorId);
        var vetId = await CreateVeterinarianAsync();
        var apptDate = FutureDate(1);
        var initialTime = new TimeOnly(10, 0);

        await RegisterAppointmentAsync(serviceId, tutorId, petId, vetId, apptDate, initialTime, "Notas originais");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.VeterinarianId == vetId);
            appointmentId = savedAppt!.Id;
        }

        var newTime = new TimeOnly(12, 0);
        var updateRequest = new
        {
            VeterinarianId = vetId,
            ServiceId = serviceId,
            Date = apptDate,
            Time = newTime,
            Notes = "Notas atualizadas via PUT"
        };

        var response = await _client.PutAsJsonAsync($"/api/appointments/{appointmentId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var updatedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);

            updatedAppt.Should().NotBeNull();
            updatedAppt!.Notes.Should().Be("Notas atualizadas via PUT");
            updatedAppt.ScheduledDate.Should().Be(apptDate);
            updatedAppt.ScheduledTime.Should().Be(newTime);
            updatedAppt.UpdatedByUserId.Should().Be(userId);
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointmentStatus_Should_Return_401_When_No_Token_Provided()
    {
        var statusRequest = new { NewStatus = 2 };
        var response = await _client.PatchAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/status", statusRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAppointmentStatus_Should_Return_400_When_Appointment_Does_Not_Exist()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var statusRequest = new { NewStatus = 2 };
        var response = await _client.PatchAsJsonAsync($"/api/appointments/{Guid.NewGuid()}/status", statusRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.not_found");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointmentStatus_Should_Return_204_And_Update_Db_When_Valid()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var tutorId = await CreateTutorAsync(clinicId);
        var petId = await CreatePetAsync(tutorId);
        var vetId = await CreateVeterinarianAsync();
        var apptDate = FutureDate(1);

        await RegisterAppointmentAsync(serviceId, tutorId, petId, vetId, apptDate, new TimeOnly(14, 0), "Agendamento para mudar status");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.ClinicId == clinicId);
            appointmentId = savedAppt!.Id;
        }

        var statusRequest = new { NewStatus = 4 };
        var response = await _client.PatchAsJsonAsync($"/api/appointments/{appointmentId}/status", statusRequest);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var updatedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);

            updatedAppt.Should().NotBeNull();
            updatedAppt!.Status.Should().Be(PetClinix.Modules.Appointments.Domain.Enums.AppointmentStatus.Canceled);
            updatedAppt.UpdatedByUserId.Should().Be(userId);
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointments_Should_Return_400_When_StartDate_Is_After_EndDate()
    {
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var start = FutureDate(10);
        var response = await _client.GetAsync($"/api/appointments?startDate={start:yyyy-MM-dd}&endDate={start.AddDays(-5):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.invalid_range");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_400_When_Service_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, _, _, _) = await SetupAdminWithReferencesAsync();
        var (_, serviceIdB, tutorIdB, petIdB, vetIdB) = await SetupAdminWithReferencesAsync();

        var response = await RegisterAppointmentAsync(serviceIdA, tutorIdB, petIdB, vetIdB, FutureDate(1), new TimeOnly(10, 0), "Servico de outra clinica");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.invalid_service");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_400_When_Tutor_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();
        var (_, serviceIdB, tutorIdB, petIdB, vetIdB) = await SetupAdminWithReferencesAsync();

        var response = await RegisterAppointmentAsync(serviceIdB, tutorIdA, petIdB, vetIdB, FutureDate(1), new TimeOnly(10, 0), "Tutor de outra clinica");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.invalid_tutor");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_400_When_Pet_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();
        var (_, serviceIdB, tutorIdB, petIdB, vetIdB) = await SetupAdminWithReferencesAsync();

        var response = await RegisterAppointmentAsync(serviceIdB, tutorIdB, petIdA, vetIdB, FutureDate(1), new TimeOnly(10, 0), "Pet de outra clinica");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.invalid_pet");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task RegisterAppointment_Should_Return_400_When_Veterinarian_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();
        var (_, serviceIdB, tutorIdB, petIdB, vetIdB) = await SetupAdminWithReferencesAsync();

        var response = await RegisterAppointmentAsync(serviceIdB, tutorIdB, petIdB, vetIdA, FutureDate(1), new TimeOnly(10, 0), "Veterinario de outra clinica");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.invalid_veterinarian");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointmentById_Should_Return_404_When_Appointment_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();

        await RegisterAppointmentAsync(serviceIdA, tutorIdA, petIdA, vetIdA, FutureDate(1), new TimeOnly(10, 0), "Agendamento da clinica A");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.ClinicId == clinicA);
            appointmentId = savedAppt!.Id;
        }

        var (emailB, passwordB, userIdB, clinicB) = await SetupAdminAsync();
        await LoginAsync(emailB, passwordB);

        var response = await _client.GetAsync($"/api/appointments/{appointmentId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.not_found");

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointment_Should_Return_400_When_Appointment_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();

        await RegisterAppointmentAsync(serviceIdA, tutorIdA, petIdA, vetIdA, FutureDate(1), new TimeOnly(10, 0), "Notas originais da clinica A");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.ClinicId == clinicA);
            appointmentId = savedAppt!.Id;
        }

        var (emailB, passwordB, userIdB, clinicB) = await SetupAdminAsync();
        await LoginAsync(emailB, passwordB);

        var updateRequest = new
        {
            VeterinarianId = vetIdA,
            ServiceId = serviceIdA,
            Date = FutureDate(1),
            Time = new TimeOnly(15, 0),
            Notes = "Tentativa de alteracao pela clinica B"
        };

        var response = await _client.PutAsJsonAsync($"/api/appointments/{appointmentId}", updateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.not_found");

        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var unchangedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);

            unchangedAppt.Should().NotBeNull();
            unchangedAppt!.Notes.Should().Be("Notas originais da clinica A");
            unchangedAppt.ScheduledTime.Should().Be(new TimeOnly(10, 0));
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task UpdateAppointmentStatus_Should_Return_400_When_Appointment_Belongs_To_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();

        await RegisterAppointmentAsync(serviceIdA, tutorIdA, petIdA, vetIdA, FutureDate(1), new TimeOnly(10, 0), "Status da clinica A");

        Guid appointmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var savedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.ClinicId == clinicA);
            appointmentId = savedAppt!.Id;
        }

        var (emailB, passwordB, userIdB, clinicB) = await SetupAdminAsync();
        await LoginAsync(emailB, passwordB);

        var statusRequest = new { NewStatus = 4 };
        var response = await _client.PatchAsJsonAsync($"/api/appointments/{appointmentId}/status", statusRequest);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errorContent = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        errorContent!.ErrorCode.Should().Be("appointments.appt.not_found");

        using (var scope = _factory.Services.CreateScope())
        {
            var apptDb = scope.ServiceProvider.GetRequiredService<AppointmentsDbContext>();
            var unchangedAppt = await apptDb.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);

            unchangedAppt.Should().NotBeNull();
            unchangedAppt!.Status.Should().Be(PetClinix.Modules.Appointments.Domain.Enums.AppointmentStatus.Scheduled);
        }

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAppointments_Should_Not_Return_Appointments_From_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();
        var apptDate = FutureDate(1);

        await RegisterAppointmentAsync(serviceIdA, tutorIdA, petIdA, vetIdA, apptDate, new TimeOnly(10, 0), "Agendamento da clinica A");

        var (emailB, passwordB, userIdB, clinicB) = await SetupAdminAsync();
        await LoginAsync(emailB, passwordB);

        var response = await _client.GetAsync($"/api/appointments?startDate={apptDate:yyyy-MM-dd}&endDate={apptDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<AppointmentItemResponse>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task GetAvailableSlots_Should_Not_Be_Affected_By_Appointments_From_Another_Clinic()
    {
        var (clinicA, serviceIdA, tutorIdA, petIdA, vetIdA) = await SetupAdminWithReferencesAsync();
        var testDate = FutureDate(2);

        await RegisterAppointmentAsync(serviceIdA, tutorIdA, petIdA, vetIdA, testDate, new TimeOnly(9, 0), "Ocupando slot na clinica A");

        var (_, serviceIdB, tutorIdB, petIdB, vetIdB) = await SetupAdminWithReferencesAsync();

        var query = $"/api/appointments/available-slots?vetId={vetIdA}&serviceId={serviceIdB}&date={testDate:yyyy-MM-dd}";
        var response = await _client.GetAsync(query);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var slots = await response.Content.ReadFromJsonAsync<List<string>>();
        slots.Should().NotBeNull();
        slots!.Should().Contain("09:00");

        _client.DefaultRequestHeaders.Authorization = null;
    }
}

public class PagedAppointmentResponse
{
    public List<AppointmentItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class AppointmentItemResponse
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }
    public Guid PetId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid VeterinarianId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AppointmentDetailResponse
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }
    public Guid PetId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid VeterinarianId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public TimeOnly ScheduledTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
}