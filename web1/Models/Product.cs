// ================================================================
// Product - Model sản phẩm
//
// Entity map trực tiếp với bảng [Products] trong SQL Server.
// Rating & ReviewCount: được cập nhật tự động khi có đánh giá mới (xem ProductService.AddReviewAsync).
// SoldCount: được tăng khi đặt hàng thành công, giảm khi hủy đơn (xem OrderService).
// Stock: mặc định 100, giảm khi đặt hàng, tăng khi hủy đơn.
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class Product
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        [Display(Name = "Tên sản phẩm")]
        public string? Name { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Giá")]
        public decimal? Price { get; set; }

        /// <summary>FK → Category.Id. Null = chưa phân loại.</summary>
        [Display(Name = "Danh mục")]
        public int? CategoryId { get; set; }

        /// <summary>Navigation property đến Category.</summary>
        public virtual Category? Category { get; set; }

        [Display(Name = "Hình ảnh")]
        public string? ImageUrl { get; set; }

        /// <summary>Tồn kho hiện tại. Mặc định 100.</summary>
        [Display(Name = "Tồn kho")]
        public int? Stock { get; set; } = 100;

        /// <summary>Điểm TB từ 0–5 (tự động tính khi thêm đánh giá).</summary>
        [Display(Name = "Đánh giá")]
        public decimal? Rating { get; set; }

        /// <summary>Tổng số đánh giá đã nhận.</summary>
        [Display(Name = "Số lượt đánh giá")]
        public int? ReviewCount { get; set; }

        /// <summary>Tổng số đã bán (tăng khi đặt hàng, giảm khi hủy đơn).</summary>
        [Display(Name = "Đã bán")]
        public int? SoldCount { get; set; } = 0;

        [Display(Name = "Ngày tạo")]
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
    }
}
