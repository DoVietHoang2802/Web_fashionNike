// ================================================================
// ProductsController - Danh sách sản phẩm, chi tiết, đánh giá, wishlist
//
// Wishlist: yêu cầu đăng nhập. Toggle bằng AJAX trên trang chi tiết.
// ================================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;
using Microsoft.EntityFrameworkCore;

namespace web1.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ProductService  _productService;
        private readonly CategoryService _categoryService;
        private readonly WishlistService _wishlistService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductsController(
            ProductService          productService,
            CategoryService         categoryService,
            WishlistService         wishlistService,
            UserManager<ApplicationUser> userManager)
        {
            _productService   = productService;
            _categoryService  = categoryService;
            _wishlistService = wishlistService;
            _userManager     = userManager;
        }

        // ================================================================
        // INDEX - Danh sách sản phẩm (lọc, sắp xếp, phân trang)
        // ================================================================
        public async Task<IActionResult> Index(
            int? categoryId, string? search,
            string? sort = "newest", int page = 1)
        {
            int pageSize = 9;
            var (products, totalCount) = await _productService
                .GetFilteredProductsAsync(categoryId, search, sort, page, pageSize);

            var topSelling    = await _productService.GetTopSellingProductsAsync(3);
            var topSellingIds  = topSelling.Select(p => p.Id).ToList();

            // Đưa sản phẩm bán chạy lên 3 hàng đầu tiên
            var productList = products.ToList();
            if (topSellingIds.Any())
            {
                var sorted = new List<Product>();
                foreach (var id in topSellingIds)
                {
                    var item = productList.FirstOrDefault(p => p.Id == id);
                    if (item != null) { sorted.Add(item); productList.Remove(item); }
                }
                sorted.AddRange(productList);
                productList = sorted;
            }

            ViewBag.Categories        = await _categoryService.GetActiveCategoriesAsync();
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SearchTerm        = search;
            ViewBag.SortOrder         = sort;
            ViewBag.CurrentPage       = page;
            ViewBag.TotalPages        = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount        = totalCount;
            ViewBag.TopSellingIds     = topSellingIds;

            return View(productList);
        }

        // ================================================================
        // DETAILS - Chi tiết sản phẩm (kèm wishlist status)
        // ================================================================
        public async Task<IActionResult> Details(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            // ── Sản phẩm liên quan ──────────────────────────────────────
            var relatedProducts = await _productService.GetRelatedProductsAsync(id, 4);

            // ── Đánh giá ─────────────────────────────────────────────────
            var reviews = await _productService.GetReviewsByProductIdAsync(id);

            // ── Wishlist status (chỉ khi đã đăng nhập) ─────────────────
            bool isInWishlist = false;
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                isInWishlist = await _wishlistService.IsInWishlistAsync(id, user.Id);

            ViewBag.RelatedProducts = relatedProducts;
            ViewBag.Reviews         = reviews;
            ViewBag.IsInWishlist    = isInWishlist;

            return View(product);
        }

        // ================================================================
        // ADD REVIEW
        // ================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(Review review)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                review.UserId = user.Id;
                if (string.IsNullOrEmpty(review.CustomerName) || review.CustomerName == "Logged-in User")
                    review.CustomerName = user.FullName ?? user.UserName ?? "Khách hàng";
            }

            if (ModelState.IsValid)
            {
                review.CreatedDate = DateTime.Now;
                await _productService.AddReviewAsync(review);
                TempData["ReviewSuccess"] = "Cảm ơn bạn đã gửi đánh giá!";
                return RedirectToAction(nameof(Details), new { id = review.ProductId });
            }

            TempData["ReviewError"] = "Vui lòng điền đầy đủ thông tin đánh giá.";
            return RedirectToAction(nameof(Details), new { id = review.ProductId });
        }

        // ================================================================
        // QUICK VIEW (AJAX)
        // ================================================================
        public async Task<IActionResult> QuickView(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Reviews = await _productService.GetReviewsByProductIdAsync(id);
            return PartialView("_QuickView", product);
        }

        // ================================================================
        // WISHLIST (AJAX)
        // ================================================================

        /// <summary>Toggle wishlist — thêm nếu chưa có, xóa nếu đã có.</summary>
        [HttpPost]
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });

            var isInWishlist = await _wishlistService.IsInWishlistAsync(productId, user.Id);

            if (isInWishlist)
            {
                await _wishlistService.RemoveFromWishlistAsync(productId, user.Id);
                return Json(new
                {
                    success      = true,
                    action       = "removed",
                    message      = "Đã xóa khỏi yêu thích.",
                    isInWishlist = false
                });
            }
            else
            {
                await _wishlistService.AddToWishlistAsync(productId, user.Id);
                return Json(new
                {
                    success      = true,
                    action       = "added",
                    message      = "Đã thêm vào yêu thích!",
                    isInWishlist = true
                });
            }
        }
    }
}
