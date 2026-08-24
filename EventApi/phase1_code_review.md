# Phase 1 – Giải Thích Toàn Bộ Luồng Code
## Dự Án: EventRegistration (PRN232 – BL5)

---

## 1. Tổng Quan Kiến Trúc

Hệ thống gồm **3 project** chạy song song:

| Project | Vai trò | Port |
|---|---|---|
| `EventApi` | ASP.NET Core Web API – backend chính | https://localhost:7xxx |
| `EventRegistration.WebClient` | ASP.NET Core MVC – giao diện web | https://localhost:7150 |
| `GrpcNotificationService` | gRPC server – gửi thông báo đăng ký | http://localhost:5090 |

### Luồng giao tiếp tổng quát

```
Browser
  │  HTTP (form submit / link)
  ▼
WebClient (MVC)
  │  HTTP + JWT Bearer (EventApiClient)
  ▼
EventApi (REST API)
  │  EF Core
  ▼
SQL Server Database
  │  gRPC (khi có đăng ký sự kiện)
  ▼
GrpcNotificationService
```

### Kiến trúc phân lớp của EventApi

```
EventApi/
├── Domain/          ← Entities, Enums, Common (Result pattern)
├── Application/     ← DTOs, Interfaces, Services, Mapping, OData
├── Infrastructure/  ← DbContext, UnitOfWork, Repository, Security (JWT, Password)
└── Controllers/     ← HTTP endpoints
```

---

## 2. Domain Layer – Trái Tim Của Ứng Dụng

### 2.1. Entities (các bảng trong database)

#### `User`
```csharp
public class User {
    public int Id { get; set; }
    public string Email { get; set; }         // unique index
    public string PasswordHash { get; set; }  // hash PBKDF2-SHA256
    public string PasswordSalt { get; set; }  // salt ngẫu nhiên 16 bytes
    public string FullName { get; set; }
    public string? StudentCode { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }        // enum: Student, Staff, Admin
    public bool IsActive { get; set; } = true;
    // Navigation
    public ICollection<Event> Events { get; set; }         // events do user tạo
    public ICollection<Registration> Registrations { get; set; }
    public ICollection<Feedback> Feedbacks { get; set; }
}
```
> **Tại sao có PasswordHash + PasswordSalt riêng?** → Dùng PBKDF2 với salt ngẫu nhiên, không dùng BCrypt. Lưu riêng để khi verify chỉ cần lấy salt ra derive lại key rồi so sánh bằng FixedTimeEquals (chống timing attack).

#### `Event`
```csharp
public class Event {
    public int Id { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = "Draft";  // lưu string, không phải enum
    public int LocationId { get; set; }
    public int OrganizerId { get; set; }
    public int CreatedById { get; set; }
    // Navigation
    public Location Location { get; set; }
    public Organizer Organizer { get; set; }
    public User CreatedBy { get; set; }
    public ICollection<Registration> Registrations { get; set; }
    public ICollection<Feedback> Feedbacks { get; set; }
}
```
> **Tại sao Status lưu string?** → Linh hoạt với OData query filter, dễ so sánh trong LINQ (`e.Status == "Published"`). Nếu dùng enum phải convert liên tục.

#### `Registration`
```csharp
public class Registration {
    public int Id { get; set; }
    public int EventId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = "Registered";
    // Các giá trị: "Registered", "Attended", "NoShow", "Cancelled"
    public DateTime RegisteredAt { get; set; }
    public DateTime? CheckedInAt { get; set; }   // null nếu chưa check-in
    public DateTime? CancelledAt { get; set; }   // null nếu chưa cancel
}
```
> **Unique index trên (EventId, StudentId)** → Đảm bảo 1 student chỉ đăng ký 1 lần 1 event. Nếu đăng ký lại sau khi cancel → reuse record cũ, không tạo mới.

#### `Location` & `Organizer`
Danh mục đơn giản. `IsActive` cho phép soft-delete (ẩn thay vì xóa).

#### `Feedback`
```csharp
public class Feedback {
    public int EventId { get; set; }
    public int StudentId { get; set; }
    public int Rating { get; set; }    // 1–5
    public string? Comment { get; set; }
    // Unique index (EventId, StudentId) → mỗi student 1 feedback / event
}
```

### 2.2. Enums

```csharp
public enum EventStatus { Draft, Published, Ongoing, Completed, Cancelled }
public enum UserRole    { Student, Staff, Admin }
```

### 2.3. `Result<T>` – Pattern xử lý lỗi

```csharp
public class Result<T> {
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; } = 200;

    public static Result<T> Success(T data, int statusCode = 200) => ...
    public static Result<T> Fail(string error, int statusCode = 400) => ...
}
public class PagedResult<T> {
    public IEnumerable<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

> **Tại sao dùng Result pattern?** → Thay vì throw exception ở service layer, ta trả về `Result<T>`. Controller đọc `IsSuccess` để quyết định trả về HTTP 200, 400, 404, 409... Cách này rõ ràng hơn, không bị unexpected exception làm crash pipeline.

---

## 3. Infrastructure Layer

### 3.1. `AppDbContext` (EF Core)

```csharp
public class AppDbContext : DbContext {
    public DbSet<User> Users => Set<User>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Organizer> Organizers => Set<Organizer>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        // User: Email unique, Role lưu string ("Student","Staff","Admin")
        // Event: FK đến Location, Organizer, CreatedBy(User)
        // Registration: Unique(EventId, StudentId)
        // Feedback: Unique(EventId, StudentId)
    }
}
```

> **Lưu ý:** `entity.Property(e => e.Role).HasConversion<string>()` → enum `UserRole` sẽ được lưu vào DB dưới dạng string "Student"/"Staff"/"Admin", không phải số 0/1/2.

### 3.2. Generic Repository + Unit of Work

#### `IGenericRepository<T>`
```csharp
public interface IGenericRepository<T> {
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    IQueryable<T> Query();          // trả về IQueryable để service tự chain filter/sort
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
}
```

#### `UnitOfWork`
```csharp
public class UnitOfWork : IUnitOfWork {
    private readonly Dictionary<Type, object> _repositories = new();

    public IGenericRepository<T> Repository<T>() {
        // Lazy-create: nếu chưa có thì new GenericRepository<T>
        // Cache lại trong dictionary để tái sử dụng
    }
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}
```

> **Tại sao dùng UoW?** → Tất cả thao tác trong một request dùng chung 1 `DbContext` (do DI Scoped). `SaveChangesAsync()` commit tất cả thay đổi trong 1 transaction. Ví dụ: tạo User xong mới save, không save từng bước.

### 3.3. Security

#### `PasswordHasher` (PBKDF2)
```csharp
public static class PasswordHasher {
    private const int SaltSize = 16;       // 16 bytes salt
    private const int KeySize = 32;        // 32 bytes key output
    private const int Iterations = 100_000; // 100k vòng lặp

    public static (string hash, string salt) HashPassword(string password) {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, SHA256, KeySize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool VerifyPassword(string password, string hash, string salt) {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = DeriveKey(password, saltBytes);
        var storedHash = Convert.FromBase64String(hash);
        return CryptographicOperations.FixedTimeEquals(hashBytes, storedHash); // chống timing attack
    }
}
```

#### `JwtTokenService`
```csharp
public class JwtTokenService(IConfiguration configuration) {
    public LoginResponseDto GenerateToken(User user, UserDto userDto) {
        // Đọc Jwt:Key, Jwt:Issuer, Jwt:Audience, Jwt:ExpireMinutes từ appsettings
        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),   // userId
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())              // "Student"/"Staff"/"Admin"
        };
        // Ký bằng HMAC-SHA256
        // Trả về LoginResponseDto { Token, ExpiresAt, User }
    }
}
```

> **Claims quan trọng:**
> - `Sub` = userId → dùng để lấy `GetCurrentUserId()` trong controller
> - `ClaimTypes.Role` = role string → dùng cho `[Authorize(Roles="Staff,Admin")]`

---

## 4. Application Layer

### 4.1. Service Interfaces (`IServices.cs`)

Toàn bộ business logic nằm sau interface:

```
IAuthService       → Register, Login
IAccountService    → GetMe, UpdateMe, ChangePassword (user tự quản lý profile)
IUserService       → CRUD user (Admin/Staff quản lý)
ILocationService   → CRUD địa điểm
IOrganizerService  → CRUD ban tổ chức
IEventService      → CRUD sự kiện + chuyển status
IRegistrationService → Đăng ký, hủy, check-in, mark no-show
IFeedbackService   → Gửi và xem feedback
IReportService     → Báo cáo thống kê
```

### 4.2. Dependency Injection (Program.cs)

```csharp
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IOrganizerService, OrganizerService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddAutoMapper(typeof(MappingProfile));
```

> **Scoped** = mỗi HTTP request tạo 1 instance mới, dùng chung trong suốt request đó.

### 4.3. AutoMapper (`MappingProfile`)

```csharp
CreateMap<User, UserDto>()
    .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString())); // enum → string

CreateMap<Location, LocationDto>();
CreateMap<Organizer, OrganizerDto>();

CreateMap<Event, EventDetailDto>()
    .ForMember(d => d.RegisteredCount, opt => opt.Ignore())  // tính thủ công sau
    .ForMember(d => d.AvailableSlots, opt => opt.Ignore())   // tính thủ công sau
    .ForMember(d => d.Location, opt => opt.MapFrom(s => s.Location))
    .ForMember(d => d.Organizer, opt => opt.MapFrom(s => s.Organizer));

CreateMap<Location, LocationSummaryDto>();
CreateMap<Organizer, OrganizerSummaryDto>();
```

> `RegisteredCount` và `AvailableSlots` bị Ignore trong AutoMapper vì phải tính dựa trên Registrations collection sau khi map.

### 4.4. OData EDM Model

```csharp
public static IEdmModel GetEdmModel() {
    var builder = new ODataConventionModelBuilder();
    builder.EntitySet<EventODataDto>("Events");
    builder.EntitySet<RegistrationODataDto>("Registrations");
    return builder.GetEdmModel();
}
```

**Cấu hình OData trong Program.cs:**
```csharp
.AddOData(options => options
    .Select().Filter().OrderBy().Count().SetMaxTop(100)
    .AddRouteComponents("odata", ODataEdmModel.GetEdmModel()));
```

> OData cho phép client query như: `GET /odata/Events?$filter=Status eq 'Published'&$select=Id,Title&$top=5&$orderby=StartTime`

---

## 5. Business Logic Services

### 5.1. AuthService – Đăng Ký & Đăng Nhập

#### Luồng `RegisterAsync`
```
1. Kiểm tra email đã tồn tại chưa → nếu có: Fail(409)
2. HashPassword(password) → (hash, salt)
3. Tạo User mới với Role = Student (mặc định)
4. AddAsync → SaveChangesAsync
5. Map User → UserDto, trả về Success(201)
```

#### Luồng `LoginAsync`
```
1. Tìm user theo email (Query().FirstOrDefaultAsync)
2. Nếu không tìm thấy HOẶC VerifyPassword sai → Fail(401)
3. Nếu IsActive = false → Fail(403)
4. Map User → UserDto
5. JwtTokenService.GenerateToken(user, userDto) → LoginResponseDto{Token, ExpiresAt, User}
6. Trả về Success(200)
```

### 5.2. EventWorkflow – Quản Lý Vòng Đời Sự Kiện

Đây là class static quan trọng, điều phối **toàn bộ logic trạng thái** của Event.

#### Các trạng thái hợp lệ:
```
Draft → Published → Ongoing → Completed
  └──────────────────────────→ Cancelled
```

#### `PublicStatuses` – trạng thái Student có thể thấy:
```csharp
public static readonly string[] PublicStatuses = ["Published", "Ongoing", "Completed"];
// Draft và Cancelled chỉ Staff/Admin mới thấy
```

#### `ResolveStatusByTime` – tự động đẩy trạng thái theo thời gian:
```
Nếu status = Draft   → giữ nguyên Draft (không tự publish)
Nếu status = Cancelled → giữ nguyên Cancelled
Nếu status = Completed → giữ nguyên Completed (sticky)
Nếu now >= EndTime   → Completed
Nếu now >= StartTime → Ongoing
Nếu đang Ongoing     → giữ Ongoing (không lùi về Published)
Còn lại              → Published
```

> **Điểm quan trọng:** Draft KHÔNG tự chuyển thành Published. Staff phải chủ động gọi `/api/events/{id}/publish`. Đây là thiết kế có chủ đích.

#### `ValidateTransition` – kiểm tra chuyển status hợp lệ:
```csharp
(Draft, Published)    → OK
(Published, Ongoing)  → OK
(Ongoing, Completed)  → OK
(Draft, Cancelled)    → OK
(Published, Cancelled)→ OK
(Ongoing, Cancelled)  → OK
_ → Fail(409, "Cannot change status from X to Y")
```

#### `ValidateSchedule` – validate khi tạo/sửa event:
```
EndTime > StartTime                    → OK
RegistrationDeadline <= StartTime      → OK
(Deadline KHÔNG được sau StartTime)
```

#### `SyncAllAsync` – đồng bộ trạng thái toàn bộ events:
```csharp
// Gọi ApplyResolvedStatus cho mọi event
// Nếu có event nào thay đổi status → SaveChangesAsync
```

> Được gọi mỗi lần `GetAllAsync` để đảm bảo status luôn đúng theo thời gian thực.

### 5.3. EventService – CRUD Sự Kiện

#### `GetAllAsync(query, userRole)`
```
1. SyncEventStatusesAsync() → cập nhật status theo thời gian thực
2. Build query với Include(Location, Organizer)
3. Nếu không phải Staff/Admin → filter chỉ PublicStatuses
4. Áp dụng filter: status, search (title/description), startDate, endDate
5. Sort theo: title/status/availableslots/starttime/createdat
6. Đếm tổng, skip-take cho phân trang
7. Project sang EventListItemDto (bao gồm RegisteredCount tính inline)
```

> **Tại sao dùng Select projection thay vì AutoMapper?** → Projection chạy trên SQL Server (translate sang SQL), chỉ lấy đúng cột cần. AutoMapper sẽ load toàn bộ entity rồi map ở client-side.

#### `GetByIdAsync(id, userRole)`
```
1. Include(Location, Organizer, Registrations)
2. SyncEventStatusAsync(evt) → cập nhật status nếu cần
3. Student không thấy Draft/Cancelled → Fail(404)
4. MapToDetailDto → tính RegisteredCount, AvailableSlots
```

#### `CreateAsync(request, userId)`
```
1. ValidateSchedule: EndTime > StartTime, Deadline <= StartTime
2. Capacity > 0
3. Kiểm tra LocationId tồn tại
4. Kiểm tra OrganizerId tồn tại
5. Tạo Event với Status = "Draft"
6. SaveChangesAsync
7. Gọi GetByIdAsync để lấy đầy đủ navigation properties, trả về 201
```

#### `UpdateAsync(id, request, userRole)`
```
1. Tìm event (include Registrations để đếm active count)
2. SyncEventStatus
3. Completed/Cancelled event → chỉ Admin mới được sửa
4. ValidateSchedule
5. Capacity không được < số active registrations hiện tại
6. Kiểm tra Location, Organizer
7. Cập nhật fields
8. ApplyResolvedStatus (tự động điều chỉnh status sau khi sửa thời gian)
9. SaveChangesAsync
```

#### `ChangeStatusAsync(id, targetStatus, userRole)`
```
1. Chỉ Staff/Admin được gọi
2. Tìm event
3. SyncEventStatus
4. ValidateTransition(current, target) → kiểm tra transition hợp lệ
5. Gán status mới
6. ApplyResolvedStatus (re-resolve nếu thời gian đã qua)
7. SaveChangesAsync
```

### 5.4. RegistrationService – Đăng Ký Sự Kiện

#### `RegisterAsync(studentId, request)`
```
1. Tìm event (include Registrations)
2. SyncEventStatus
3. Event bị Cancelled → Fail
4. Event không phải Published → Fail ("not open for registration")
5. Event đã started (StartTime <= now) → Fail
6. RegistrationDeadline < now → Fail
7. Đếm active registrations >= Capacity → Fail ("No available slots")
8. Tìm existing registration cùng (EventId, StudentId):
   - Nếu tồn tại và status != Cancelled → Fail ("already registered")
   - Nếu tồn tại và đã Cancelled → reuse record (reset về Registered, xóa timestamps)
   - Nếu chưa tồn tại → tạo mới
9. SaveChangesAsync
10. SendRegistrationNotificationAsync (gRPC, fire-and-forget, lỗi không throw)
11. Trả về RegistrationDto
```

#### `CancelAsync(registrationId, userId, userRole)`
```
1. Tìm registration (include Event, Student)
2. Kiểm tra quyền: studentId == userId HOẶC là Staff/Admin
3. Status đã Cancelled → Fail
4. Status không phải Registered → Fail ("Only registered ticket can be cancelled")
5. Event.StartTime <= now → Fail ("cannot cancel after event started")
6. Đặt Status = "Cancelled", CancelledAt = now
7. SaveChangesAsync
```

#### `CheckInAsync(registrationId, userRole, request)`
```
1. Chỉ Staff/Admin
2. Validate request.Status ∈ {"Attended", "NoShow"}
3. Tìm registration (include Event)
4. SyncEventStatus
5. Nếu "Attended":
   - Event phải là Published hoặc Ongoing
   - Status = "Attended", CheckedInAt = now
6. Nếu "NoShow":
   - Event.EndTime phải <= now (event phải kết thúc)
   - Status = "NoShow", CheckedInAt = null
```

#### `MarkNoShowAsync(eventId, userRole)` – batch no-show
```
1. Chỉ Staff/Admin
2. Lấy tất cả registrations của event
3. Event phải đã kết thúc (EndTime <= now)
4. Tất cả registration còn status "Registered" → đặt "NoShow"
5. SaveChangesAsync
```

#### Thông báo gRPC `SendRegistrationNotificationAsync`
```
try {
    lấy thông tin user
    tạo RegistrationNotificationRequest { EventId, EventTitle, UserName, UserEmail }
    kết nối GrpcChannel.ForAddress("http://localhost:5090")
    gọi client.SendRegistrationConfirmationAsync(notification)
    nếu reply.Success = false → LogWarning
} catch (Exception) {
    LogWarning("gRPC not available; registration still succeeded")
    // KHÔNG throw, không rollback → đăng ký vẫn thành công
}
```

### 5.5. FeedbackService

#### `SubmitAsync(studentId, request)`
```
1. Rating phải từ 1–5
2. Event phải tồn tại
3. Kiểm tra student đã có registration với Status = "Attended" → Fail nếu chưa attended
4. Kiểm tra đã submit feedback cho event này chưa → Fail nếu đã có
5. Tạo Feedback, SaveChangesAsync
```

> **Điều kiện quan trọng:** Chỉ những ai thực sự **attended** (check-in thành công) mới được gửi feedback.

### 5.6. ReportService

#### `GetEventReportAsync(eventId)`
Trả về `EventReportDto` với:
- `RegisteredCount` = số registrations status != "Cancelled"
- `CheckedInCount` = số registrations status != "Cancelled" AND CheckedInAt != null
- `AttendanceRate` = CheckedInCount / RegisteredCount
- `FillRate` = RegisteredCount / Capacity
- `AverageRating` = trung bình Rating của Feedbacks (null nếu chưa có feedback)

#### `GetOverviewAsync()`
Tổng hợp toàn hệ thống:
- `EventsByStatus` = Dictionary<string, int> đếm event theo từng status
- `OverallAttendanceRate`, `OverallFillRate`
- `OverallAverageRating`

### 5.7. UserService & AccountService

**Khác biệt:**
- `AccountService`: user tự quản lý profile của mình (`GetMe`, `UpdateMe`, `ChangePassword`)
- `UserService`: Admin/Staff quản lý tất cả users (CRUD, soft-deactivate)

**Soft-deactivate** trong `DeactivateAsync`:
```csharp
user.IsActive = false;  // không xóa, chỉ đánh dấu inactive
// Login sẽ bị Fail(403) vì kiểm tra !user.IsActive
```

---

## 6. Controllers Layer

### 6.1. `ApiControllerBase` – Base class chung

```csharp
[ApiController]
public abstract class ApiControllerBase : ControllerBase {
    protected IActionResult ToActionResult<T>(Result<T> result) {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, result.Data);
        return StatusCode(result.StatusCode, new { error = result.Error });
    }

    protected int GetCurrentUserId() {
        // Lấy claim "sub" từ JWT token đã decode
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst("sub");
        return int.Parse(claim!.Value);
    }
}
```

> `ToActionResult` là pattern để controller không cần if-else nhiều: mọi service trả về `Result<T>`, controller chỉ cần gọi `ToActionResult(await service.XxxAsync(...))`.

### 6.2. `AuthController`

```
POST /api/auth/register  [AllowAnonymous] → AuthService.RegisterAsync
POST /api/auth/login     [AllowAnonymous] → AuthService.LoginAsync
```

### 6.3. `EventsController`

```
GET    /api/events          [AllowAnonymous]          → GetAllAsync (filter, sort, paging)
GET    /api/events/{id}     [AllowAnonymous]          → GetByIdAsync
POST   /api/events          [Authorize(Staff,Admin)]  → CreateAsync
PUT    /api/events/{id}     [Authorize(Staff,Admin)]  → UpdateAsync
POST   /api/events/{id}/publish  [Authorize(Staff,Admin)] → ChangeStatusAsync("Published")
POST   /api/events/{id}/start    [Authorize(Staff,Admin)] → ChangeStatusAsync("Ongoing")
POST   /api/events/{id}/complete [Authorize(Staff,Admin)] → ChangeStatusAsync("Completed")
POST   /api/events/{id}/cancel   [Authorize(Staff,Admin)] → ChangeStatusAsync("Cancelled")
```

> `GetCurrentUserRole()` trong EventsController trả về `null` nếu user chưa login → EventService biết đây là anonymous user, chỉ hiển thị PublicStatuses.

### 6.4. `RegistrationsController`

```
GET  /api/registrations/me                    [Authorize]           → GetMyRegistrationsAsync
GET  /api/registrations/events/{eventId}      [Authorize(Staff,Admin)] → GetEventRegistrationsAsync
POST /api/registrations/event/{eventId}/mark-noshow [Authorize(Staff,Admin)] → MarkNoShowAsync
POST /api/registrations                       [Authorize]           → RegisterAsync
POST /api/registrations/{id}/cancel          [Authorize]           → CancelAsync
POST /api/registrations/{id}/checkin         [Authorize(Staff,Admin)] → CheckInAsync
```

### 6.5. Cấu hình JWT trong Program.cs

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.MapInboundClaims = false;  // QUAN TRỌNG: không tự remap claim names
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,          // kiểm tra expiry
            ValidateIssuerSigningKey = true,
            ValidIssuer = ...,
            ValidAudience = ...,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("StaffOrAdmin", p => p.RequireRole("Staff", "Admin"));
});
```

> **`MapInboundClaims = false`** rất quan trọng! Nếu không set, ASP.NET sẽ rename claim `sub` → `nameidentifier` và `role` → chuỗi XML dài. Khi false, claims giữ nguyên tên gốc từ JWT.

### 6.6. CORS

```csharp
options.AddPolicy("WebClient", policy =>
    policy.WithOrigins("https://localhost:7150", "http://localhost:5150")
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials());
```

> Chỉ cho phép WebClient domain gọi API. `AllowCredentials()` cần thiết khi WebClient gửi cookie/session kèm request.

---

## 7. WebClient (MVC)

### 7.1. Cấu hình (`Program.cs`)

```csharp
// Session để lưu JWT token phía client
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Typed HttpClient
builder.Services.AddHttpClient<EventApiClient>(client => {
    client.BaseAddress = new Uri(configuration["ApiBaseUrl"]);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler {
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    // Bỏ qua SSL cert validation (dùng trong dev với localhost)
});
```

### 7.2. `EventApiClient` – HTTP Client Wrapper

**Đây là lớp trung gian duy nhất để WebClient gọi API.** Mọi controller WebClient đều inject `EventApiClient`.

```csharp
private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body = null) {
    var request = new HttpRequestMessage(method, url);
    // Tự động lấy JWT từ Session và gắn vào Authorization header
    var token = _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
    if (!string.IsNullOrEmpty(token))
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body is not null)
        request.Content = JsonContent.Create(body);
    return request;
}

// GET → T? (null nếu API trả lỗi)
public async Task<T?> GetAsync<T>(string url)

// POST/PUT → (bool Success, T? Data, string? Error, int StatusCode)
public async Task<(bool Success, T? Data, string? Error, int StatusCode)> PostAsync<T>(string url, object body)
public async Task<(bool Success, T? Data, string? Error, int StatusCode)> PutAsync<T>(string url, object body)

// DELETE → (bool Success, string? Error, int StatusCode)
public async Task<(bool Success, string? Error, int StatusCode)> DeleteAsync(string url)
```

> **Tại sao Session thay vì Cookie?** → Token được lưu server-side trong session (in-memory), browser chỉ nhận session cookie (HttpOnly). Bảo mật hơn vì JS không đọc được token.

### 7.3. Luồng Login trên WebClient

```
1. User submit form Login (Email, Password)
2. AuthController.Login(model) gọi _api.PostAsync<LoginResponseViewModel>("/api/auth/login", model)
3. Nếu thất bại → ModelState.AddModelError, return View(model)
4. Nếu thành công:
   - Session["JwtToken"] = result.Data.Token
   - Session["UserName"] = result.Data.User.FullName
   - Session["UserRole"] = result.Data.User.Role
5. RedirectToAction("Index", "Home")
```

### 7.4. Kiểm tra quyền trên WebClient

WebClient không dùng `[Authorize]` attribute vì nó không có Authentication middleware. Thay vào đó:

```csharp
// Trong EventsController (WebClient)
private bool CanManage =>
    HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

// Trong RegistrationsController (WebClient)
private bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("JwtToken"));
private bool CanCheckIn => HttpContext.Session.GetString("UserRole") is "Staff" or "Admin";

// Kiểm tra đầu mỗi action cần bảo vệ:
if (!CanManage) return RedirectToAction("Login", "Auth");
```

> **Lưu ý:** Security thực sự nằm ở **EventApi**. WebClient chỉ hide UI. Nếu ai đó gọi thẳng API mà không có JWT → API sẽ từ chối (401/403).

### 7.5. Luồng tạo Event trên WebClient

```
1. GET /Events/Create → EventsController.Create()
   - Kiểm tra CanManage
   - Gọi API lấy danh sách Locations và Organizers
   - Trả về View với CreateEventViewModel

2. POST /Events/Create → EventsController.Create(model)
   - Gọi _api.PostAsync<EventDetailViewModel>("/api/events", {...})
   - Nếu thất bại → ModelState.AddModelError, rebuild lists, return View
   - Nếu thành công → TempData["Success"] = "...", Redirect sang Details
```

### 7.6. Luồng đăng ký Event trên WebClient

```
1. Student xem Details của event
2. Submit form Register (post eventId)
3. RegistrationsController.Register(eventId)
   - Kiểm tra IsLoggedIn
   - Gọi _api.PostAsync<RegistrationViewModel>("/api/registrations", new { eventId })
   - Nếu lỗi → TempData["Error"], redirect về Details
   - Nếu thành công → TempData["Success"], redirect về Index (danh sách registrations)
```

---

## 8. GrpcNotificationService

### Proto Definition (`notification.proto`)
```protobuf
service NotificationService {
  rpc SendRegistrationConfirmation (RegistrationNotificationRequest)
    returns (RegistrationNotificationReply);
}

message RegistrationNotificationRequest {
  int32 eventId = 1;
  string eventTitle = 2;
  string userName = 3;
  string userEmail = 4;
}

message RegistrationNotificationReply {
  bool success = 1;
  string message = 2;
}
```

### `NotificationService.cs`
```csharp
public override Task<RegistrationNotificationReply> SendRegistrationConfirmation(
    RegistrationNotificationRequest request,
    ServerCallContext context)
{
    _logger.LogInformation(
        "Registration notification: {UserName} ({Email}) registered for event {EventId} - {EventTitle}",
        request.UserName, request.UserEmail, request.EventId, request.EventTitle);

    return Task.FromResult(new RegistrationNotificationReply {
        Success = true,
        Message = $"Registration notification sent for {request.EventTitle}."
    });
}
```

> **Phase 1 chỉ log.** Phase 2 sẽ tích hợp gửi email thực sự. Nhưng kiến trúc gRPC đã sẵn sàng.

---

## 9. Luồng Code Hoàn Chỉnh – Ví Dụ Đăng Ký Sự Kiện

```
Browser (Student)
│
│ POST /Registrations/Register  body: { eventId: 5 }
▼
WebClient.RegistrationsController.Register(eventId=5)
│ Kiểm tra Session["JwtToken"] != null
│
│ EventApiClient.PostAsync<RegistrationViewModel>(
│     "/api/registrations", new { eventId = 5 })
│   → CreateRequest(POST, url, body)
│   → Lấy JWT từ Session → gắn Authorization: Bearer xxx
▼
EventApi.RegistrationsController.Register([FromBody] { EventId: 5 })
│ [Authorize] → ASP.NET validate JWT
│ GetCurrentUserId() → parse claim "sub" = 12
│
│ _registrationService.RegisterAsync(studentId=12, { EventId=5 })
▼
RegistrationService.RegisterAsync(12, {EventId=5})
│ Tìm Event id=5 (include Registrations)
│ SyncEventStatus → ApplyResolvedStatus → status vẫn "Published"
│ Kiểm tra: không Cancelled, là Published, chưa started, deadline chưa qua
│ Đếm active regs < Capacity → còn slot
│ Tìm existing (EventId=5, StudentId=12) → không có
│ Tạo Registration { EventId=5, StudentId=12, Status="Registered", RegisteredAt=now }
│ context.Registrations.Add(registration)
│ await context.SaveChangesAsync()
│
│ SendRegistrationNotificationAsync(5, "Tech Talk", 12)
│   → Lấy user (userId=12) → name, email
│   → GrpcChannel.ForAddress("http://localhost:5090")
│   → client.SendRegistrationConfirmationAsync({EventId=5, ...})
▼
GrpcNotificationService.NotificationService.SendRegistrationConfirmation(...)
│ _logger.LogInformation("Registration notification: ...")
│ return { Success=true, Message="..." }
▼
RegistrationService → trả về Result<RegistrationDto>.Success(dto)
▼
RegistrationsController → ToActionResult(result) → HTTP 200 + JSON
▼
EventApiClient.ParseResponse → (true, RegistrationViewModel, null, 200)
▼
WebClient.RegistrationsController.Register
│ TempData["Success"] = "Event registered successfully."
│ RedirectToAction("Index") → danh sách registrations của student
```

---

## 10. Bảng Tóm Tắt API Endpoints Phase 1

| Method | Endpoint | Auth | Mô tả |
|---|---|---|---|
| POST | `/api/auth/register` | Anonymous | Đăng ký tài khoản Student |
| POST | `/api/auth/login` | Anonymous | Đăng nhập, nhận JWT |
| GET | `/api/account/me` | Any | Xem profile bản thân |
| PUT | `/api/account/me` | Any | Cập nhật profile |
| PUT | `/api/account/me/password` | Any | Đổi mật khẩu |
| GET | `/api/users` | Staff/Admin | Danh sách users + tìm kiếm + phân trang |
| POST | `/api/users` | Admin | Tạo user mới |
| PUT | `/api/users/{id}` | Admin | Sửa user |
| DELETE | `/api/users/{id}` | Admin | Deactivate user |
| GET | `/api/locations` | Any | Danh sách địa điểm |
| POST | `/api/locations` | Staff/Admin | Tạo địa điểm |
| PUT | `/api/locations/{id}` | Staff/Admin | Sửa địa điểm |
| DELETE | `/api/locations/{id}` | Staff/Admin | Xóa địa điểm |
| GET | `/api/organizers` | Any | Danh sách ban tổ chức |
| POST | `/api/organizers` | Staff/Admin | Tạo BTC |
| GET | `/api/events` | Anonymous/Auth | Danh sách events (filter, sort, paging) |
| GET | `/api/events/{id}` | Anonymous/Auth | Chi tiết event |
| POST | `/api/events` | Staff/Admin | Tạo event (→ Draft) |
| PUT | `/api/events/{id}` | Staff/Admin | Sửa event |
| POST | `/api/events/{id}/publish` | Staff/Admin | Chuyển → Published |
| POST | `/api/events/{id}/start` | Staff/Admin | Chuyển → Ongoing |
| POST | `/api/events/{id}/complete` | Staff/Admin | Chuyển → Completed |
| POST | `/api/events/{id}/cancel` | Staff/Admin | Chuyển → Cancelled |
| GET | `/api/registrations/me` | Any | Đăng ký của bản thân |
| POST | `/api/registrations` | Any | Đăng ký event |
| POST | `/api/registrations/{id}/cancel` | Any/Staff/Admin | Hủy đăng ký |
| GET | `/api/registrations/events/{id}` | Staff/Admin | Danh sách đăng ký theo event |
| POST | `/api/registrations/{id}/checkin` | Staff/Admin | Check-in / NoShow |
| POST | `/api/registrations/event/{id}/mark-noshow` | Staff/Admin | Batch NoShow |
| GET | `/api/feedbacks/event/{id}` | Any | Feedbacks của event |
| POST | `/api/feedbacks` | Student | Gửi feedback |
| GET | `/api/reports/overview` | Staff/Admin | Báo cáo tổng quan |
| GET | `/api/reports/events` | Staff/Admin | Báo cáo theo từng event |
| GET | `/api/reports/events/{id}` | Staff/Admin | Báo cáo chi tiết 1 event |
| GET | `/odata/Events` | Any | OData query events |
| GET | `/odata/Registrations` | Staff/Admin | OData query registrations |

---

## 11. Các Thiết Kế Đáng Chú Ý

### 1. Result Pattern thay vì Exception
Service không throw, Controller không try-catch. Dùng `Result<T>.Success/Fail` với status code rõ ràng.

### 2. Auto-sync Event Status
Mỗi lần `GetAll`, `GetById`, `Register` → gọi `SyncEventStatus` tự động đẩy Published→Ongoing→Completed theo thời gian. Không cần background job.

### 3. Reuse Registration Record
Khi student đăng ký lại event đã từng cancel → không tạo record mới, reset lại record cũ. Đảm bảo unique constraint `(EventId, StudentId)` không bị vi phạm.

### 4. gRPC Fire-and-forget
Notification gRPC được gọi sau khi SaveChanges thành công. Lỗi gRPC chỉ log warning, không rollback transaction đăng ký.

### 5. Session-based Auth trên WebClient
JWT không lưu trong localStorage (dễ bị XSS). Lưu trong server-side session, browser chỉ có session cookie (HttpOnly).

### 6. Anonymous Event Browsing
`[AllowAnonymous]` cho GET events → khách chưa đăng nhập vẫn xem được danh sách. Nhưng chỉ thấy Published/Ongoing/Completed, không thấy Draft/Cancelled.
