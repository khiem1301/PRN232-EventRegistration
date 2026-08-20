# Integration Contract — Khiêm (Member 1)

> Baseline for TV2 (Events) and TV3 (Registration/Feedback) integration.

## Shared Infrastructure

| Component | Location | Usage |
|-----------|----------|-------|
| `AppDbContext` | `EventApi/Infrastructure/Data/AppDbContext.cs` | Single DbContext — add DbSets via new migrations |
| `GenericRepository<T>`, `IUnitOfWork` | `EventApi/Infrastructure/Repositories/` | Inject `IUnitOfWork` in your services |
| `Result<T>`, `PagedResult<T>` | `EventApi/Domain/Common/Result.cs` | Return from services; use `ToActionResult` in controllers |
| JWT + Roles | Configured in `EventApi/Program.cs` | Use `[Authorize(Roles = "...")]` on your controllers |
| Content Negotiation | Global in `Program.cs` | JSON + XML supported; other formats return 406 |

## Database

- Connection string: `appsettings.json` → `DefaultConnection`
- Baseline migration: `Initial_Khiem_User_Location_Organizer`
- Seed users:
  - `admin@fpt.edu.vn` / `Admin@123` (Admin)
  - `staff@fpt.edu.vn` / `Staff@123` (Staff)

### TV2 — Events

Add/extend `Event` entity in `Domain/Entities/Event.cs` (stub FKs already exist: `LocationId`, `OrganizerId`, `CreatedById`).

```bash
dotnet ef migrations add TV2_Events -p EventApi
dotnet ef database update -p EventApi
```

### TV3 — Registration & Feedback

Entities `Registration` and `Feedback` already exist as stubs in `Domain/Entities/`.

```bash
dotnet ef migrations add TV3_Registration_Feedback -p EventApi
dotnet ef database update -p EventApi
```

## API Endpoints (Khiêm)

| Group | Routes |
|-------|--------|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Account | `GET/PUT /api/account/me`, `PUT /api/account/me/password` |
| Users | CRUD `/api/users` (Admin only) |
| Catalog | CRUD `/api/locations`, `/api/organizers` |

## Business Rules Implemented

- **BR-G-01**: Unique email on register/create user
- **BR-G-02**: `/api/account/me` scoped to authenticated user
- **BR-G-03**: PBKDF2 password hashing (100k iterations, SHA256)
- **BR-G-04**: Role-based authorization policies
- **BR-E-05**: Block delete Location/Organizer when referenced by Events

## Running Locally

```bash
# Terminal 1 — API
dotnet run --project EventApi

# Terminal 2 — WebClient
dotnet run --project EventRegistration.WebClient
```

- API Swagger: https://localhost:7026/swagger
- WebClient: https://localhost:7150
