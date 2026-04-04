# Fashion Store — ASP.NET Core Web Application

## Tổng quan

Fashion Store là ứng dụng thương mại điện tử xây dựng bằng **ASP.NET Core 8 MVC**, kết nối **SQL Server** qua Entity Framework Core.

---

## Công nghệ

| Lớp | Công nghệ |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| Database | SQL Server (EF Core) |
| Authentication | ASP.NET Identity |
| ORM | Entity Framework Core |
| Caching | IMemoryCache |
| Session | DistributedMemoryCache + HttpContext.Session |
| UI | Vanilla CSS (modern-ui.css), Font Awesome, Google Fonts (Inter) |
| Charts | Chart.js (Admin Dashboard) |

---

## Kiến trúc

```
Controllers/          → HTTP endpoints, input validation, TempData/ViewBag
Models/                → Entity + Service (business logic) + DTO
Data/                  → ApplicationDbContext, OnModelCreating
wwwroot/               → Static files (CSS, JS, uploads)
Migrations/           → EF Core migrations
AppConstants.cs       → Cache key / Session key dùng chung
Program.cs             → DI container, middleware pipeline, seed data
```

---

## Tính năng chính

### Người dùng
- [x] Đăng ký / Đăng nhập / Đăng xuất
- [x] Quên mật khẩu → Reset qua FullName + Email
- [x] Đổi mật khẩu
- [x] Cập nhật hồ sơ (FullName, Address, DateOfBirth, Avatar)
- [x] Upload avatar (max 5MB, định dạng: JPG/PNG/GIF/WEBP)
- [x] Xem sản phẩm, tìm kiếm, lọc theo danh mục, sắp xếp, phân trang
- [x] Chi tiết sản phẩm + đánh giá + sản phẩm liên quan
- [x] Quick View (AJAX)
- [x] Giỏ hàng (session-based, hỗ trợ Size/Color, kiểm tra tồn kho)
- [x] Mã giảm giá (validate theo thời gian, lượt dùng, đơn tối thiểu)
- [x] Đặt hàng → Checkout (yêu cầu đăng nhập)
- [x] Lịch sử đơn hàng (theo UserId hoặc Email)
- [x] Hủy đơn (khi "Chờ xác nhận" / "Đã xác nhận")
- [x] Xác nhận thanh toán khi shipper giao hàng
- [x] Xóa đơn hàng khỏi lịch sử (soft delete — chỉ khi Hoàn tất / Đã hủy)
- [x] Wishlist (thêm / xóa / toggle bằng AJAX)

### Quản trị (Admin)
- [x] Dashboard: biểu đồ doanh thu 6 tháng (Chart.js), KPI tổng quan
- [x] Quản lý sản phẩm: CRUD, upload ảnh, giữ nguyên Rating/ReviewCount/SoldCount, filter tồn kho thấp
- [x] Quản lý đơn hàng: lọc theo status/search, cập nhật trạng thái, tự động hoàn tồn kho khi hủy/trả
- [x] Quản lý mã giảm giá: CRUD coupon
- [x] Quản lý danh mục: CRUD category, tự tạo slug, cache 30 phút
- [x] Quản lý người dùng: search, phân trang, gán/xóa vai trò, xem đơn hàng

---

## Trạng thái đơn hàng

```
Chờ xác nhận → Đã xác nhận → Giao ĐVVC → Đang giao → Đến nơi → Hoàn tất
                                          ↘ Đã hủy ←───────────────────↗
                                          ↘ Trả hàng ←───────────────────────↗
```

## Trạng thái thanh toán

```
Chưa thanh toán → Chờ thanh toán → Thất bại → Đã thanh toán
```

---

## Quy tắc code

- **Transaction**: `OrderService.CreateOrderAsync` dùng `BeginTransactionAsync`.
- **ExecuteUpdate**: Stock / SoldCount cập nhật bằng `ExecuteUpdateAsync` — không load entity.
- **AsNoTracking**: Tất cả query chỉ-đọc dùng `.AsNoTracking()`.
- **Projection (ProductDto)**: `ProductService` dùng DTO projection tránh tracking entity gốc.
- **Cache**: Danh mục active cache 30 phút qua `IMemoryCache`.
- **Soft delete**: `Order.IsDeletedByUser` — ẩn đơn khỏi view người dùng.
- **CSRF**: Tất cả action POST đều có `[ValidateAntiForgeryToken]`.
- **Hardcoded strings**: Cache key / Session key gom vào `AppConstants.cs`.

---

## Cấu hình

### `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=web1;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### Tài khoản mặc định
```
Email:    admin@fashionstore.com
Password: Admin@123
Vai trò:  Admin
```

### Migration
```bash
dotnet ef migrations add <TênMigration>
dotnet ef database update
```

### Chạy ứng dụng
```bash
dotnet run --project web1
```

### Build & Test
```bash
dotnet build
dotnet test
```

---

## Cấu trúc thư mục uploads

```
wwwroot/uploads/
├── avatars/       ← avatar user
└── products/     ← ảnh sản phẩm
```

---

## Database Tables

| Bảng | Mô tả |
|---|---|
| `Users` | ApplicationUser (Identity) |
| `Roles` | IdentityRole |
| `Products` | Sản phẩm |
| `Categories` | Danh mục |
| `Orders` | Đơn hàng |
| `OrderItems` | Chi tiết đơn hàng |
| `Coupons` | Mã giảm giá |
| `Reviews` | Đánh giá sản phẩm |
| `WishlistItems` | Sản phẩm yêu thích |

---

## Lịch sử cập nhật

| Ngày | Nội dung |
|---|---|
| 25/03/2026 | Hiện đại hóa toàn bộ UI (Glassmorphism, modern-ui.css) |
| 28/03/2026 | Refactor Database, ExecuteUpdate, AsNoTracking, Cache |
| 01/04/2026 | Thêm Wishlist, Upload Avatar, Reset Password, Admin Dashboard Chart.js |