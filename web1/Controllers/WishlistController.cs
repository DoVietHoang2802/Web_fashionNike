// ================================================================
// WishlistController - Trang danh sách sản phẩm yêu thích
//
// Yêu cầu đăng nhập. Hiển thị tất cả sản phẩm user đã wishlist.
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;

namespace web1.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly WishlistService          _wishlistService;
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(
            WishlistService wishlistService,
            UserManager<ApplicationUser> userManager)
        {
            _wishlistService = wishlistService;
            _userManager     = userManager;
        }

        /// <summary>Hiển thị danh sách sản phẩm yêu thích của user đã đăng nhập.</summary>
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var wishlist = await _wishlistService.GetWishlistAsync(user.Id);
            return View(wishlist);
        }
    }
}
