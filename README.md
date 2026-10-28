[🇧🇷 Português](README.pt-br.md) | [🇺🇸 English](README.md) | [🧭 Testing Guide](GUIDE.md)

# PetClinix

**🔗 Live Demo:** [https://petclinix.onrender.com/swagger/index.html](https://petclinix.onrender.com/swagger/index.html)
_(Note: The free tier on Render may take ~50 seconds to wake up if inactive)_

PetClinix is a multi-tenant SaaS MVP for veterinary clinics, designed and implemented as a public portfolio project to demonstrate senior-level software engineering practices, product thinking, and architectural decision-making.

This repository intentionally represents the **public MVP stage** of the product.  
Its purpose is to showcase the foundation of the platform, including domain modeling, modular architecture, tenancy boundaries, and the first core business capabilities.

The full product evolution, including advanced features, production hardening, and commercial-grade improvements, is intended to continue in a **private repository** after the MVP milestone.

---

## Purpose

This project was created to demonstrate the design and implementation of a real-world SaaS product with a strong focus on:

- Clean Architecture
- Domain-Driven Design (DDD)
- modular design
- multi-tenant thinking
- maintainability
- scalability
- clear separation of concerns
- domain-first software design

Rather than building a generic CRUD application, the goal is to model a realistic business domain and build a solid architectural foundation that can support future growth.

---

## Product Vision

PetClinix aims to provide veterinary clinics with a centralized platform to manage their daily operations, staff, pets, tutors, scheduling, subscriptions, and clinical workflows.

The platform is being designed as a SaaS product where each clinic operates within its own isolated data scope.

At the MVP stage, the product focuses on validating the core operational foundation of the platform.

---

## Public MVP Scope

The public MVP is intended to cover the core foundations and essential workflows of the platform, including:

- clinic onboarding and registration
- subscription signup and payment flow
- authentication
- employee management
- pet registration
- tutor registration
- schedule management
- services management

This scope is intentionally broad enough to represent a realistic vertical slice of the product, while still being limited to an MVP that is suitable for public demonstration.

---

## Repository Strategy

This repository is the **public MVP showcase**.

Its purpose is to demonstrate:

- architectural foundations
- domain design
- project structure
- coding standards
- technical direction
- implementation style

After this MVP reaches a representative and demonstrative state, the project will continue evolving in a **private repository**, where the product can be further refined with:

- additional modules
- production-grade hardening
- deeper business rules
- improved UX and operational concerns
- infrastructure evolution
- commercial and security enhancements

This public version should therefore be seen as a **portfolio-grade architectural milestone**, not the final commercial form of the product.

---

## Architecture

PetClinix is being built as a **modular monolith** with clear dependency boundaries and business-oriented module separation.

The solution follows the principles of:

- Clean Architecture
- Domain-Driven Design
- modular organization by business context

### Current project structure

```text
├── PetClinix.Api/                                  # Presentation layer (Controllers, Middleware, DI)
├── PetClinix.BuildingBlocks.Application/           # Base contracts (CQRS interfaces, Result pattern)
├── PetClinix.BuildingBlocks.Domain/                # Base domain contracts (Entity, AggregateRoot, DomainEvents)
├── PetClinix.BuildingBlocks.Infrastructure/        # Base infrastructure contracts
├── PetClinix.Modules.Appointments.Application/     # Appointments use cases (Scheduling, Slots)
├── PetClinix.Modules.Appointments.Domain/          # Appointments business logic (Appointment entity)
├── PetClinix.Modules.Appointments.Infrastructure/  # Appointments persistence
├── PetClinix.Modules.Billing.Application/          # Billing use cases (Stripe Checkout)
├── PetClinix.Modules.Billing.Domain/               # Billing business logic (Subscription entity)
├── PetClinix.Modules.Billing.Infrastructure/       # Billing persistence & Stripe integration
├── PetClinix.Modules.Catalog.Application/          # Catalog use cases (Services)
├── PetClinix.Modules.Catalog.Domain/               # Catalog business logic (Service entity)
├── PetClinix.Modules.Catalog.Infrastructure/       # Catalog persistence
├── PetClinix.Modules.Identity.Application/         # Identity use cases (Auth, Users)
├── PetClinix.Modules.Identity.Domain/              # Identity business logic (Entities, Value Objects)
├── PetClinix.Modules.Identity.Infrastructure/      # Identity persistence (EF Core, Repositories)
├── PetClinix.Modules.Pets.Application/             # Pets use cases (Tutors, Pets)
├── PetClinix.Modules.Pets.Domain/                  # Pets business logic
└── PetClinix.Modules.Pets.Infrastructure/          # Pets persistence
tests/
├── PetClinix.UnitTests/                            # Fast, isolated tests using NSubstitute and FluentAssertions
└── PetClinix.IntegrationTests/                     # E2E API tests using WebApplicationFactory and Testcontainers
```

---

## Tech Stack

- **Framework:** .NET 10
- **Architecture:** Modular Monolith, Clean Architecture, DDD
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core 9
- **Validation:** FluentValidation
- **Testing:** xUnit, NSubstitute, FluentAssertions, Testcontainers
- **API Documentation:** Swagger / OpenAPI
- **Payments:** Stripe API (Checkout & Webhooks)
- **Security:** JWT Authentication, Refresh Tokens, Role-Based Access Control (RBAC), Rate Limiting, CORS
- **Emails:** Resend API (Welcome & Password Reset flows)
- **Infrastructure:** Docker, Docker Compose

---

## Prerequisites

Before you begin, ensure you have the following installed on your machine:

1. **.NET 10 SDK**
    - Required to build and run the application.
    - Download: [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

2. **Docker & Docker Compose**
    - Used to run the PostgreSQL database locally for development.
    - The project includes a `docker-compose.yml` file. Create a `.env` file in the root (copy from `.env.example`) with your local DB credentials, then run:
        ```bash
        docker-compose up -d
        ```
    - **Strictly required to run the Integration Tests.** The integration tests use `Testcontainers` to spin up a real, ephemeral PostgreSQL database inside a Docker container automatically.
    - Download: [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/)

3. **Entity Framework Core Tools**
    - Required to create and apply database migrations manually.
    - Install globally by running:
        ```bash
        dotnet tool install --global dotnet-ef
        ```

4. **Stripe CLI**
    - Required to test the subscription payment flow and webhooks locally.
    - Installation guide: [https://stripe.com/docs/stripe-cli](https://stripe.com/docs/stripe-cli)

5. **Resend Account**
    - Required to send real welcome and password reset emails.
    - Sign up for free at [https://resend.com](https://resend.com) and get your API key.

---

## Getting Started

Follow these steps to set up and run the project locally.

### 1. Clone the repository

```bash
git clone https://github.com/your-username/PetClinix.git
cd PetClinix
```

### 2. Configure the Database Connection

The application uses a layered configuration approach. The base template is in `appsettings.json`, but your local credentials should be placed in `appsettings.Development.json` (which is ignored by Git).

Create the file `PetClinix.Api/appsettings.Development.json` and add your connection string. Ensure the credentials match your `.env` file used by Docker Compose:

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Host=localhost;Port=5432;Database=petclinix_db;Username=petclinix_dev;Password=YourPassowrd@123"
    },
    "Stripe": {
        "SecretKey": "sk_test_YOUR_STRIPE_SECRET_KEY",
        "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET"
    },
    "JwtSettings": {
        "SecretKey": "SuperSecretKeyChangeThisInProductionAtLeast32CharactersLong",
        "Issuer": "PetClinix",
        "Audience": "PetClinixUsers",
        "ExpiryMinutes": 60
    },
    "Resend": {
        "ApiKey": "re_YOUR_RESEND_API_KEY",
        "DevOverrideEmail": "YOUR_RESEND_EMAI"
    },
    "Cors": {
        "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
    },
    "App": {
        "FrontendBaseUrl": "http://localhost:5173"
    },
    "Mailjet": {
        "ApiKey": "YOUR_API_KEY",
        "SecretKey": "YOUR_SECRET_KEY",
        "SenderEmail": "seu.remetente@gmail.com",
        "SenderName": "PetClinix"
    }
}
```

### 3. Run the Application

```bash
dotnet run --project PetClinix.Api
```

_(Note: The API is configured to automatically apply database migrations on startup, creating all necessary tables. Manual migration commands are not required for local development)._

Once running, open your browser and navigate to the Swagger UI to test the API endpoints:

- **Swagger UI:** `http://localhost:<port>/swagger` (check your terminal output for the exact port, usually `5180` or `5000`).

### 4. Setup Stripe Webhooks (Local Development)

To test the payment flow locally, you need to forward Stripe webhook events to your local API.

1. Log in to your Stripe account via CLI:
    ```bash
    stripe login
    ```
2. Start listening for webhooks and forward them to your local API endpoint (adjust the port if necessary):
    ```bash
    stripe listen --forward-to http://localhost:5180/api/webhooks/stripe
    ```
3. The CLI will output a webhook signing secret (e.g., `whsec_...`). Copy this secret and paste it into your `appsettings.Development.json` under `Stripe:WebhookSecret`.
4. To simulate a successful payment event in another terminal, run:
    ```bash
    stripe trigger checkout.session.completed
    ```

---

## Testing

The project contains a comprehensive test suite divided into Unit Tests and Integration Tests.

### Run All Tests

To run the entire test suite. **Note: Docker must be running** for the integration tests to execute successfully:

```bash
dotnet test
```

### Run Unit Tests Only

Unit tests are fast and do not require external dependencies like databases or Docker.

```bash
dotnet test tests/PetClinix.UnitTests
```

### Run Integration Tests Only

Integration tests validate the API from the HTTP request down to the PostgreSQL database. **They require Docker to be running**.

```bash
dotnet test tests/PetClinix.IntegrationTests
```

---

## Roadmap

- [x] Clinic Onboarding & Registration (Multi-tenant foundation)
- [x] Domain validation & Value Objects
- [x] PostgreSQL integration with EF Core
- [x] Stripe subscription checkout flow
- [x] Webhook handling for payment success
- [x] Identity module: Password definition, Login & Refresh Token (JWT)
- [x] API Hardening: Rate Limiting, CORS & Global Exception Handling
- [x] Atomic Database Operations (Unit of Work pattern)
- [x] Comprehensive Testing Suite (Unit tests with NSubstitute & E2E Integration tests with Testcontainers)
- [x] Protected Routes & Role-based Authorization (RBAC)
- [x] Stripe Billing Portal & Subscription Cancellation Handling
- [x] Email Integration (Resend) for Welcome & Password Reset
- [x] Employee management module (CRUD, Plan Limits, Soft Delete)
- [x] Pet & Tutor registration module (CRUD, Multi-tenant isolation, Audit tracking)
- [x] Services & Catalog module (CRUD, Multi-tenant isolation, Audit tracking)
- [x] Scheduling module (Appointments, Double Booking prevention, Available Slots algorithm, Lifecycle status management)
- [x] Cross-module Referential Integrity (Safe Deletion checks)

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
