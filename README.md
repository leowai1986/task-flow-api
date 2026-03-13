# TaskFlow API

A production-grade **multi-tenant Task Management API** built with **.NET 8**, demonstrating advanced backend architecture patterns used in real enterprise systems.

![CI](https://github.com/leowai1986/task-flow-api/actions/workflows/ci.yml/badge.svg)

---

## Architecture & Patterns

| Pattern | Implementation |
|---|---|
| Clean Architecture | Domain → Application → Infrastructure → API |
| CQRS + MediatR | Every use case is a Command or Query with its own Handler |
| Multi-Tenancy | TenantId on every entity, enforced via JWT claims |
| Domain Events | TaskCompletedEvent, TaskCancelledEvent, TaskAssignedEvent |
| Outbox Pattern | Reliable event delivery with retry via BackgroundService |
| Soft Delete | IsDeleted + global EF Core query filter |
| Audit Log | Every write operation logged with user, action, entity, timestamp |
| JWT + Refresh Tokens | Access token (60min) + rotating refresh token (30 days) |
| Redis Caching | Per-tenant cache with pattern-based invalidation |
| Rate Limiting | 100 req/min global, 10 req/min on auth endpoints |
| Pipeline Behaviors | Logging → Validation → DomainEvents on every request |
| Idempotency | 24h key-based deduplication via Redis |

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | .NET 8 / ASP.NET Core |
| ORM | Entity Framework Core 8 |
| Database | SQL Server |
| Caching | Redis (StackExchange.Redis) |
| Auth | JWT Bearer + BCrypt + Refresh Tokens |
| Validation | FluentValidation |
| Logging | Serilog (Console + File) |
| Testing | NUnit + Moq + FluentAssertions |
| Integration Tests | WebApplicationFactory + InMemory DB |
| CI/CD | GitHub Actions → Azure App Service |
| Containerization | Docker + docker-compose |

---

## Quick Start

### With Docker (recommended)

```bash
git clone https://github.com/lwainer/taskflow-api.git
cd taskflow-api
docker-compose up --build
```

Browse to `http://localhost:5001/swagger`

### Without Docker

```bash
# Start SQL Server and Redis first, then:
dotnet run --project src/API
```

---

## API Endpoints

### Auth
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register-tenant` | Create tenant + admin user |
| POST | `/api/auth/register` | Register user in existing tenant |
| POST | `/api/auth/login` | Login → access + refresh tokens |
| POST | `/api/auth/refresh` | Rotate refresh token |
| POST | `/api/auth/revoke` | Logout / revoke refresh token |

### Tasks (requires Bearer token)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/tasks` | Paged list with filters + sorting |
| GET | `/api/tasks/{id}` | Detail with comments |
| POST | `/api/tasks` | Create task |
| PUT | `/api/tasks/{id}` | Update task |
| PATCH | `/api/tasks/{id}/start` | Todo → InProgress |
| PATCH | `/api/tasks/{id}/complete` | → Done (fires event) |
| PATCH | `/api/tasks/{id}/cancel` | → Cancelled (fires event) |
| PATCH | `/api/tasks/{id}/assign` | Assign to user |
| DELETE | `/api/tasks/{id}` | Soft delete |
| POST | `/api/tasks/{id}/comments` | Add comment |

### Metrics
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/metrics` | Totals, overdue, avg completion, top tags, tasks per user |

### System
| Method | Endpoint | Description |
|---|---|---|
| GET | `/health` | SQL Server + Redis health check |

---

## Deploy to Azure

1. Create an **Azure App Service** (Linux, .NET 8)
2. Download the **Publish Profile** from the Azure Portal
3. Add it as a GitHub secret named `AZURE_WEBAPP_PUBLISH_PROFILE`
4. Update `AZURE_WEBAPP_NAME` in `.github/workflows/ci.yml`
5. Push to `main` — CI runs tests then deploys automatically

---

## Running Tests

```bash
# Unit tests (domain + handler tests with Moq)
dotnet test tests/Application.Tests

# Integration tests (full HTTP flow with InMemory DB)
dotnet test tests/Integration.Tests

# All tests
dotnet test
```

---

## Author

**Leandro Wainer** — Senior .NET Developer  
[linkedin.com/in/lwainer](https://linkedin.com/in/lwainer)
