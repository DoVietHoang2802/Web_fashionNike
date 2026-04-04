// ================================================================
// Order - Model đơn hàng
//
// Entity map với bảng [Orders] trong SQL Server.
//
// Trạng thái đơn hàng (Status):
//   Chờ xác nhận → Đã xác nhận → Giao ĐVVC → Đang giao → Đến nơi → Hoàn tất
//   Hoặc: Đã hủy / Trả hàng (hoàn tồn kho khi chuyển sang 2 trạng thái này).
//
// Trạng thái thanh toán (PaymentStatus):
//   Chưa thanh toán → Chờ thanh toán → Thất bại → Đã thanh toán
//
// NOTE: UserId nullable — khách chưa đăng nhập vẫn đặt được.
// IsDeletedByUser: soft delete, ẩn đơn khỏi view của user.
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class Order
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        [Display(Name = "Họ tên")]
        [StringLength(200)]
        public string? CustomerName { get; set; }

        [Display(Name = "Email")]
        [StringLength(200)]
        public string? Email { get; set; }

        [Display(Name = "Số điện thoại")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "Địa chỉ")]
        [StringLength(500)]
        public string? Address { get; set; }

        [Display(Name = "Ngày đặt")]
        public DateTime? OrderDate { get; set; } = DateTime.Now;

        /// <summary>Phương thức thanh toán. Mặc định "COD".</summary>
        [Display(Name = "Phương thức thanh toán")]
        public string? PaymentMethod { get; set; } = "COD";

        [Display(Name = "Tổng tiền")]
        public decimal? TotalAmount { get; set; }

        [Display(Name = "Phí vận chuyển")]
        public decimal? ShippingFee { get; set; }

        [Display(Name = "Mã giảm giá")]
        [StringLength(50)]
        public string? CouponCode { get; set; }

        [Display(Name = "Số tiền giảm")]
        public decimal? DiscountAmount { get; set; }

        /// <summary>Trạng thái đơn hàng. Mặc định "Chờ xác nhận".</summary>
        [Display(Name = "Trạng thái")]
        public string? Status { get; set; } = "Chờ xác nhận";

        /// <summary>Trạng thái thanh toán. Mặc định "Chưa thanh toán".</summary>
        [Display(Name = "Trạng thái thanh toán")]
        public string? PaymentStatus { get; set; } = "Chưa thanh toán";

        /// <summary>FK → ApplicationUser.Id. Null = khách chưa đăng nhập.</summary>
        public string? UserId { get; set; }

        /// <summary>Soft delete — user ẩn đơn khỏi lịch sử của mình.</summary>
        public bool? IsDeletedByUser { get; set; } = false;

        /// <summary>Danh sách sản phẩm trong đơn hàng.</summary>
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    // ================================================================
    // OrderItem - Chi tiết từng sản phẩm trong một đơn hàng
    // ================================================================
    public class OrderItem
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        /// <summary>FK → Order.Id.</summary>
        public int? OrderId { get; set; }

        /// <summary>Navigation property đến Order.</summary>
        public virtual Order? Order { get; set; }

        /// <summary>FK → Product.Id.</summary>
        public int? ProductId { get; set; }

        /// <summary>Navigation property đến Product.</summary>
        public virtual Product? Product { get; set; }

        /// <summary>Số lượng đặt.</summary>
        public int? Quantity { get; set; }

        /// <summary>Giá tại thời điểm đặt hàng (snapshot, không cập nhật theo giá hiện tại).</summary>
        public decimal? Price { get; set; }

        /// <summary>Size được chọn khi thêm vào giỏ.</summary>
        public string? SelectedSize { get; set; }

        /// <summary>Màu được chọn khi thêm vào giỏ.</summary>
        public string? SelectedColor { get; set; }
    }
}
