// ================================================================
// Category - Model danh mục sản phẩm
//
// Entity map với bảng [Categories] trong SQL Server.
// Slug: unique, tạo tự động từ Name (viết thường, dấu gạch ngang).
// Cache: GetActiveCategoriesAsync cache 30 phút trong IMemoryCache.
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class Category
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        [Display(Name = "Tên danh mục")]
        public string? Name { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        /// <summary>URL slug — unique, tạo tự động từ Name bằng CategoryService.GenerateSlug.</summary>
        [StringLength(100)]
        [Display(Name = "Đường dẫn URL")]
        public string? Slug { get; set; }

        /// <summary>Thứ tự ưu tiên hiển thị (số nhỏ = hiển thị trước).</summary>
        [Display(Name = "Thứ tự hiển thị")]
        public int DisplayOrder { get; set; } = 0;

        /// <summary>False → ẩn danh mục khỏi frontend (vẫn còn trong DB).</summary>
        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
