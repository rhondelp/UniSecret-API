# UniSecret API

**Universities' Freedom Confession Board — Backend API**

UniSecret is the REST API that powers an anonymous, university-scoped "confession board" — students verify with their university email, then post anonymous or attributed confessions that go through a moderation queue before appearing publicly.

The API is built with **ASP.NET Core (.NET 10)**, **PostgreSQL**, **Entity Framework Core**, **Redis**, **JWT authentication**, **health checks**, and **ASP.NET Core rate limiting**.

> Companion repo: [UniSecret-MobileApp](https://github.com/rhondelp/UniSecret-MobileApp) — the Expo/React Native client that consumes this API.

---

## Tech Stack & Packages

| Package / Technology | Version | Purpose |
|---|---:|---|
| `Microsoft.NET.Sdk.Web` | .NET 10 / `net10.0` | Web application framework/runtime |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL provider for EF Core |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.10 | EF Core CLI tooling, migrations, scaffolding |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.11 | JWT bearer authentication |
| `BCrypt.Net-Next` | 4.2.0 | Password hashing with BCrypt |
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | OpenAPI support |
| `Microsoft.OpenApi` | 2.7.5 | OpenAPI object model |
| `Swashbuckle.AspNetCore` | 10.2.3 | Swagger / Swagger UI |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.11 | Redis distributed caching |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | 10.0.11 | EF Core database health checks |

### Built-in ASP.NET Core features

The API also uses framework-provided functionality for:

- JWT authentication
- Authorization
- ASP.NET Core rate limiting
- Fixed-window rate limiting
- Partitioned rate limiting
- Health checks
- Dependency injection
- Middleware pipeline
- Controller-based APIs

> **Note:** Rate limiting is implemented using the ASP.NET Core framework APIs. The project does **not** require the separate `Microsoft.AspNetCore.RateLimiting` NuGet package.

Project settings:

- Target framework: `net10.0`
- Nullable reference types: enabled
- Implicit usings: enabled

---

# Architecture

The project follows a lightweight **Controller → Service → DbContext** structure rather than a strict Clean Architecture / Onion Architecture implementation.

```text
                         ┌──────────────────────┐
                         │  Expo / React Native  │
                         │      Mobile App       │
                         └──────────┬───────────┘
                                    │
                                  HTTPS
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │        NGINX          │
                         │ Reverse Proxy / LB    │
                         └──────────┬───────────┘
                                    │
                     ┌──────────────┼──────────────┐
                     │              │              │
                     ▼              ▼              ▼
                ┌─────────┐    ┌─────────┐    ┌─────────┐
                │ API #1  │    │ API #2  │    │ API #N  │
                │ ASP.NET │    │ ASP.NET │    │ ASP.NET │
                │ Core    │    │ Core    │    │ Core    │
                └────┬────┘    └────┬────┘    └────┬────┘
                     │              │              │
                     └──────────────┼──────────────┘
                                    │
                         ┌──────────┴──────────┐
                         │                     │
                         ▼                     ▼
                  ┌──────────────┐      ┌──────────────┐
                  │    Redis     │      │  PostgreSQL  │
                  │ Distributed  │      │   Database   │
                  │    Cache     │      │              │
                  └──────────────┘      └──────────────┘
```

The API is designed to remain **stateless** at the application layer so that multiple ASP.NET Core instances can operate behind NGINX.

---

## High-Concurrency Features

The API includes several features intended to improve reliability and scalability when many users access the system simultaneously.

### 1. Stateless API

The API does not maintain user session state in individual server instances.

Authentication is performed using JWT bearer tokens.

```text
Client
  │
  │ JWT
  ▼
API Instance
  │
  └── validates token
```

Because authentication state is contained in the JWT, requests can be handled by different API instances behind NGINX.

---

### 2. Asynchronous ASP.NET Core / EF Core Operations

Controller and service operations use asynchronous APIs where database or network I/O is involved.

This allows ASP.NET Core threads to be released while waiting for external I/O operations such as:

- PostgreSQL queries
- Redis operations
- Other network operations

This is important for high-concurrency workloads because threads are not unnecessarily blocked while waiting for I/O.

---

### 3. EF Core / PostgreSQL Connection Pooling

EF Core uses the PostgreSQL provider through Npgsql.

Database connections are obtained from the underlying connection pool when required and returned when the operation is complete.

The application does not keep database connections open across requests.

Transient PostgreSQL failures are also handled using:

```csharp
EnableRetryOnFailure(
    maxRetryCount: 3,
    maxRetryDelay: TimeSpan.FromSeconds(5),
    errorCodesToAdd: null);
```

The retry configuration is intentionally modest so that transient failures do not create excessive request latency.

---

### 4. In-Memory Cache

The API registers ASP.NET Core's memory cache:

```csharp
builder.Services.AddMemoryCache();
```

Memory caching is extremely fast because cached data is stored inside the current API process.

However, memory cache is **instance-local**.

For example:

```text
NGINX
 │
 ├── API #1 → Memory Cache #1
 │
 ├── API #2 → Memory Cache #2
 │
 └── API #3 → Memory Cache #3
```

Therefore, MemoryCache should not be treated as the shared source of truth when multiple API instances are running.

---

### 5. Redis Distributed Cache

Redis is registered as the distributed cache when a Redis connection string is configured.

```text
API #1 ──┐
API #2 ──┼──► Redis
API #3 ──┘
```

This allows multiple API instances to share cached data.

Example configuration:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

Redis is especially useful for data that can safely be cached and shared across instances.

---

### 6. Database Health Check

The API exposes:

```text
GET /health
```

The health check uses:

```csharp
.AddDbContextCheck<AppDbContext>();
```

This verifies the application's ability to communicate with the configured database.

The endpoint can later be used by:

- NGINX
- Docker
- Kubernetes
- Load balancers
- Monitoring systems

A load-balancing environment can use this endpoint to determine whether an API instance is healthy.

---

### 7. Rate Limiting

The API uses ASP.NET Core's built-in rate-limiting infrastructure.

Rate limiting protects the application from excessive request volume and helps prevent one client from consuming disproportionate server resources.

The current implementation uses **partitioned fixed-window rate limiting based on client IP**.

#### Authentication endpoints

Authentication endpoints receive a stricter limit:

```text
10 requests / minute / client IP
```

This is intentionally stricter because authentication can involve BCrypt password verification, which is CPU-intensive.

The authentication path is identified by:

```text
/api/v1/auth/*
```

#### General API endpoints

Other API endpoints currently use:

```text
120 requests / minute / client IP
```

#### Rate limit response

When a client exceeds the configured limit, the API returns:

```http
429 Too Many Requests
```

#### Excluded endpoints

The following endpoints are excluded from the global limiter:

```text
/health
/swagger
```

Swagger is intended primarily for development, while the health endpoint needs to remain available to infrastructure monitoring and load-balancing systems.

---

## Rate Limiting Architecture

```text
                       Incoming Request
                              │
                              ▼
                    ┌──────────────────┐
                    │ ASP.NET Core     │
                    │ Rate Limiter     │
                    └────────┬─────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
                ▼                         ▼
        /api/v1/auth/*             Other API routes
                │                         │
                ▼                         ▼
         10 requests/min            120 requests/min
           per IP                     per IP
                │                         │
                └────────────┬────────────┘
                             │
                             ▼
                       Controller
```

The current rate limiter is **local to each API instance**.

For example, with three API instances:

```text
NGINX
 │
 ├── API #1 → 120/min
 ├── API #2 → 120/min
 └── API #3 → 120/min
```

A future distributed rate-limiting implementation could use Redis if a single global limit across all API instances becomes necessary.

---

# Project Structure

```text
UniSecret-API/
│
├── Controllers/
│   ├── AuthController.cs
│   ├── ConfessionsController.cs
│   └── UniversitiesController.cs
│
├── Services/
│   ├── AuthService.cs
│   └── CacheService.cs
│
├── Data/
│   └── AppDbContext.cs
│
├── Entities/
│   └── ... EF Core entities
│
├── Dtos/
│   └── ... Request/response DTOs
│
├── Enums/
│   └── AppEnum.cs
│
├── Migrations/
│   └── ... EF Core migrations
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── .env.example
├── .gitignore
└── UniSecret.Api.csproj
```

### Important files

| File / Folder | Purpose |
|---|---|
| `Controllers/` | HTTP API endpoints |
| `Services/` | Application/business services |
| `Data/AppDbContext.cs` | EF Core database context and model configuration |
| `Entities/` | Database/domain entities |
| `Dtos/` | API request/response models |
| `Enums/` | Application-wide enumerations |
| `Migrations/` | EF Core database migration history |
| `Program.cs` | Dependency injection, authentication, caching, health checks, rate limiting, and middleware |
| `appsettings.json` | Non-secret/default application configuration |
| `.env.example` | Example environment variable configuration |
| `.gitignore` | Files excluded from source control |

---

# Domain Model

The `AppDbContext` currently registers 13 entities, only some of which are wired up to controllers.

| Entity | Wired to a controller? | Notes |
|---|---|---|
| `University` | ✅ `UniversitiesController` | Full CRUD |
| `User` | ✅ `AuthController` | Registration/login only, no profile endpoints yet |
| `Confession` | ✅ `ConfessionsController` | List approved confessions + create |
| `Category` | ⚠️ Referenced only | No CRUD endpoints yet |
| `Hashtag` / `ConfessionHashtag` | ⚠️ Modeled, not used | Many-to-many pivot exists |
| `Comment` | ❌ Not implemented | Self-referencing `ParentId` supports threaded replies |
| `Like` | ❌ Not implemented | Polymorphic via `LikeableId` / `LikeableType` |
| `Mention` | ❌ Not implemented | |
| `SavedPost` | ❌ Not implemented | |
| `Report` | ❌ Not implemented | `ReportReason` / `ReportStatus` enums already exist |
| `Notification` | ❌ Not implemented | Stores a `DataJson` payload |
| `ModerationLog` | ❌ Not implemented | Audit trail for admin actions |

### Key modeling decisions

- Enums are stored as **strings**, not integers, for database readability.
- `Confession.UserId` is always stored, including anonymous confessions.
- Anonymity is enforced at the API/response layer.
- `ConfessionsController` replaces the author identity with `"Anonymous"` when `IsAnonymous` is true.
- This preserves accountability and moderation capabilities while keeping anonymous identities hidden from normal API responses.
- Several relationships use `DeleteBehavior.Restrict` to avoid multiple cascade paths.
- Unique indexes exist on:
  - `User.Username`
  - `User.Email`
  - `Category.Slug`
  - `Hashtag.Tag`

---

# Migrations

The project currently contains two EF Core migrations.

### 1. `20260812094313_InitialCreate`

Creates the baseline schema containing the application's 13 tables:

```text
Universities
Users
Confessions
Categories
Hashtags
ConfessionHashtags
Comments
Likes
Mentions
SavedPosts
Reports
Notifications
ModerationLogs
```

The migration also creates the required foreign keys, indexes, and enum-as-string conversions.

### 2. `20260812094536_FixUniversityTypo`

Renames the `Universities.CreateAt` column to `UpdatedAt`.

```csharp
migrationBuilder.RenameColumn(
    name: "CreateAt",
    table: "Universities",
    newName: "UpdatedAt");
```

### Applying migrations

Install the EF Core CLI tool if necessary:

```bash
dotnet tool install --global dotnet-ef
```

Apply pending migrations:

```bash
dotnet ef database update
```

Create a migration after changing the EF Core model:

```bash
dotnet ef migrations add <DescriptiveName>
```

> **Important:** The `Migrations/` folder should be committed to Git. It represents the version-controlled history of the database schema.

---

# Getting Started

## Prerequisites

Install:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 14+
- Redis (recommended when distributed caching is enabled)
- `dotnet-ef`

Install the EF Core CLI tool:

```bash
dotnet tool install --global dotnet-ef
```

---

## Clone the Repository

```bash
git clone https://github.com/rhondelp/UniSecret-API.git
cd UniSecret-API
```

---

## Restore Packages

```bash
dotnet restore
```

---

## Configuration

The application requires configuration for:

- PostgreSQL
- JWT
- Redis

Example:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=unisecret_db;Username=postgres;Password=<your-password>",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "<a long, random secret>",
    "Issuer": "UniSecretApi",
    "Audience": "UniSecretClient",
    "DurationInDays": 7
  }
}
```

### PostgreSQL

The `DefaultConnection` string configures the PostgreSQL database used by EF Core.

### Redis

The `Redis` connection string configures the distributed cache.

If Redis is not configured, the application can still start and use the registered in-memory cache.

### JWT

The `JwtSettings` section configures JWT token validation and issuing.

The JWT secret should be long, random, and kept outside source control.

---

# Secrets & Environment Configuration

**Do not commit real secrets to Git.**

The repository should use placeholder configuration for development examples.

Recommended approaches include:

### .NET User Secrets

For local development:

```bash
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"

dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

dotnet user-secrets set "JwtSettings:Secret" "<long-random-secret>"
```

### Environment Variables

Production deployments can provide configuration through environment variables or the deployment platform's secret-management system.

Example:

```text
ConnectionStrings__DefaultConnection
ConnectionStrings__Redis
JwtSettings__Secret
JwtSettings__Issuer
JwtSettings__Audience
```

---

# `.gitignore`

Generated files and local secrets should not be committed.

The repository should ignore files such as:

```gitignore
bin/
obj/
.vs/
.env
.env.*
*.log
TestResults/
```

An example environment file may be committed:

```text
.env.example
```

but it must contain **placeholders only**, never real credentials or secrets.

The following should remain version controlled:

```text
Program.cs
Controllers/
Services/
Data/
Entities/
Dtos/
Enums/
Migrations/
*.csproj
appsettings.json
.env.example
.gitignore
```

---

# Run the API

After configuring PostgreSQL:

```bash
dotnet ef database update
```

Then:

```bash
dotnet run
```

The API will listen on the URLs configured by the application/launch settings.

When running in the Development environment, Swagger UI is available at:

```text
/swagger
```

---

# Health Check

The API exposes:

```http
GET /health
```

Example:

```bash
curl https://localhost:<port>/health
```

The health check includes the EF Core database check.

A healthy API instance can therefore be represented as:

```text
NGINX / Load Balancer
          │
          │ GET /health
          ▼
     ASP.NET Core
          │
          ▼
      PostgreSQL
```

This endpoint can be used by infrastructure to determine whether an API instance should receive traffic.

---

# API Endpoints

All API routes are versioned under:

```text
/api/v1
```

| Method | Route | Auth | Rate Limit | Description |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | — | 10/min/IP | Registers a student |
| `POST` | `/api/v1/auth/login` | — | 10/min/IP | Authenticates and returns a JWT |
| `GET` | `/api/v1/universities` | — | 120/min/IP | Lists all universities |
| `GET` | `/api/v1/universities/{id}` | — | 120/min/IP | Gets one university |
| `POST` | `/api/v1/universities` | — | 120/min/IP | Creates a university |
| `PUT` | `/api/v1/universities/{id}` | — | 120/min/IP | Updates a university |
| `DELETE` | `/api/v1/universities/{id}` | — | 120/min/IP | Deletes a university |
| `GET` | `/api/v1/confessions?universityId=` | — | 120/min/IP | Lists approved confessions |
| `POST` | `/api/v1/confessions` | ✅ JWT | 120/min/IP | Submits a confession |
| `GET` | `/health` | — | Excluded | Application/database health check |

### Authentication

Authenticated endpoints use:

```http
Authorization: Bearer <JWT>
```

### Rate Limiting

When the configured request limit is exceeded:

```http
HTTP/1.1 429 Too Many Requests
```

is returned.

---

# Security

The API currently uses several security mechanisms:

### Password hashing

Passwords are hashed using:

```text
BCrypt.Net-Next
```

Passwords should never be stored as plaintext.

### JWT authentication

The API uses JWT bearer authentication for protected endpoints.

JWT validation includes:

- Signature validation
- Issuer validation
- Audience validation
- Lifetime validation

Token clock skew is configured as:

```csharp
ClockSkew = TimeSpan.Zero
```

### HTTPS

HTTPS redirection is enabled:

```csharp
app.UseHttpsRedirection();
```

JWT metadata is configured to require HTTPS outside the Development environment.

### Rate limiting

Rate limiting protects the API from excessive request volume.

Authentication endpoints receive a stricter limit because password hashing is CPU-intensive.

### Database

PostgreSQL access is handled through EF Core/Npgsql with connection pooling and limited transient-failure retries.

### Distributed cache

Redis can be used to share cache state between multiple API instances.

---

# Production Deployment Architecture

The intended scalable deployment architecture is:

```text
                         Internet
                            │
                            ▼
                    ┌───────────────┐
                    │     NGINX     │
                    │ Reverse Proxy │
                    │ Load Balancer │
                    └───────┬───────┘
                            │
              ┌─────────────┼─────────────┐
              │             │             │
              ▼             ▼             ▼
        ┌──────────┐  ┌──────────┐  ┌──────────┐
        │ API #1   │  │ API #2   │  │ API #3   │
        │ .NET 10  │  │ .NET 10  │  │ .NET 10  │
        └────┬─────┘  └────┬─────┘  └────┬─────┘
             │             │             │
             └─────────────┼─────────────┘
                           │
                ┌──────────┴──────────┐
                │                     │
                ▼                     ▼
          ┌───────────┐        ┌────────────┐
          │   Redis   │        │ PostgreSQL │
          │  Cluster/ │        │  Database  │
          │ Distributed│       │            │
          │   Cache   │        │            │
          └───────────┘        └────────────┘
```

### Scalability principles

The API is designed around:

- Stateless request processing
- Async I/O
- EF Core connection pooling
- PostgreSQL
- Redis distributed caching
- Local memory caching
- Health checks
- Rate limiting
- NGINX load balancing
- Multiple ASP.NET Core instances

---

# Current Limitations

The current implementation still has several areas that should be addressed before production deployment.

### Authorization

University management endpoints currently do not enforce administrator-only authorization.

For example:

```text
POST   /api/v1/universities
PUT    /api/v1/universities/{id}
DELETE /api/v1/universities/{id}
```

should eventually require an appropriate role or authorization policy.

### Distributed rate limiting

The current rate limiter operates at the individual API-instance level.

For example:

```text
NGINX
 │
 ├── API #1 → rate limit
 ├── API #2 → rate limit
 └── API #3 → rate limit
```

A future Redis-backed distributed rate limiter can provide a global limit across all API instances if required.

### Cache invalidation

Redis and MemoryCache are infrastructure components, but cache invalidation policies still need to be defined for each resource that is cached.

### Swagger

Swagger is currently enabled only in the Development environment.

Production API documentation should be deliberately exposed and secured if required.

### Secrets

Production secrets must be moved out of source control and managed through an appropriate secret-management mechanism.

---

# Roadmap / Future Plans

Based on the entities and enums already modeled but not yet exposed through the API:

- [ ] **Comments & threaded replies** — `Comment` supports self-referencing `ParentId` / replies.
- [ ] **Likes** — implement polymorphic `Like` functionality.
- [ ] **Hashtags** — parse and expose hashtags and trending tags.
- [ ] **Categories CRUD** — management endpoints for categories.
- [ ] **Moderation workflow** — admin approval/rejection of confessions.
- [ ] **Moderation logs** — record administrative moderation actions.
- [ ] **Reports** — allow users to report confessions/comments.
- [ ] **Notifications** — expose replies, mentions, and moderation notifications.
- [ ] **Saved posts** — bookmarking functionality.
- [ ] **Mentions** — parse `@username` mentions.
- [ ] **Role-based authorization** — protect administrative endpoints using roles/policies.
- [ ] **Scheduled confessions** — background processing for `ScheduledAt`.
- [ ] **Secrets hygiene** — remove production credentials from configuration files.
- [ ] **Seed data** — provide default universities/categories where appropriate.
- [ ] **Automated tests** — add unit, integration, and API tests.
- [ ] **Distributed rate limiting** — consider Redis-backed global rate limiting for multi-instance deployments.
- [ ] **Production observability** — structured logging, metrics, tracing, and monitoring.
- [ ] **Containerization** — Docker deployment for API, NGINX, PostgreSQL, and Redis.
- [ ] **Load testing** — benchmark API throughput, latency, database performance, cache effectiveness, and rate limits under concurrent users.

---

# Development Commands

### Restore

```bash
dotnet restore
```

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

### EF Core migrations

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### List packages

```bash
dotnet list package
```

### Clean build artifacts

```bash
dotnet clean
dotnet build
```

---

# License

No license file is currently included in this repository.

Add an appropriate license (for example, MIT) before accepting external contributions.