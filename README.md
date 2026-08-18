# 🎓 UniSecret API

Backend REST API for **UniSecret** — an anonymous university confession platform. Built with ASP.NET Core (.NET 10), PostgreSQL, and Redis.

Serves the [UniSecret Mobile App](https://github.com/rhondelp/UniSecret-MobileApp) (React Native / Expo).

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Npgsql-4169E1?logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-Distributed%20Cache-DC382D?logo=redis&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT%20Bearer-black?logo=jsonwebtokens&logoColor=white)
![Swagger](https://img.shields.io/badge/API%20Docs-Swagger-85EA2D?logo=swagger&logoColor=black)
![EF Core](https://img.shields.io/badge/ORM-EF%20Core%2010-512BD4?logo=dotnet&logoColor=white)
![Status](https://img.shields.io/badge/status-in--development-yellow)

---

## 📖 Overview

UniSecret lets students post and browse anonymous confessions scoped to their university, comment on threads, like posts, save favorites, follow trending hashtags, and report abuse. This repo is the API layer — it handles auth, data persistence, caching, rate limiting, and moderation for the platform.

## 🧱 Tech Stack

| Layer            | Technology                                                          |
| ----------------- | -------------------------------------------------------------------- |
| Framework         | ASP.NET Core Web API — .NET 10                                       |
| Database          | PostgreSQL (via Npgsql EF Core provider)                             |
| ORM               | Entity Framework Core 10                                             |
| Caching           | Two-tier — in-memory (`IMemoryCache`) + Redis (`StackExchangeRedisCache`) |
| Auth              | JWT Bearer tokens                                                    |
| Password Hashing  | BCrypt.Net-Next                                                      |
| API Docs          | Swashbuckle (Swagger / OpenAPI)                                      |
| Rate Limiting     | Built-in ASP.NET Core `System.Threading.RateLimiting`                |
| Health Checks     | `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`  |

## 📦 Key Packages

| Package | Version | Why it's here |
| --- | --- | --- |
| `BCrypt.Net-Next` | 4.2.0 | Hashes user passwords before they touch the database |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.11 | Validates JWT bearer tokens on protected endpoints |
| `Microsoft.AspNetCore.OpenApi` | 10.0.0 | Generates the OpenAPI schema consumed by Swagger |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.10 | Enables `dotnet ef migrations` / `dotnet ef database update` from the CLI |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.11 | Distributed cache so multiple API instances behind NGINX share cached data |
| `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` | 10.0.11 | Powers the `/health` endpoint used by load balancers / orchestration |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | EF Core provider for PostgreSQL, with built-in transient-failure retry |
| `Swashbuckle.AspNetCore` | 10.2.3 | Generates the interactive Swagger UI at `/swagger` (dev only) |

## 📁 Project Structure

```
UniSecret-API/
├── Controllers/           # Auth, Confessions, Comments, Likes, SavedPosts,
│                          # Reports, Moderation, Categories, Hashtags, Universities
├── Data/                  # AppDbContext (EF Core)
├── Dtos/                  # Request/response DTOs
├── Entities/              # EF Core entity models (13 tables)
├── Enums/                 # Status/role enums
├── Migrations/            # EF Core migration history
├── Services/              # AuthService, CacheService
├── Program.cs             # App bootstrap — DI, auth, rate limiting, middleware
└── appsettings.json        # Configuration (connection strings, JWT settings)
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local or Docker)
- Redis *(optional — the app falls back to memory-only caching if it's not configured)*

### Setup

**1. Clone and restore**

```bash
git clone https://github.com/rhondelp/UniSecret-API.git
cd UniSecret-API
dotnet restore
```

**2. Configure secrets** — don't edit `appsettings.json` directly for local dev; use `dotnet user-secrets`:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=unisecret_db;Username=postgres;Password=yourpassword"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
dotnet user-secrets set "JwtSettings:Secret" "a-long-random-secret-at-least-32-chars"
```

> `JwtSettings:Issuer`, `JwtSettings:Audience`, and `JwtSettings:DurationInDays` can stay in `appsettings.json` since they aren't secret.

**3. Apply migrations**

```bash
dotnet ef database update
```

**4. Run the API**

```bash
dotnet run
```

| Endpoint | URL |
| --- | --- |
| HTTP | `http://localhost:5277` |
| HTTPS | `https://localhost:7070` |
| Swagger UI (dev only) | `http://localhost:5277/swagger` |
| Health check | `GET /health` |

## 🔐 Authentication

All protected endpoints require a JWT bearer token in the `Authorization` header:

```
Authorization: Bearer <token>
```

- `POST /api/v1/auth/register` — create an account, returns a token
- `POST /api/v1/auth/login` — authenticate, returns a token
- Tokens are signed with `JwtSettings:Secret` and validated for issuer, audience, lifetime, and signature. No clock skew allowance — expired means expired.

Role-based access uses three tiers — `Student`, `Admin`, `SuperAdmin` — enforced via `[Authorize(Roles = "...")]`.

## 🌐 API Endpoints

### Auth
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register` | — | Register a new user |
| POST | `/api/v1/auth/login` | — | Log in, get JWT |

### Confessions
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/v1/confessions` | — | List approved confessions (paginated, filterable) |
| GET | `/api/v1/confessions/{id}` | — | Get a single confession |
| POST | `/api/v1/confessions` | ✅ | Submit a confession (starts as `Pending`; hashtags auto-parsed) — blocked for suspended/banned users |

### Comments
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/v1/confessions/{confessionId}/comments` | — | List comments on a confession (paginated) |
| POST | `/api/v1/confessions/{confessionId}/comments` | ✅ | Add a comment (threaded replies supported) |
| DELETE | `/api/v1/comments/{id}` | ✅ | Delete your own comment |

### Likes
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| POST | `/api/v1/likes/toggle` | ✅ | Like/unlike a Confession or Comment (polymorphic) |
| GET | `/api/v1/likes/status` | — | Check like status/count for a target |

### Saved Posts
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| POST | `/api/v1/savedposts/{confessionId}/toggle` | ✅ | Bookmark/unbookmark a confession |
| GET | `/api/v1/savedposts` | ✅ | List the current user's saved posts |

### Reports & Moderation
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| POST | `/api/v1/reports` | ✅ | Report a Confession or Comment |
| GET | `/api/v1/reports` | Admin/SuperAdmin | List reports |
| GET | `/api/v1/moderation/queue` | Admin/SuperAdmin | Pending confessions queue |
| POST | `/api/v1/moderation/confessions/{id}/review` | Admin/SuperAdmin | Approve/reject a confession |
| POST | `/api/v1/moderation/users/{id}/status` | Admin/SuperAdmin | Suspend/ban/reinstate a user |
| GET | `/api/v1/moderation/logs` | Admin/SuperAdmin | Audit log of moderation actions |

### Categories & Hashtags
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/v1/categories` | — | List categories |
| GET | `/api/v1/categories/{id}` | — | Get a category |
| GET | `/api/v1/hashtags/trending` | — | Trending hashtags |
| GET | `/api/v1/hashtags/search` | — | Search hashtags |

### Universities
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/api/v1/universities` | — | List universities (cached) |
| GET | `/api/v1/universities/{id}` | — | Get one university (cached) |
| POST | `/api/v1/universities` | Admin/SuperAdmin | Create a university |
| PUT | `/api/v1/universities/{id}` | Admin/SuperAdmin | Update a university |
| DELETE | `/api/v1/universities/{id}` | Admin/SuperAdmin | Delete a university |

### System
| Method | Route | Auth | Description |
| --- | --- | --- | --- |
| GET | `/health` | — | Health check for DB connectivity |

## ⏱️ Rate Limiting

Global limiter, partitioned by client IP:

- **`/api/v1/auth/*`** — 10 requests/minute (auth ops are CPU-heavy due to BCrypt)
- **All other endpoints** — 120 requests/minute
- `/health` and `/swagger` are exempt
- Returns `429 Too Many Requests` when exceeded — limits are provisional and should be tuned with load testing

## ⚡ Caching Strategy

Two-tier read-through cache (`CacheService`):

1. **L1 — In-memory** (`IMemoryCache`, ~30s TTL) — fastest, but local to a single instance
2. **L2 — Redis** (`IDistributedCache`, ~10min TTL) — shared across instances behind NGINX

A read checks memory first, then Redis, then the database — populating the faster tiers on the way back. Redis is optional; if `ConnectionStrings:Redis` isn't set, the app runs on memory cache alone. Currently used by the Universities endpoints.

## 🗄️ Database Schema

13 tables modeled: `University`, `User`, `Confession`, `Category`, `Hashtag`, `ConfessionHashtag` (join table), `Comment` (self-referencing for threaded replies), `Like` (polymorphic via `LikeableType`/`LikeableId`), `Mention`, `SavedPost`, `Report` (polymorphic via `ReportableType`/`ReportableId`), `Notification`, `ModerationLog`.

All tables except `Notification` and `Mention` currently have live endpoints.

**Notable design choices:**
- Enums (`Status`, `Role`, `Reason`, etc.) are stored as strings, not ints — readable directly in the DB
- Composite indexes on `Confession(Status, CreatedAt)` and `Confession(UniversityId, Status, CreatedAt)` support the public feed's filter + sort pattern
- Unique indexes on `User.Username`, `User.Email`, `Category.Slug`, `Hashtag.Tag`
- `Like` and `Report` use a polymorphic pattern (`*ableType` + `*ableId`) so one table covers both Confessions and Comments

## 🔄 Migrations

| Migration | Date | Change |
| --- | --- | --- |
| `InitialCreate` | 2026-08-12 | Initial schema — all 13 tables, indexes, FKs |
| `FixUniversityTypo` | 2026-08-12 | Renamed `Universities.CreateAt` → `UpdatedAt` |

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

- **`appsettings.json` currently has a real-looking Postgres password and JWT secret committed to source.** Rotate both and move them out of the repo (`dotnet user-secrets` locally, environment variables / a secrets manager in production) before this API is exposed publicly.
- Passwords are hashed with BCrypt — never logged or returned in DTOs.
- `RequireHttpsMetadata` is only relaxed in the `Development` environment; production enforces HTTPS.
- Role-based authorization is enforced: University writes, moderation, and report listing all require `Admin`/`SuperAdmin`. Suspended/banned users are blocked from posting confessions.

## 🛣️ Roadmap

Comments, Likes, Saved Posts, Reports, Moderation, Categories, and Hashtags are shipped. Remaining work:

1. **Notifications** — surface `Notification` rows (replies, mentions, approvals) to the mobile app, likely via polling or a push endpoint. `@mention` parsing on submit is also still open (hashtag parsing already ships).
2. **Refresh tokens** — access tokens have no refresh flow yet; add refresh token rotation so users aren't forced to re-login every `DurationInDays`.
3. **Scheduled confessions** — `ConfessionStatus.Scheduled` exists in the schema but nothing publishes a scheduled confession when its time arrives (needs a background job, e.g. `IHostedService` or Hangfire).
4. **Secrets/config cleanup** — move connection strings and the JWT secret out of `appsettings.json` (see Security Notes).
5. **Testing** — no test project exists yet; add unit tests for `AuthService`/`CacheService` and integration tests for controllers (`WebApplicationFactory`).
6. **CI/CD** — add a GitHub Actions workflow for build, test, and `dotnet ef migrations` validation on PRs.
7. **Rate limiting for new endpoints** — confirm Comments/Likes/Reports/Moderation routes sit under the general 120 req/min limiter, or give moderation actions their own tier.

## 🔗 Related Repository

- [UniSecret-MobileApp](https://github.com/rhondelp/UniSecret-MobileApp) — React Native (Expo, TypeScript) client that consumes this API

## 📄 License

No license file is currently included in this repository.