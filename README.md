# 🎓 UniSecret API

Backend REST API for **UniSecret** — an anonymous university confession platform. Built with ASP.NET Core (.NET 10), PostgreSQL, and Redis. Serves the [UniSecret Mobile App](https://github.com/rhondelp/UniSecret-MobileApp) (React Native / Expo).

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Npgsql-4169E1?logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Distributed%20Cache-DC382D?logo=redis&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT%20Bearer-black?logo=jsonwebtokens&logoColor=white)
![Swagger](https://img.shields.io/badge/API%20Docs-Swagger-85EA2D?logo=swagger&logoColor=black)
![EF Core](https://img.shields.io/badge/ORM-EF%20Core%2010-512BD4?logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/status-in--development-yellow)

---

## 🧱 Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Web API — .NET 10 |
| Database | PostgreSQL (via Npgsql EF Core provider) |
| ORM | Entity Framework Core 10 |
| Caching | In-memory (`IMemoryCache`) + Redis (`StackExchangeRedisCache`) — two-tier |
| Auth | JWT Bearer tokens |
| Password Hashing | BCrypt.Net-Next |
| API Docs | Swashbuckle (Swagger / OpenAPI) |
| Rate Limiting | Built-in ASP.NET Core `System.Threading.RateLimiting` |
| Health Checks | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |

## 📦 Packages Used

```xml
BCrypt.Net-Next                                              4.2.0
Microsoft.AspNetCore.Authentication.JwtBearer                10.0.11
Microsoft.AspNetCore.OpenApi                                 10.0.0
Microsoft.EntityFrameworkCore.Tools                          10.0.10
Microsoft.Extensions.Caching.StackExchangeRedis               10.0.11
Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore  10.0.11
Npgsql.EntityFrameworkCore.PostgreSQL                         10.0.3
Swashbuckle.AspNetCore                                        10.2.3
```

| Package | Why it's here |
|---|---|
| **BCrypt.Net-Next** | Hashes user passwords before they touch the database. |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | Validates JWT bearer tokens on protected endpoints. |
| **Microsoft.AspNetCore.OpenApi** | Generates the OpenAPI schema consumed by Swagger. |
| **Microsoft.EntityFrameworkCore.Tools** | Enables `dotnet ef migrations` / `dotnet ef database update` from the CLI. |
| **Microsoft.Extensions.Caching.StackExchangeRedis** | Distributed cache provider so multiple API instances behind NGINX share cached data. |
| **Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore** | Powers the `/health` endpoint used by load balancers / orchestration. |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | EF Core provider for PostgreSQL, with built-in transient-failure retry. |
| **Swashbuckle.AspNetCore** | Generates the interactive Swagger UI at `/swagger` (dev only). |

## 📁 Project Structure

```
UniSecret-API/
├── Controllers/        # Auth, Confessions, Universities
├── Data/                # AppDbContext (EF Core)
├── Dtos/                # Request/response DTOs
├── Entities/             # EF Core entity models (13 tables)
├── Enums/                # Status/role enums
├── Migrations/           # EF Core migration history
├── Services/             # AuthService, CacheService
├── Program.cs            # App bootstrap: DI, auth, rate limiting, middleware
└── appsettings.json      # Configuration (connection strings, JWT settings)
```

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local or Docker)
- Redis (optional — app runs fine without it, falls back to memory cache only)

### Setup

1. **Clone and restore**
   ```bash
   git clone https://github.com/rhondelp/UniSecret-API.git
   cd UniSecret-API
   dotnet restore
   ```

2. **Configure secrets** — don't edit `appsettings.json` directly for local dev. Use `dotnet user-secrets` instead:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=unisecret_db;Username=postgres;Password=yourpassword"
   dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
   dotnet user-secrets set "JwtSettings:Secret" "a-long-random-secret-at-least-32-chars"
   ```
   `JwtSettings:Issuer`, `JwtSettings:Audience`, and `JwtSettings:DurationInDays` can stay in `appsettings.json` since they aren't secret.

3. **Apply migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the API**
   ```bash
   dotnet run
   ```
   - HTTP: `http://localhost:5277`
   - HTTPS: `https://localhost:7070`
   - Swagger UI (dev only): `http://localhost:5277/swagger`
   - Health check: `GET /health`

## 🔐 Authentication

All protected endpoints require a JWT bearer token in the `Authorization` header:

```
Authorization: Bearer <token>
```

- `POST /api/v1/auth/register` — create an account, returns a token.
- `POST /api/v1/auth/login` — authenticate, returns a token.
- Tokens are signed with `JwtSettings:Secret` and validated for issuer, audience, lifetime, and signature. No clock skew allowance — expired means expired.

## 🌐 API Endpoints (current)

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/v1/auth/register` | — | Register a new user |
| POST | `/api/v1/auth/login` | — | Log in, get JWT |
| GET | `/api/v1/confessions` | — | List approved confessions (paginated, optional `universityId` filter) |
| POST | `/api/v1/confessions` | ✅ | Submit a confession (goes to `Pending` for moderation) |
| GET | `/api/v1/universities` | — | List universities (Redis/memory cached) |
| GET | `/api/v1/universities/{id}` | — | Get one university (cached) |
| POST | `/api/v1/universities` | — | Create a university |
| PUT | `/api/v1/universities/{id}` | — | Update a university |
| DELETE | `/api/v1/universities/{id}` | — | Delete a university |
| GET | `/health` | — | Health check for DB connectivity |

> The University create/update/delete endpoints aren't `[Authorize]`-protected yet — see **Future Plans** below. They should be admin-only before this goes to production.

## ⏱️ Rate Limiting

Global limiter, partitioned by client IP:

- **`/api/v1/auth/*`** — 10 requests/minute (auth ops are CPU-heavy due to BCrypt).
- **All other endpoints** — 120 requests/minute.
- `/health` and `/swagger` are exempt.
- Returns `429 Too Many Requests` when exceeded. Limits are provisional — tune with load testing.

## ⚡ Caching Strategy

Two-tier read-through cache (`CacheService`):

1. **L1 — In-memory** (`IMemoryCache`, ~30s TTL): fastest, but local to a single instance.
2. **L2 — Redis** (`IDistributedCache`, ~10min TTL): shared across instances behind NGINX.

A read checks memory first, then Redis, then the database — populating the faster tiers on the way back. Redis is optional; if `ConnectionStrings:Redis` isn't set, the app runs on memory cache alone. Currently used by the Universities endpoints.

## 🗄️ Database Schema (EF Core entities)

13 tables modeled: `University`, `User`, `Confession`, `Category`, `Hashtag`, `ConfessionHashtag` (join table), `Comment` (self-referencing for threaded replies), `Like` (polymorphic via `LikeableType`/`LikeableId`), `Mention`, `SavedPost`, `Report` (polymorphic via `ReportableType`/`ReportableId`), `Notification`, `ModerationLog`.

Notable design choices:
- Enums (`Status`, `Role`, `Reason`, etc.) are stored as strings, not ints — readable directly in the DB.
- Composite indexes on `Confession(Status, CreatedAt)` and `Confession(UniversityId, Status, CreatedAt)` support the public feed's filter + sort pattern.
- Unique indexes on `User.Username`, `User.Email`, `Category.Slug`, `Hashtag.Tag`.
- `Like` and `Report` use a polymorphic pattern (`*ableType` + `*ableId`) so one table covers both Confessions and Comments.

## 🔄 Migration History

| Migration | Date | Change |
|---|---|---|
| `InitialCreate` | 2026-08-12 | Initial schema — all 13 tables, indexes, FKs. |
| `FixUniversityTypo` | 2026-08-12 | Renamed `Universities.CreateAt` → `UpdatedAt` (typo from the initial migration). |

**Running migrations:**
```bash
# Create a new migration after changing an entity
dotnet ef migrations add <MigrationName>

# Apply pending migrations to the database
dotnet ef database update

# Roll back to a specific migration
dotnet ef database update <PreviousMigrationName>

# Remove the last (unapplied) migration
dotnet ef migrations remove
```

## ⚠️ Security Notes

- ⚠️ `appsettings.json` currently has a real-looking Postgres password and JWT secret committed to source. Rotate both and move them out of the repo (`dotnet user-secrets` locally, environment variables / a secrets manager in production) before this API is exposed publicly.
- Passwords are hashed with BCrypt — never logged or returned in DTOs.
- `RequireHttpsMetadata` is only relaxed in the `Development` environment; production enforces HTTPS.
- University write endpoints currently have no `[Authorize]` attribute — anyone can create/edit/delete a university. Lock these down before production.

## 🛣️ Future Plans

The `Entities/` folder already models more of the product than the `Controllers/` folder exposes — these are the next logical endpoints to build, in rough priority order:

1. **Comments** — CRUD + threaded replies (`Comment.ParentId` is already modeled).
2. **Likes** — like/unlike a Confession or Comment (polymorphic `Like` entity is ready).
3. **Reports & Moderation** — report a Confession/Comment, and an admin moderation queue backed by `ModerationLog` and `Confession.Status` transitions (`Pending → Approved/Rejected`).
4. **Notifications** — surface `Notification` rows (replies, mentions, approvals) to the mobile app, likely via a polling or push endpoint.
5. **Saved Posts** — bookmark/unbookmark a Confession.
6. **Hashtags & Mentions** — parse `#tags` and `@mentions` on submit; expose hashtag search/trending.
7. **Authorization hardening** — role-based policies (`Student` / `Admin` / `SuperAdmin`) for moderation and university-management endpoints, instead of the current all-or-nothing `[Authorize]`.
8. **Refresh tokens** — access tokens currently have no refresh flow; add refresh token rotation so users aren't forced to re-login every `DurationInDays`.
9. **Scheduled confessions** — `Confession.ScheduledAt` exists in the schema but nothing currently publishes a scheduled confession when its time arrives (needs a background job, e.g. `IHostedService` or Hangfire).
10. **Secrets/config cleanup** — move connection strings and JWT secret out of `appsettings.json` (see Security Notes).
11. **Testing** — no test project exists yet; add unit tests for `AuthService`/`CacheService` and integration tests for controllers (`WebApplicationFactory`).
12. **CI/CD** — no pipeline yet; add a GitHub Actions workflow for build, test, and `dotnet ef migrations` validation on PRs.

## 🔗 Related Repository

- [UniSecret-MobileApp](https://github.com/rhondelp/UniSecret-MobileApp) — React Native (Expo, TypeScript) client that consumes this API.