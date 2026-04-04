# Nike Fashion Store - Premium Modernization Project

Dự án cải tiến và hiện đại hóa cửa hàng thời trang chuẩn phong cách Nike (Nike-inspired) sử dụng công nghệ **C# ASP.NET Core 8.0 MVC**.

## 🚀 Trạng thái Dự án: **Professional & Stable**

Dự án đã hoàn thành giai đoạn nâng cấp UI/UX cao cấp và tối ưu hóa hệ thống vận hành.

---

## 🎨 Hệ thống Thiết kế (Design System)

- **Aesthetics**: Giao diện **Glassmorphism** tối giản, hiện đại với tông màu đen trắng chủ đạo.
- **Typography (Đặc biệt)**: Sử dụng font **Be Vietnam Pro**. Đây là font thiết kế dành riêng cho Tiếng Việt, đảm bảo hiển thị hoàn hảo mọi dấu thanh, không bị lỗi font hay biến dạng ký tự.
- **Responsive**: Toàn bộ website được tối ưu hóa để hiển thị tốt trên mọi thiết bị và mọi mức độ phóng to của trình duyệt (đặc biệt là mức 75%).

## 🛍️ Tính năng Cửa hàng (Storefront)

- **Phân trang Hiệu năng cao**: Hệ thống phân trang 9 sản phẩm/trang được xử lý trực tiếp từ SQL Server (Database level), giúp tốc độ load trang cực nhanh.
- **Duy trì Trạng thái Lọc**: Tự động giữ bộ lọc Danh mục (`Category`), Tìm kiếm (`Search`) và Sắp xếp (`Sort`) khi chuyển trang.
- **Giỏ hàng Động (AJAX Cart)**: 
  - Cập nhật số lượng túi xách trong Header ngay lập tức khi thêm hàng.
  - Áp dụng mã giảm giá và tính lại tổng tiền trực tiếp không cần load lại trang.
- **Quy trình Thanh toán (Premium Checkout)**: 
  - Thiết kế dạng thẻ bo tròn (`Rounded Cards`) tinh tế.
  - Các hình thức thanh toán được thiết kế chuyên nghiệp, dễ bấm và rõ ràng.

## 🛠️ Hệ thống Quản trị (Admin Dashboard)

- **Dashboard Hiện đại**: Giao diện quản lý dạng Glassmorphism tích hợp thống kê.
- **Quản lý Đơn hàng Chặt chẽ**:
  - Theo dõi và cập nhật trạng thái đơn hàng (Chờ xác nhận, Giao hàng, Hoàn tất...).
  - **Order Hardening**: Khóa cập nhật đối với đơn hàng đã hoàn thành hoặc đã hủy để đảm bảo tính an toàn dữ liệu.
- **Fix Layout Chi tiết**: Đã xử lý các lỗi chồng chéo hình ảnh và vỡ Grid trong trang quản lý của Admin.

## 💻 Tech Stack

- **Framework**: C# ASP.NET Core 8.0 MVC.
- **Database**: Entity Framework Core.
- **Client-side**: JavaScript (AJAX), jQuery.
- **UI Framework**: Vanilla CSS (Modern CSS 3), Bootstrap 5 (Grid system).

---

## 🔧 Các lỗi đã xử lý (Resolved Issues)

- [x] Lỗi `RuntimeBinderException` trong hệ thống phân trang.
- [x] Lỗi `InvalidOperationException` do lặp thẻ Section Scripts.
- [x] Lỗi chồng chéo hình ảnh trong trang Admin Order Details.
- [x] Lỗi font chữ Tiếng Việt bị biến dạng (Fixed with Be Vietnam Pro).
- [x] Lỗi icon thông báo giỏ hàng không cập nhật (Fixed with Real-time AJAX badge).

---

© 2026 Modernized Nike Store Project. All Rights Reserved.
