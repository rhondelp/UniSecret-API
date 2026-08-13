# UniSecret API

**Universities' Freedom Confession Board — Backend API**

UniSecret is the REST API that powers an anonymous, university-scoped "confession board" — students verify with their university email, then post anonymous or attributed confessions that go through a moderation queue before appearing publicly. The API is built with **ASP.NET Core (.NET 10)** and **PostgreSQL** via **Entity Framework Core**.

> Companion repo: [UniSecret-MobileApp](https://github.com/rhondelp/UniSecret-MobileApp) — the Expo/React Native client that consumes this API.

---

## Tech Stack & Packages

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.NET.Sdk.Web` (target: `net10.0`) | .NET 10 | Web application framework/runtime |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | PostgreSQL provider for EF Core |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.10 | EF Core CLI tooling (migrations, scaffolding) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.11 | JWT bearer authentication middleware |
| `BCrypt.Net-Next` | 4.2.0 | Password hashing (`bcrypt`) |
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | Native OpenAPI document generation |
| `Microsoft.OpenApi` | 2.7.5 | OpenAPI object model, used alongside Swashbuckle |
| `Swashbuckle.AspNetCore` | 10.2.3 | Swagger / Swagger UI generation for the API |

Project settings: `Nullable` and `ImplicitUsings` are both enabled.

---

## Architecture

The project follows a light, controller-service-DbContext structure rather than a strict Clean/Onion architecture:

```
UniSecret-API/
├── Controllers/         # HTTP endpoints (thin, delegate to services or DbContext)
│   ├── AuthController.cs
│   ├── ConfessionsController.cs
│   └── UniversitiesController.cs
├── Services/
│   └── AuthService.cs   # Registration, login, JWT issuing
├── Data/
│   └── AppDbContext.cs  # EF Core DbContext + fluent model configuration
├── Entities/             # EF Core entities (the domain model)
├── Dtos/                 # Request/response DTOs (records)
├── Enums/
│   └── AppEnum.cs         # All app-wide enums in one file
├── Migrations/            # EF Core migration history
└── Program.cs             # DI, JWT config, middleware pipeline
```

### Domain model

The `AppDbContext` currently registers 13 entities, only some of which are wired up to controllers yet:

| Entity | Wired to a controller? | Notes |
|---|---|---|
| `University` | ✅ `UniversitiesController` | Full CRUD |
| `User` | ✅ via `AuthController` | Registration/login only, no profile endpoints yet |
| `Confession` | ✅ `ConfessionsController` | List (public, approved-only) + create (auth required) |
| `Category` | ⚠️ Referenced only | No CRUD endpoints yet |
| `Hashtag` / `ConfessionHashtag` | ⚠️ Modeled, not used | Many-to-many pivot exists in `AppDbContext` |
| `Comment` (with self-referencing `ParentId` for threaded replies) | ❌ Not implemented | |
| `Like` | ❌ Not implemented | Polymorphic via `LikeableId` / `LikeableType` |
| `Mention` | ❌ Not implemented | |
| `SavedPost` | ❌ Not implemented | |
| `Report` | ❌ Not implemented | `ReportReason` / `ReportStatus` enums already exist |
| `Notification` | ❌ Not implemented | Stores a `DataJson` payload |
| `ModerationLog` | ❌ Not implemented | Audit trail for admin actions |

Key modeling decisions (from `AppDbContext.OnModelCreating`):
- Enums are stored as **strings**, not ints, for readability directly in the database.
- `Confession.UserId` is **always stored**, even for anonymous confessions — anonymity is enforced at the API/response layer (`ConfessionsController` swaps in `"Anonymous"` before returning the DTO), not by omitting the author from the database. This preserves accountability/moderation ability.
- Several relationships use `DeleteBehavior.Restrict` (e.g. `Confession → User`, `Confession → ApprovedBy`, `Comment → Parent`, `Report → Reporter`, `ModerationLog → Admin`) specifically to avoid multiple cascade paths that PostgreSQL/EF would otherwise reject.
- Unique indexes on `User.Username`, `User.Email`, `Category.Slug`, `Hashtag.Tag`.

---

## Migrations

The project currently has **two** EF Core migrations:

### 1. `20260812094313_InitialCreate`
The baseline schema — creates all 13 tables described above (`Universities`, `Users`, `Confessions`, `Categories`, `Hashtags`, `ConfessionHashtags`, `Comments`, `Likes`, `Mentions`, `SavedPosts`, `Reports`, `Notifications`, `ModerationLogs`) along with their foreign keys, unique indexes, and enum-as-string conversions.

### 2. `20260812094536_FixUniversityTypo`
A same-day follow-up migration that renames the `Universities.CreateAt` column to `UpdatedAt` (the original column name was a typo/misnomer — it was actually meant to track update time, separate from `CreatedAt`).

```csharp
migrationBuilder.RenameColumn(
    name: "CreateAt",
    table: "Universities",
    newName: "UpdatedAt");
```

Applying migrations locally:

```bash
# Install the EF Core CLI tool once, if you don't have it
dotnet tool install --global dotnet-ef

# Apply all pending migrations to the configured database
dotnet ef database update

# After changing an entity, generate a new migration
dotnet ef migrations add <DescriptiveName>
```

> **Note:** With only two migrations and no seed data script in the repo yet, a fresh clone requires running `dotnet ef database update` against an empty PostgreSQL database before the API will start returning data (see [Getting Started](#getting-started)).

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 14+ running locally (or reachable via connection string)
- `dotnet-ef` CLI tool (`dotnet tool install --global dotnet-ef`)

### Setup

```bash
git clone https://github.com/rhondelp/UniSecret-API.git
cd UniSecret-API

# Restore packages
dotnet restore

# Configure your database connection & JWT secret (see Configuration below)

# Apply migrations
dotnet ef database update

# Run the API
dotnet run
```

By default the API listens on the port defined in `Properties/launchSettings.json`, and exposes Swagger UI at `/swagger` when running in the `Development` environment.

### Configuration

Connection string and JWT settings live in `appsettings.json` under `ConnectionStrings:DefaultConnection` and `JwtSettings`. **Do not commit real secrets here** — see [Security Notes](#security-notes) below for the recommended fix.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=unisecret_db;Username=postgres;Password=<your-password>"
  },
  "JwtSettings": {
    "Secret": "<a long, random secret — 32+ chars>",
    "Issuer": "UniSecretApi",
    "Audience": "UniSecretClient",
    "DurationInDays": 7
  }
}
```

---

## API Endpoints (current)

All routes are versioned under `/api/v1`.

| Method | Route | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/auth/register` | — | Registers a student; validates that the email domain matches the selected university's `Domain` |
| `POST` | `/api/v1/auth/login` | — | Authenticates and returns a JWT |
| `GET` | `/api/v1/universities` | — | Lists all universities |
| `GET` | `/api/v1/universities/{id}` | — | Gets one university |
| `POST` | `/api/v1/universities` | — | Creates a university (defaults to `Pending` status) |
| `PUT` | `/api/v1/universities/{id}` | — | Updates a university |
| `DELETE` | `/api/v1/universities/{id}` | — | Deletes a university |
| `GET` | `/api/v1/confessions?universityId=` | — | Lists **approved** confessions, optionally filtered by university; author identity is redacted when `IsAnonymous` is true |
| `POST` | `/api/v1/confessions` | ✅ JWT | Submits a confession; always starts as `Pending`, awaiting moderation |

> Note: the university and auth endpoints don't yet enforce `[Authorize]`/role checks (e.g. only admins should be able to create/update/delete universities) — see roadmap below.

---

## Security Notes

A few things worth fixing before any real deployment:

- **Hardcoded secrets in `appsettings.json`**: the PostgreSQL password and JWT signing secret are currently committed to source control. Move these to environment variables, `dotnet user-secrets` (local dev), or a secret manager (Azure Key Vault, AWS Secrets Manager, etc.) in any shared or production environment, and rotate both values.
- **`RequireHttpsMetadata = false`** on the JWT bearer options should be re-enabled (`true`) outside local development.
- Admin-only endpoints (university management, moderation) have no role-based authorization yet — anyone can currently call `POST/PUT/DELETE /api/v1/universities`.

---

## Roadmap / Future Plans

Based on the entities and enums already modeled but not yet exposed via the API, the natural next milestones are:

- [ ] **Comments & threaded replies** — `Comment` entity already supports self-referencing `ParentId`/`Replies`; needs a `CommentsController`.
- [ ] **Likes** — polymorphic `Like` entity (`LikeableId` + `LikeableType`) for liking both confessions and comments.
- [ ] **Hashtags** — surfacing/parsing hashtags from confession bodies and exposing trending-tag/browse-by-tag endpoints via the existing `Hashtag`/`ConfessionHashtag` pivot.
- [ ] **Categories CRUD** — currently referenced by confessions but has no management endpoints.
- [ ] **Moderation workflow** — endpoints for admins to approve/reject confessions (`ConfessionStatus.Approved`/`Rejected`/`Scheduled`), backed by `ModerationLog` for auditability.
- [ ] **Reports** — endpoint(s) for users to report confessions/comments (`ReportReason`, `ReportStatus`), plus an admin review queue.
- [ ] **Notifications** — surfacing `Notification` rows (replies, mentions, approval/rejection) to the client, likely via polling or push.
- [ ] **Saved posts** — bookmarking endpoints backed by the existing `SavedPost` entity.
- [ ] **Mentions** — parsing `@username` in comments and populating `Mention` for notification purposes.
- [ ] **Role-based authorization** — `[Authorize(Roles = "Admin")]` (or policy-based) on university/category/moderation management endpoints, using the `UserRole` claim already embedded in the JWT.
- [ ] **Scheduled confessions** — `Confession.ScheduledAt` exists in the model; a background job (e.g. hosted service) to flip scheduled confessions to `Approved`/published at the right time.
- [ ] **Secrets hygiene** — move connection string and JWT secret out of `appsettings.json` (see Security Notes).
- [ ] **Seed data / migration for lookup data** — a migration or startup seeder for default `Category` values would remove the current friction of manually inserting categories before confessions can be created.
- [ ] **Automated tests** — no test project currently exists in the repo.

---

## License

No license file is currently included in this repository — add one (e.g. MIT) before accepting external contributions.