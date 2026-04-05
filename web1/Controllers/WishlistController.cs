// ================================================================
// WishlistController - Trang danh sách sản phẩm yêu thích
//
// Yêu cầu đăng nhập ([Authorize]).
// Hiển thị tất cả sản phẩm user đã thêm vào wishlist.
// Toggle wishlist (thêm/xóa) được xử lý trong ProductsController/ToggleWishlist (AJAX).
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;

namespace web1.Controllers
{
    /// <summary>
    /// Controller trang wishlist (yêu thích) - yêu cầu đăng nhập.
    /// Chỉ có action Index - toggle wishlist nằm trong ProductsController.
    /// </summary>
    [Authorize]  // Chỉ user đã đăng nhập mới xem được wishlist
    public class WishlistController : Controller
    {
        // WishlistService: xử lý nghiệp vụ wishlist (thêm, xóa, lấy danh sách)
        private readonly WishlistService          _wishlistService;

        // UserManager: lấy user hiện tại để truy vấn wishlist
        private readonly UserManager<ApplicationUser> _userManager;

        // ================================================================
        // CONSTRUCTOR - Tiêm dependency
        // ================================================================
        public WishlistController(
            WishlistService wishlistService,
            UserManager<ApplicationUser> userManager)
        {
            _wishlistService = wishlistService;
            _userManager     = userManager;
        }

        // ================================================================
        // INDEX - Danh sách sản phẩm yêu thích
        // ================================================================

        /// <summary>
        /// GET: /Wishlist
        /// Hiển thị danh sách sản phẩm yêu thích của user đã đăng nhập.
        /// Lấy user hiện tại từ Claims (User) trong cookie đăng nhập.
        /// Gọi WishlistService.GetWishlistAsync(userId) để lấy danh sách.
        ///
        /// Lưu ý: Toggle wishlist (thêm/xóa) KHÔNG xử lý ở đây.
        /// Nút trái tim trên trang chi tiết sản phẩm gọi
        /// ProductsController.ToggleWishlist (AJAX) để thêm/xóa nhanh.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Lấy user hiện tại từ cookie đăng nhập (ClaimsPrincipal)
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // Gọi service để lấy danh sách sản phẩm yêu thích của user
            var wishlist = await _wishlistService.GetWishlistAsync(user.Id);
            return View(wishlist);
        }
    }
}
