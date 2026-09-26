using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PetClinix.Api.Middlewares;
using PetClinix.Api.Services;
using PetClinix.BuildingBlocks.Application;
using PetClinix.Modules.Billing.Application.Contracts;
using PetClinix.Modules.Billing.Application.UseCases.ActivateSubscription;
using PetClinix.Modules.Billing.Application.UseCases.CreateCheckoutSession;
using PetClinix.Modules.Billing.Application.UseCases.CreatePortalSession;
using PetClinix.Modules.Billing.Application.UseCases.CancelSubscription;
using PetClinix.Modules.Billing.Domain.Repositories;
using PetClinix.Modules.Billing.Infrastructure.Persistence;
using PetClinix.Modules.Billing.Infrastructure.Repositories;
using PetClinix.Modules.Billing.Infrastructure.Services;
using PetClinix.Modules.Identity.Application.Contracts;
using PetClinix.Modules.Identity.Application.UseCases.Login;
using PetClinix.Modules.Identity.Application.UseCases.RegisterClinicWithAdmin;
using PetClinix.Modules.Identity.Application.UseCases.SetPassword;
using PetClinix.Modules.Identity.Application.UseCases.RefreshToken;
using PetClinix.Modules.Identity.Application.UseCases.GetProfile;
using PetClinix.Modules.Identity.Application.UseCases.UpdateProfile;
using PetClinix.Modules.Identity.Application.UseCases.UpdateClinic;
using PetClinix.Modules.Identity.Application.UseCases.UpdateAccount;
using PetClinix.Modules.Identity.Application.UseCases.RegisterStaff;
using PetClinix.Modules.Identity.Application.UseCases.GetStaff;
using PetClinix.Modules.Identity.Application.UseCases.DeactivateStaff;
using PetClinix.Modules.Identity.Application.UseCases.UpdateStaff;
using PetClinix.Modules.Identity.Domain.Repositories;
using PetClinix.Modules.Identity.Infrastructure.Persistence;
using PetClinix.Modules.Identity.Infrastructure.Repositories;
using PetClinix.Modules.Identity.Infrastructure.Services;
using PetClinix.Modules.Pets.Infrastructure.Persistence;
using PetClinix.Modules.Pets.Infrastructure.Repositories;
using PetClinix.Modules.Pets.Application.UseCases.RegisterTutor;
using PetClinix.Modules.Pets.Application.UseCases.GetTutors;
using PetClinix.Modules.Pets.Application.UseCases.UpdateTutor;
using PetClinix.Modules.Pets.Application.UseCases.DeactivateTutor;
using PetClinix.Modules.Pets.Application.UseCases.RegisterPet;
using PetClinix.Modules.Pets.Application.UseCases.GetPets;
using PetClinix.Modules.Pets.Application.UseCases.UpdatePet;
using PetClinix.Modules.Pets.Application.UseCases.DeactivatePet;
using PetClinix.Modules.Pets.Application.Contracts;
using PetClinix.Modules.Pets.Domain.Repositories;
using PetClinix.Modules.Catalog.Application.Contracts;
using PetClinix.Modules.Catalog.Application.UseCases.RegisterService;
using PetClinix.Modules.Catalog.Application.UseCases.GetServices;
using PetClinix.Modules.Catalog.Application.UseCases.UpdateService;
using PetClinix.Modules.Catalog.Application.UseCases.DeactivateService;
using PetClinix.Modules.Catalog.Domain.Repositories;
using PetClinix.Modules.Catalog.Infrastructure.Persistence;
using PetClinix.Modules.Catalog.Infrastructure.Repositories;
using PetClinix.Modules.Appointments.Application.Contracts;
using PetClinix.Modules.Appointments.Application.UseCases.RegisterAppointment;
using PetClinix.Modules.Appointments.Application.UseCases.GetAvailableSlots;
using PetClinix.Modules.Appointments.Application.UseCases.GetAppointments;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointment;
using PetClinix.Modules.Appointments.Application.UseCases.UpdateAppointmentStatus;
using PetClinix.Modules.Appointments.Domain.Repositories;
using PetClinix.Modules.Appointments.Infrastructure.Persistence;
using PetClinix.Modules.Appointments.Infrastructure.Repositories;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Resend;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<PetsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<AppointmentsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IClinicRepository, ClinicRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IUnitOfWork, PetClinix.Modules.Identity.Infrastructure.Persistence.UnitOfWork>();
builder.Services.AddScoped<ICommandHandler<RegisterClinicWithAdminCommand, Result<RegisterClinicWithAdminResponse>>, RegisterClinicWithAdminCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SetPasswordCommand, Result>, SetPasswordCommandHandler>();
builder.Services.AddScoped<ICommandHandler<LoginCommand, Result<LoginResponse>>, LoginCommandHandler>();
builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ICommandHandler<CreateCheckoutSessionCommand, Result<CreateCheckoutSessionResponse>>, CreateCheckoutSessionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ActivateSubscriptionCommand, Result>, ActivateSubscriptionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>, RefreshTokenCommandHandler>();
builder.Services.AddScoped<ICommandHandler<GetProfileQuery, Result<ProfileResponse>>, GetProfileQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateUserCommand, Result>, UpdateUserCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateClinicCommand, Result>, UpdateClinicCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateAccountCommand, Result>, UpdateAccountCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CreatePortalSessionCommand, Result<CreatePortalSessionResponse>>, CreatePortalSessionCommandHandler>();
builder.Services.AddScoped<ICommandHandler<CancelSubscriptionCommand, Result>, CancelSubscriptionCommandHandler>();
builder.Services.AddScoped<ISubscriptionStatusService, SubscriptionStatusService>();
builder.Services.AddScoped<ICommandHandler<RegisterStaffCommand, Result<RegisterStaffResponse>>, RegisterStaffCommandHandler>();
builder.Services.AddScoped<ICommandHandler<GetStaffQuery, Result<PagedResult<StaffResponse>>>, GetStaffQueryHandler>();
builder.Services.AddScoped<ICommandHandler<GetStaffByIdQuery, Result<StaffResponse>>, GetStaffByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateStaffCommand, Result>, UpdateStaffCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeactivateStaffCommand, Result>, DeactivateStaffCommandHandler>();
builder.Services.AddScoped<ITutorRepository, TutorRepository>();
builder.Services.AddScoped<ICommandHandler<RegisterTutorCommand, Result>, RegisterTutorCommandHandler>();
builder.Services.AddScoped<IPetsUnitOfWork, PetClinix.Modules.Pets.Infrastructure.Persistence.UnitOfWork>();
builder.Services.AddScoped<ICommandHandler<GetTutorsQuery, Result<PagedResult<TutorResponse>>>, GetTutorsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<GetTutorByIdQuery, Result<TutorResponse>>, GetTutorByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateTutorCommand, Result>, UpdateTutorCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeactivateTutorCommand, Result>, DeactivateTutorCommandHandler>();
builder.Services.AddScoped<IPetRepository, PetRepository>();
builder.Services.AddScoped<ICommandHandler<RegisterPetCommand, Result>, RegisterPetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<GetPetsQuery, Result<PagedResult<PetResponse>>>, GetPetsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<GetPetByIdQuery, Result<PetResponse>>, GetPetByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdatePetCommand, Result>, UpdatePetCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeactivatePetCommand, Result>, DeactivatePetCommandHandler>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<ICatalogUnitOfWork, PetClinix.Modules.Catalog.Infrastructure.Persistence.UnitOfWork>();
builder.Services.AddScoped<ICommandHandler<RegisterServiceCommand, Result>, RegisterServiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<GetServicesQuery, Result<PagedResult<ServiceResponse>>>, GetServicesQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateServiceCommand, Result>, UpdateServiceCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeactivateServiceCommand, Result>, DeactivateServiceCommandHandler>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentsUnitOfWork, PetClinix.Modules.Appointments.Infrastructure.Persistence.UnitOfWork>();
builder.Services.AddScoped<ICommandHandler<RegisterAppointmentCommand, Result>, RegisterAppointmentCommandHandler>();
builder.Services.AddScoped<ICommandHandler<GetAvailableSlotsQuery, Result<List<string>>>, GetAvailableSlotsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<GetAppointmentsQuery, Result<PagedResult<AppointmentResponse>>>, GetAppointmentsQueryHandler>();
builder.Services.AddScoped<ICommandHandler<GetAppointmentByIdQuery, Result<AppointmentResponse>>, GetAppointmentByIdQueryHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateAppointmentCommand, Result>, UpdateAppointmentCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateAppointmentStatusCommand, Result>, UpdateAppointmentStatusCommandHandler>();
builder.Services.AddScoped<IPetDependencyChecker, PetClinix.Modules.Pets.Infrastructure.Services.PetDependencyChecker>();
builder.Services.AddScoped<IAppointmentDependencyChecker, AppointmentDependencyChecker>();
builder.Services.AddScoped<IBillingUnitOfWork, BillingUnitOfWork>();
builder.Services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

builder.Services.AddSingleton<IClinicScheduleService>(new MockClinicScheduleService());

builder.Services.AddScoped<IServiceCatalogService>(sp =>
{
    var catalogDb = sp.GetRequiredService<CatalogDbContext>();
    return new CatalogServiceAdapter(catalogDb);
});

builder.Services.Configure<ResendClientOptions>(opt =>
{
    opt.ApiToken = builder.Configuration["Resend:ApiKey"]
        ?? throw new InvalidOperationException("Resend ApiKey não configurada.");
});
builder.Services.AddHttpClient<ResendClient>();
builder.Services.AddScoped<IEmailService, ResendEmailService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey não configurada.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("VueFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 100 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("VueFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var extensionCommand = new NpgsqlCommand(
            "CREATE EXTENSION IF NOT EXISTS unaccent;", connection);
        await extensionCommand.ExecuteNonQueryAsync();

        var identityDb = services.GetRequiredService<IdentityDbContext>();
        await identityDb.Database.MigrateAsync();

        var billingDb = services.GetRequiredService<BillingDbContext>();
        await billingDb.Database.MigrateAsync();

        var petsDb = services.GetRequiredService<PetsDbContext>();
        await petsDb.Database.MigrateAsync();

        var catalogDb = services.GetRequiredService<CatalogDbContext>();
        await catalogDb.Database.MigrateAsync();

        var apptDb = services.GetRequiredService<AppointmentsDbContext>();
        await apptDb.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao rodar as migrations no banco de dados.");
    }
}

app.Run();

public class MockClinicScheduleService : IClinicScheduleService
{
    public (TimeOnly Start, TimeOnly End) GetWorkingHours() => (new TimeOnly(8, 0), new TimeOnly(18, 0));
}

public class CatalogServiceAdapter : IServiceCatalogService
{
    private readonly CatalogDbContext _context;
    public CatalogServiceAdapter(CatalogDbContext context) => _context = context;

    public async Task<int> GetDurationInMinutesAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await _context.Services.FindAsync([serviceId], cancellationToken);
        return service?.DurationInMinutes ?? 0;
    }
}

public partial class Program { }