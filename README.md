# TaskFlow API

A production-grade **multi-tenant Task Management API** built with **.NET 8**, demonstrating advanced backend architecture patterns used in real enterprise systems.

---

## Architecture & Patterns

### Clean Architecture
Four strictly isolated layers with clear dependency rules:
```
API → Application → Domain ← Infrastructure
```
The Domain has **zero external dependencies** — it is the heart of the system.

### CQRS + MediatR
Every use case is a discrete Command or Query with its own Handler — no bloated service classes. The MediatR pipeline adds cross-cutting concerns automatically:

```
Request → LoggingBehavior → ValidationBehavior → DomainEventBehavior → Handler
```

### Multi-Tenancy
Every entity carries a `TenantId`. All repository queries filter by the current tenant derived from the JWT claims — no request can ever read or write another tenant's data.

### Domain Events
State transitions on `TaskItem` raise strongly-typed domain events:
- `TaskCompletedEvent` — fired when a task is completed
- `TaskCancelledEvent` — fired when a task is cancelled  
- `TaskAssignedEvent` — fired when a task is assigned to a user

These decouple downstream side-effects (notifications, audit logs, etc.) from the core business logic.

### Redis Caching
Task lists are cached per-tenant with a short TTL. Any write operation (create, update, status change) invalidates the relevant cache keys via pattern-based deletion.

---

## Tech Stack

| Concern | Technology |
|---------|------------|
| Framework | .NET 8 / ASP.NET Core |
| Architecture | Clean Architecture + CQRS |
| Messaging | MediatR + Pipeline Behaviors |
| ORM | Entity Framework Core 8 |
| Database | SQL Server |
| Caching | Redis (StackExchange.Redis) |
| Auth | JWT Bearer (BCrypt password hashing) |
| Validation | FluentValidation |
| Testing | NUnit + Moq + FluentAssertions |
| API Docs | Swagger / OpenAPI with JWT support |

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or Docker)
- Redis (local or Docker)

### Quick start with Docker

```bash
# SQL Server
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourPass123!" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

# Redis
docker run -p 6379:6379 -d redis:alpine
```

### Run the API

```bash
git clone https://github.com/leowai1986/task-flow-api.git
cd taskflow-api

# Update appsettings.json with your connection strings and a strong JWT secret
dotnet run --project src/API
```

Browse to `https://localhost:7001/swagger`

### Run tests

```bash
dotnet test
```

---

## API Flow

### 1. Create a tenant (and get your admin JWT)
```
POST /api/auth/register-tenant
{ "name": "Acme Corp", "slug": "acme", "adminEmail": "...", "adminFullName": "...", "adminPassword": "..." }
```

### 2. Authenticate all subsequent requests
Add `Authorization: Bearer {token}` header.

### 3. Manage tasks
```
POST   /api/tasks                   # Create
GET    /api/tasks?status=Todo&priority=High&page=1&pageSize=20  # List with filters
GET    /api/tasks/{id}              # Get with comments
PUT    /api/tasks/{id}              # Update
PATCH  /api/tasks/{id}/start        # Todo → InProgress
PATCH  /api/tasks/{id}/complete     # → Done (fires TaskCompletedEvent)
PATCH  /api/tasks/{id}/cancel       # → Cancelled (fires TaskCancelledEvent)
PATCH  /api/tasks/{id}/assign       # Assign to user (fires TaskAssignedEvent)
DELETE /api/tasks/{id}              # Delete (owner or Admin)
POST   /api/tasks/{id}/comments     # Add comment
```

---

## Project Structure

```
taskflow-api/
├── src/
│   ├── Domain/                         # Entities, Domain Events, Interfaces, Exceptions
│   │   ├── Entities/                   # Tenant, User, TaskItem, TaskComment, BaseEntity
│   │   ├── Events/                     # TaskCompletedEvent, TaskCancelledEvent, TaskAssignedEvent
│   │   ├── Interfaces/                 # ITaskRepository, IUserRepository, ITenantRepository
│   │   └── Exceptions/                 # DomainException, NotFoundException, UnauthorizedException
│   ├── Application/                    # Use cases — Commands, Queries, Handlers
│   │   ├── Common/
│   │   │   ├── Behaviors/              # MediatR pipeline: Logging, Validation, DomainEvents
│   │   │   ├── Interfaces/             # ICurrentUserService, IJwtService, ICacheService
│   │   │   └── Models/                 # PagedResult<T>, CurrentUser, Result<T>
│   │   └── Features/
│   │       ├── Auth/Commands/          # RegisterCommand, LoginCommand, CreateTenantCommand
│   │       ├── Tasks/Commands/         # Create, Update, Start, Complete, Cancel, Assign, Delete
│   │       ├── Tasks/Queries/          # GetTasksQuery (paged), GetTaskByIdQuery
│   │       └── Comments/Commands/      # AddCommentCommand
│   ├── Infrastructure/                 # EF Core, Redis, JWT, BCrypt
│   │   ├── Data/                       # AppDbContext, EF configurations, indexes
│   │   ├── Repositories/               # TaskRepository, UserRepository, TenantRepository
│   │   ├── Cache/                      # RedisCacheService with pattern invalidation
│   │   └── Identity/                   # JwtService, BcryptPasswordHasher, CurrentUserService
│   └── API/                            # Controllers, Middleware, Program.cs
│       ├── Controllers/                # AuthController, TasksController
│       └── Middleware/                 # Global ExceptionMiddleware
└── tests/
    └── Application.Tests/              # NUnit + Moq + FluentAssertions
        └── Tasks/                      # Domain tests + Handler tests
```

---

## Author

**Leandro Wainer** — Senior .NET Developer  
[linkedin.com/in/lwainer](https://linkedin.com/in/lwainer)
