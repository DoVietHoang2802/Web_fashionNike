# Tiến độ triển khai ProductVariant (Size + Color)

> **Ngày cập nhật:** 2026-04-05
> **Trạng thái:** ✅ HOÀN THÀNH

---

## Mục tiêu
Quản lý Size và Color cho sản phẩm (Hybrid - vừa quản lý tồn kho vừa cho khách chọn khi mua)

---

## ✅ ĐÃ LÀM XONG TẤT CẢ

- [x] **Bước 1:** Tạo bảng `ProductVariants` trong SQL Server
- [x] **Bước 2:** Tạo Model `ProductVariant.cs`
- [x] **Bước 3:** Thêm `DbSet<ProductVariant>` vào `ApplicationDbContext.cs`
- [x] **Bước 4:** Cấu hình Entity trong `OnModelCreating`
- [x] **Bước 5:** Tạo `ProductVariantService.cs`
- [x] **Bước 6:** Admin - Thêm actions quản lý biến thể (`ProductVariants`, `ProductVariantCreate`, `ProductVariantEdit`, `ProductVariantDelete`)
- [x] **Bước 7:** Admin - Tạo Views (`Index.cshtml`, `Create.cshtml`, `Edit.cshtml`) + nút "Quản lý biến thể" trong `ProductEdit.cshtml`
- [x] **Bước 8:** Customer - Cập nhật `Details.cshtml` + `ProductsController` load biến thể động (Size/Color buttons, kiểm tra tồn kho, hiệu chỉnh giá)
- [x] **Bước 9:** Cart - Kiểm tra tồn kho theo biến thể (`AddToCart`, `UpdateQuantity`)
- [x] **Bước 10:** Order - Giảm/tăng tồn kho theo biến thể khi đặt hàng/hủy đơn (`CreateOrderAsync`, `UpdateOrderStatusAsync`)
- [x] **Bước 11:** Admin Order - `OrderDetails.cshtml` đã hiển thị Size/Color (có sẵn từ trước)

---

## 📁 Các file đã tạo mới / sửa đổi

### Tạo mới
| File | Mô tả |
|------|--------|
| `Models/ProductVariant.cs` | Entity model cho biến thể |
| `Models/ProductVariantService.cs` | Service xử lý CRUD + tồn kho biến thể |
| `Views/Admin/ProductVariants/Index.cshtml` | Danh sách biến thể của sản phẩm |
| `Views/Admin/ProductVariants/Create.cshtml` | Form thêm biến thể |
| `Views/Admin/ProductVariants/Edit.cshtml` | Form sửa biến thể |

### Sửa đổi
| File | Thay đổi |
|------|----------|
| `Data/ApplicationDbContext.cs` | Thêm `DbSet<ProductVariant>` + cấu hình entity |
| `Program.cs` | Đăng ký `ProductVariantService` vào DI Container |
| `Controllers/ProductsController.cs` | Inject `ProductVariantService`, load biến thể trong `Details` |
| `Controllers/CartController.cs` | Inject `ProductVariantService`, kiểm tra tồn kho biến thể |
| `Models/OrderService.cs` | Inject `ProductVariantService`, giảm/tăng tồn kho biến thể |
| `Controllers/AdminController.cs` | Inject `ProductVariantService`, thêm 4 actions CRUD biến thể |
| `Views/Admin/ProductEdit.cshtml` | Thêm nút "Quản lý biến thể" |
| `Views/Products/Details.cshtml` | Viết lại hoàn toàn phần Size/Color động |

---

## 📝 GHI CHÚ THAY ĐỔI DATA

### Bảng mới: ProductVariants
```sql
CREATE TABLE ProductVariants (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    ProductId      INT NOT NULL,
    Size           NVARCHAR(50) NOT NULL,
    Color          NVARCHAR(100) NOT NULL,
    Stock          INT NOT NULL DEFAULT 0,
    PriceModifier  DECIMAL(18,2) NULL,
    IsActive       BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_ProductVariants_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
);

CREATE UNIQUE INDEX IX_ProductVariants_ProductId_Size_Color
ON ProductVariants(ProductId, Size, Color);
```

---

## ⚠️ LƯU Ý QUAN TRỌNG

1. **Sử dụng bảng mới, không xóa cột Stock cũ** - để đảm bảo backward compatibility
2. **Khi đặt hàng** - giảm tồn kho trên `ProductVariant.Stock` (không giảm `Product.Stock`)
3. **Product.Stock** - được tự động tính lại = SUM(ProductVariant.Stock) mỗi khi biến thể thay đổi
4. **CartItem** - đã có SelectedSize và SelectedColor, dùng để truy xuất đúng Variant
5. **OrderItem** - đã có SelectedSize và SelectedColor, hiển thị trong chi tiết đơn hàng
6. **Fallback** - nếu không có biến thể nào, hệ thống vẫn hoạt động với `Product.Stock` cũ

---

## 🚀 Hướng dẫn sử dụng

### Admin
1. Vào **/Admin/Products** → Sửa sản phẩm → nhấn **"Quản lý biến thể (Size/Color)"**
2. Thêm biến thể: Size + Color + Tồn kho + Phụ phí (nếu có)
3. Tồn kho tổng của sản phẩm tự động cập nhật

### Khách hàng
1. Vào chi tiết sản phẩm → chọn **Size** → chọn **Màu sắc**
2. Hệ thống tự kiểm tra tồn kho → hiển thị giá có phụ phí (nếu có)
3. Nếu hết hàng → nút "Thêm vào giỏ" bị disable

### Khi đặt hàng
1. Tồn kho biến thể được giảm tự động
2. Khi hủy đơn → tồn kho biến thể được hoàn lại tự động

---

## 🔗 Liên kết các route

| Route | Chức năng |
|-------|-----------|
| `/Admin/ProductVariants/{productId}` | Danh sách biến thể |
| `/Admin/ProductVariantCreate/{productId}` | Thêm biến thể |
| `/Admin/ProductVariantEdit/{id}` | Sửa biến thể |
