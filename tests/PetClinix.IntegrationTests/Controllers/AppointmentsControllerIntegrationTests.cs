using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetClinix.Modules.Appointments.Infrastructure.Persistence;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Catalog.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
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
        var vetId = Guid.NewGuid();
        var apptDate = FutureDate(2);
        var apptTime = new TimeOnly(10, 0);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = vetId,
            Date = apptDate,
            Time = apptTime,
            Notes = "Consulta de rotina"
        };

        var response = await _client.PostAsJsonAsync("/api/appointments", apptRequest);
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
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var vetId = Guid.NewGuid();
        var apptDate = FutureDate(3);
        var apptTime = new TimeOnly(10, 0);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = vetId,
            Date = apptDate,
            Time = apptTime,
            Notes = "Primeiro agendamento"
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/appointments", apptRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = vetId,
            Date = apptDate,
            Time = apptTime,
            Notes = "Tentativa de conflito"
        };

        var secondResponse = await _client.PostAsJsonAsync("/api/appointments", secondRequest);
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
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId, 30);

        var vetId = Guid.NewGuid();
        var testDate = FutureDate(2);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = vetId,
            Date = testDate,
            Time = new TimeOnly(9, 0),
            Notes = "Ocupando slot das 09:00"
        };
        var apptResponse = await _client.PostAsJsonAsync("/api/appointments", apptRequest);
        apptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var query = $"/api/appointments/available-slots?vetId={vetId}&serviceId={serviceId}&date={testDate:yyyy-MM-dd}";
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
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var apptDate = FutureDate(1);
        var apptTime = new TimeOnly(10, 0);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = Guid.NewGuid(),
            Date = apptDate,
            Time = apptTime,
            Notes = "Agendamento de teste para listagem"
        };
        await _client.PostAsJsonAsync("/api/appointments", apptRequest);

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
        var (email, password, userId, clinicId) = await SetupAdminAsync();
        await LoginAsync(email, password);

        var serviceId = await CreateServiceAsync(clinicId);
        var apptDate = FutureDate(1);
        var apptTime = new TimeOnly(11, 0);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = Guid.NewGuid(),
            Date = apptDate,
            Time = apptTime,
            Notes = "Agendamento para teste de detalhe"
        };
        await _client.PostAsJsonAsync("/api/appointments", apptRequest);

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
        var vetId = Guid.NewGuid();
        var apptDate = FutureDate(1);
        var initialTime = new TimeOnly(10, 0);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = vetId,
            Date = apptDate,
            Time = initialTime,
            Notes = "Notas originais"
        };
        await _client.PostAsJsonAsync("/api/appointments", apptRequest);

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
        var apptDate = FutureDate(1);

        var apptRequest = new
        {
            TutorId = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            ServiceId = serviceId,
            VeterinarianId = Guid.NewGuid(),
            Date = apptDate,
            Time = new TimeOnly(14, 0),
            Notes = "Agendamento para mudar status"
        };
        await _client.PostAsJsonAsync("/api/appointments", apptRequest);

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