// ================================================================
// WishlistService - Xử lý nghiệp vụ sản phẩm yêu thích
//
// Quy tắc:
//   • User phải đăng nhập mới thao tác được wishlist.
//   • Mỗi user chỉ wishlist 1 sản phẩm 1 lần (unique index ở DB).
//   • Xóa wishlist item không ảnh hưởng đến sản phẩm.
// ================================================================
using Microsoft.EntityFrameworkCore;
using web1.Data;

namespace web1.Models
{
    public class WishlistService
    {
        private readonly ApplicationDbContext _context;

        public WishlistService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Lấy danh sách sản phẩm yêu thích của user (kèm thông tin Product).</summary>
        public async Task<List<WishlistItem>> GetWishlistAsync(string userId)
            => await _context.WishlistItems
                .AsNoTracking()
                .Include(w => w.Product)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedDate)
                .ToListAsync();

        /// <summary>Thêm sản phẩm vào wishlist. Return false nếu đã tồn tại.</summary>
        public async Task<bool> AddToWishlistAsync(int productId, string userId)
        {
            var exists = await _context.WishlistItems
                .AnyAsync(w => w.ProductId == productId && w.UserId == userId);
            if (exists) return false;

            _context.WishlistItems.Add(new WishlistItem
            {
                ProductId  = productId,
                UserId      = userId,
                CreatedDate = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Xóa sản phẩm khỏi wishlist. Return false nếu không tìm thấy.</summary>
        public async Task<bool> RemoveFromWishlistAsync(int productId, string userId)
        {
            var item = await _context.WishlistItems
                .FirstOrDefaultAsync(w => w.ProductId == productId && w.UserId == userId);
            if (item == null) return false;

            _context.WishlistItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>Kiểm tra sản phẩm đã nằm trong wishlist của user chưa.</summary>
        public async Task<bool> IsInWishlistAsync(int productId, string userId)
            => await _context.WishlistItems
                .AnyAsync(w => w.ProductId == productId && w.UserId == userId);

        /// <summary>Đếm tổng sản phẩm trong wishlist của user.</summary>
        public async Task<int> GetWishlistCountAsync(string userId)
            => await _context.WishlistItems.CountAsync(w => w.UserId == userId);
    }
}
