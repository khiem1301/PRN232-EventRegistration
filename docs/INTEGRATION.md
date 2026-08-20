# Integration Contract — Khiêm (Member 1) + TV2 (Member 2)

> Baseline for TV2 (Events) and TV3 (Registration/Feedback) integration.

## Shared Infrastructure

| Component | Location | Usage |
|-----------|----------|-------|
| `AppDbContext` | `EventApi/Infrastructure/Data/AppDbContext.cs` | Single DbContext — add DbSets via new migrations |
| `GenericRepository<T>`, `IUnitOfWork` | `EventApi/Infrastructure/Repositories/` | Inject `IUnitOfWork` in your services |
| `Result<T>`, `PagedResult<T>` | `EventApi/Domain/Common/Result.cs` | Return from services; use `ToActionResult` in controllers |
| JWT + Roles | Configured in `EventApi/Program.cs` | Use `[Authorize(Roles = "...")]` on your controllers |
| Content Negotiation | Global in `Program.cs` | JSON + XML supported; other formats return 406 |
| OData | `EventApi/Application/OData/ODataEdmModel.cs` | `GET /odata/Events` (Staff, Admin) |

## Database

- Connection string: `appsettings.json` → `DefaultConnection`
- Baseline migration: `Initial_Khiem_User_Location_Organizer`
- Seed users:
  - `admin@fpt.edu.vn` / `Admin@123` (Admin)
  - `staff@fpt.edu.vn` / `Staff@123` (Staff)
- Seed events: 5 sample events (Draft, Published, Ongoing, Completed, Cancelled) via `DbInitializer.SeedEventsAsync`

### TV2 — Events (Implemented)

Entity `Event` in `Domain/Entities/Event.cs`. No schema migration required — uses baseline tables.

**REST Endpoints:**

| Method | Route | Auth | UC |
|--------|-------|------|-----|
| GET | `/api/events` | Anonymous (Published/Ongoing/Completed only) | UC04 |
| GET | `/api/events/{id}` | Anonymous (filtered) | UC04 |
| POST | `/api/events` | Staff, Admin | UC11 |
| PUT | `/api/events/{id}` | Staff, Admin | UC11 |
| POST | `/api/events/{id}/publish` | Staff, Admin | UC12 |
| POST | `/api/events/{id}/start` | Staff, Admin | UC12 |
| POST | `/api/events/{id}/complete` | Staff, Admin | UC12 |
| POST | `/api/events/{id}/cancel` | Staff, Admin | UC12 |
| GET | `/api/reports/event/{id}` | Staff, Admin | UC14 |
| GET | `/api/reports/events` | Staff, Admin | UC14 |
| GET | `/api/reports/overview` | Staff, Admin | UC14 |
| GET | `/odata/Events` | Staff, Admin | OData |

**AvailableSlots contract:**

```
AvailableSlots = Capacity - COUNT(Registrations WHERE Status != "Cancelled")
```

Computed server-side in `EventService`, `ReportService`, and OData projection.

**Business Rules (TV2):**

- **BR-E-01**: Valid status transitions only (Draft→Published→Ongoing→Completed; Cancel from Draft/Published/Ongoing)
- **BR-E-02**: Staff cannot edit Completed/Cancelled events; Admin can
- **BR-E-03**: Capacity ≥ active registration count on update
- **BR-E-04**: EndTime > StartTime; RegistrationDeadline ≤ StartTime

**MVC WebClient:**

| Route | Role | Description |
|-------|------|-------------|
| `/Events` | All | Browse, search, filter |
| `/Events/Details/{id}` | All | Event detail + AvailableSlots |
| `/Events/Create`, `/Events/Edit/{id}` | Staff, Admin | UC11 |
| `/Events/Manage/{id}` | Staff, Admin | UC12 workflow |
| `/Reports` | Staff, Admin | Dashboard |
| `/Reports/Event/{id}` | Staff, Admin | Per-event report |

**TV3 dependency:** Registration/Feedback APIs not implemented by TV2. Reports and AvailableSlots read `Registrations`/`Feedbacks` tables directly — metrics update when TV3 seeds real data.

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
| Events | CRUD + workflow `/api/events/*` |
| Reports | `/api/reports/*` (Staff, Admin) |
| OData | `GET /odata/Events` (Staff, Admin) |

## Business Rules Implemented

- **BR-G-01**: Unique email on register/create user
- **BR-G-02**: `/api/account/me` scoped to authenticated user
- **BR-G-03**: PBKDF2 password hashing (100k iterations, SHA256)
- **BR-G-04**: Role-based authorization policies
- **BR-E-05**: Block delete Location/Organizer when referenced by Events
- **BR-E-01 → BR-E-04**: Event lifecycle rules (see TV2 section above)

## Running Locally

```bash
# Terminal 1 — API
dotnet run --project EventApi

# Terminal 2 — WebClient
dotnet run --project EventRegistration.WebClient
```

- API Swagger: https://localhost:7026/swagger
- WebClient: https://localhost:7150
