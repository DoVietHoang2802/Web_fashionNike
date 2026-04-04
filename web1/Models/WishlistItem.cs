// ================================================================
// WishlistItem - Model sản phẩm yêu thích
//
// Entity map với bảng [WishlistItems] trong SQL Server.
// Mỗi user chỉ có thể wishlist 1 sản phẩm 1 lần (unique index).
// Yêu cầu đăng nhập để sử dụng (UserId bắt buộc).
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class WishlistItem
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        /// <summary>FK → Product.Id. Xóa wishlist item khi sản phẩm bị xóa.</summary>
        public int ProductId { get; set; }

        /// <summary>Navigation property đến Product.</summary>
        public virtual Product? Product { get; set; }

        /// <summary>FK → ApplicationUser.Id. Bắt buộc — phải đăng nhập mới wishlist được.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Navigation property đến ApplicationUser.</summary>
        public virtual ApplicationUser? User { get; set; }

        /// <summary>Thời điểm thêm vào wishlist.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
