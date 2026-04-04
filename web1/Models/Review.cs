// ================================================================
// Review - Model đánh giá sản phẩm
//
// Entity map với bảng [Reviews] trong SQL Server.
// Rating: điểm từ 1–5 do khách/nhập.
// Khi thêm Review mới → ProductService.AddReviewAsync tự động cập nhật
// Rating (điểm TB) & ReviewCount trên Product tương ứng.
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class Review
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        /// <summary>FK → Product.Id.</summary>
        public int ProductId { get; set; }

        /// <summary>Navigation property đến Product.</summary>
        public virtual Product? Product { get; set; }

        /// <summary>FK → ApplicationUser.Id. Null = khách chưa đăng nhập.</summary>
        public string? UserId { get; set; }

        /// <summary>Navigation property đến ApplicationUser.</summary>
        public virtual ApplicationUser? User { get; set; }

        /// <summary>Họ tên người đánh giá (tự điền nếu đã đăng nhập).</summary>
        [Display(Name = "Họ tên")]
        public string? CustomerName { get; set; }

        /// <summary>Điểm đánh giá từ 1–5.</summary>
        [Display(Name = "Đánh giá")]
        public int? Rating { get; set; }

        /// <summary>Nội dung đánh giá.</summary>
        [Display(Name = "Nội dung")]
        [MaxLength(1000)]
        public string? Content { get; set; }

        /// <summary>Thời điểm tạo đánh giá.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
