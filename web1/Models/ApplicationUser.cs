using Microsoft.AspNetCore.Identity;

namespace web1.Models
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        public string? FullName { get; set; }

        [PersonalData]
        public string? Address { get; set; }

        [PersonalData]
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Đường dẫn avatar của user. Null = dùng icon mặc định.</summary>
        [PersonalData]
        public string? AvatarUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Alias for views that use RegisteredDate
        public DateTime RegisteredDate => CreatedAt;

        // Navigation
        public List<Order> Orders { get; set; } = new List<Order>();

        /// <summary>Danh sách sản phẩm yêu thích của user.</summary>
        public List<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    }
}
