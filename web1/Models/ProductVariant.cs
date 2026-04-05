// ================================================================
// ProductVariant - Biến thể sản phẩm (Size + Color)
//
// Mỗi tổ hợp Size + Color = 1 biến thể riêng biệt.
// Ví dụ: Nike Air Force 1 - Size M - Màu Đen = 1 biến thể
//
// Mục đích:
//   • Quản lý tồn kho theo từng biến thể (Size + Color)
//   • Giá có thể khác nhau theo biến thể (PriceModifier)
//   • Admin: thêm/sửa tồn kho từng biến thể
//   • Customer: chọn Size + Color -> kiểm tra tồn kho biến thể đó
//
// ⚠️ Lưu ý:
//   - Stock tổng của Product vẫn giữ nguyên (tổng tất cả biến thể)
//   - Khi thêm/sửa biến thể -> cập nhật lại Product.Stock = sum(Variant.Stock)
//   - Khi đặt hàng -> giảm Variant.Stock thay vì Product.Stock
// ================================================================
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace web1.Models
{
    public class ProductVariant
    {
        /// <summary>PK tự tăng.</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>FK → Product.Id. Sản phẩm cha mà biến thể này thuộc về.</summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// Size của biến thể.
        /// VD: "S", "M", "L", "XL", "40", "41", "42"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Màu sắc của biến thể.
        /// VD: "Đen", "Trắng", "Xanh Navy", "Đỏ"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Tồn kho của biến thể này (Size + Color cụ thể).
        /// Giảm khi đặt hàng, tăng khi hủy đơn.
        /// </summary>
        [Display(Name = "Tồn kho")]
        public int Stock { get; set; } = 0;

        /// <summary>
        /// Phụ phí / giảm giá cho biến thể này so với giá gốc của Product.
        /// VD: +50,000 (size lớn hơn đắt hơn), -20,000 (khuyến mãi)
        /// Null = giá bằng giá gốc của Product.
        /// Giá bán thực tế = Product.Price + (PriceModifier ?? 0)
        /// </summary>
        [Display(Name = "Điều chỉnh giá")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceModifier { get; set; }

        /// <summary>
        /// Biến thể có đang được bán không.
        /// false = ẩn biến thể này (hết hàng vĩnh viễn hoặc ngừng bán).
        /// </summary>
        [Display(Name = "Hoạt động")]
        public bool IsActive { get; set; } = true;

        // ================================================================
        // NAVIGATION PROPERTIES
        // ================================================================

        /// <summary>Navigation đến Product cha.</summary>
        public virtual Product? Product { get; set; }

        // ================================================================
        // COMPUTED PROPERTIES
        // ================================================================

        /// <summary>
        /// Giá bán thực tế của biến thể.
        /// = Giá gốc Product + PriceModifier (nếu có).
        /// </summary>
        [NotMapped]
        public decimal EffectivePrice => (Product?.Price ?? 0) + (PriceModifier ?? 0);
    }
}
