# Trạng thái Event — Tự động theo thời gian

> Status **không chỉnh bằng tay** (đã bỏ nút Publish/Start/Complete/Cancel).  
> Hệ thống tính từ 3 mốc thời gian khi tạo/sửa event.

## Quy tắc (UTC)

| Điều kiện | Status | Ai thấy? |
|-----------|--------|----------|
| `now < RegistrationDeadline` | **Draft** | Chỉ Staff/Admin |
| `RegistrationDeadline ≤ now < StartTime` | **Published** | Mọi user |
| `StartTime ≤ now < EndTime` | **Ongoing** | Mọi user |
| `now ≥ EndTime` | **Completed** | Mọi user |

```
Timeline:  ----[Draft]----|----[Published]----|----[Ongoing]----|----[Completed]----
                          ↑ Hạn ĐK            ↑ Start           ↑ End
```

## Cách đổi trạng thái

**Sửa thời gian** trong form Edit (hoặc PUT `/api/events/{id}`), rồi tải lại trang — status tự cập nhật.

Ví dụ muốn event **Published** ngay:
- Đặt `RegistrationDeadline` ≤ thời điểm hiện tại
- Đặt `StartTime` ở tương lai

## Demo data `[DEMO AUTO]`

Restart EventApi để seed (nếu chưa có). Tìm trên Events:

| Event | Status mong đợi |
|-------|-----------------|
| Draft — trước hạn đăng ký | Draft |
| Published — đang mở | Published |
| Ongoing — đang diễn ra | Ongoing |
| Completed — đã kết thúc | Completed |

## Tài khoản

| Email | Password |
|-------|----------|
| staff@fpt.edu.vn | Staff@123 |
| admin@fpt.edu.vn | Admin@123 |
