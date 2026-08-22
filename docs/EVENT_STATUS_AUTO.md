# Trạng thái Event

> **Draft → Published** do Staff/Admin bấm Publish.  
> Sau đó hệ thống tự chuyển **Published → Ongoing → Completed** theo `StartTime` / `EndTime` (UTC).  
> `RegistrationDeadline` **không** đổi status — chỉ quyết định student còn đăng ký được hay không.  
> **Cancelled** không bị ghi đè.

## Quy tắc

| Điều kiện | Status | Ai thấy? | Student đăng ký? |
|-----------|--------|----------|------------------|
| Chưa Publish | **Draft** | Chỉ Staff/Admin | Không |
| Đã Publish và `now < StartTime` | **Published** | Mọi user | Có, nếu còn hạn và còn chỗ |
| `StartTime ≤ now < EndTime` | **Ongoing** | Mọi user | Không |
| `now ≥ EndTime` | **Completed** | Mọi user | Không |
| Staff/Admin Cancel | **Cancelled** | Chỉ Staff/Admin | Không |

```
Timeline:  --[Draft]--|----[Published, mở ĐK]----|----[Published, hết hạn]----|----[Ongoing]----|----[Completed]
                      ↑ Publish                   ↑ Hạn ĐK                     ↑ Start           ↑ End
```

## Thao tác thủ công (Manage)

| Action | From → To |
|--------|-----------|
| Publish | Draft → Published |
| Start | Published → Ongoing (tùy chọn; hệ thống cũng tự chuyển khi tới StartTime) |
| Complete | Ongoing → Completed (tùy chọn; hệ thống cũng tự chuyển khi tới EndTime) |
| Cancel | Draft / Published / Ongoing → Cancelled |

## Cách mở đăng ký cho student

1. Tạo event (mặc định **Draft**)
2. Đặt `RegistrationDeadline` **ở tương lai** và `≤ StartTime`
3. Vào **Manage** → **Publish**

## Demo data `[DEMO AUTO]`

Restart EventApi để seed/repair. Tìm trên Events:

| Event | Status mong đợi |
|-------|-----------------|
| Draft — chưa publish | Draft |
| Published — đang mở đăng ký | Published (student đăng ký được) |
| Ongoing — đang diễn ra | Ongoing |
| Completed — đã kết thúc | Completed |

## Tài khoản

| Email | Password |
|-------|----------|
| staff@fpt.edu.vn | Staff@123 |
| admin@fpt.edu.vn | Admin@123 |
| student@fpt.edu.vn | Student@123 |
