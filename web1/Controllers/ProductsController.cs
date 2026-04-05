// ================================================================
// ProductsController - Trang sản phẩm cho khách hàng
//
// Chức năng:
//   • Index: Danh sách sản phẩm (lọc, sắp xếp, phân trang, sản phẩm bán chạy)
//   • Details: Chi tiết sản phẩm (kèm đánh giá, sản phẩm liên quan, wishlist)
//   • AddReview: Gửi đánh giá sản phẩm
//   • QuickView: Xem nhanh sản phẩm (AJAX - partial view)
//   • ToggleWishlist: Thêm/xóa yêu thích (AJAX)
//
// NOTE: CRUD sản phẩm (thêm/sửa/xóa) nằm trong AdminController
// ================================================================
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;
using Microsoft.EntityFrameworkCore;

namespace web1.Controllers
{
    /// <summary>
    /// Controller hiển thị sản phẩm cho khách hàng - không yêu cầu đăng nhập.
    /// (Trừ ToggleWishlist cần đăng nhập để biết wishlist của ai)
    /// </summary>
    public class ProductsController : Controller
    {
        // ProductService: lấy sản phẩm, đánh giá, sản phẩm liên quan, bán chạy
        private readonly ProductService  _productService;

        // CategoryService: lấy danh sách danh mục để hiển thị bộ lọc
        private readonly CategoryService _categoryService;

        // WishlistService: kiểm tra / thêm / xóa wishlist
        private readonly WishlistService _wishlistService;

        // UserManager: lấy user hiện tại để kiểm tra wishlist status
        private readonly UserManager<ApplicationUser> _userManager;

        // ProductVariantService: lấy biến thể (Size + Color) để hiển thị
        private readonly ProductVariantService _variantService;

        // ================================================================
        // CONSTRUCTOR - Tiêm dependency
        // ================================================================
        public ProductsController(
            ProductService          productService,
            CategoryService         categoryService,
            WishlistService         wishlistService,
            UserManager<ApplicationUser> userManager,
            ProductVariantService    variantService)
        {
            _productService   = productService;
            _categoryService  = categoryService;
            _wishlistService = wishlistService;
            _userManager     = userManager;
            _variantService  = variantService;
        }

        // ================================================================
        // INDEX - Danh sách sản phẩm (SHOP PAGE)
        // ================================================================

        /// <summary>
        /// GET: /Products hoặc /Products/Index
        /// Trang danh sách sản phẩm với bộ lọc:
        ///   - categoryId: lọc theo danh mục
        ///   - search: tìm theo tên sản phẩm
        ///   - sort: sắp xếp (newest, oldest, price_asc, price_desc, popularity)
        ///   - Phân trang: 9 sản phẩm / trang
        ///
        /// Đặc biệt: 3 sản phẩm bán chạy nhất được đưa lên 3 vị trí đầu tiên.
        /// </summary>
        /// <param name="categoryId">Lọc theo danh mục (null = tất cả)</param>
        /// <param name="search">Từ khóa tìm kiếm theo tên</param>
        /// <param name="sort">Sắp xếp: newest, oldest, price_asc, price_desc, popularity</param>
        /// <param name="page">Số trang (default = 1)</param>
        public async Task<IActionResult> Index(
            int? categoryId, string? search,
            string? sort = "newest", int page = 1)
        {
            // Lấy danh sách sản phẩm đã lọc + phân trang từ ProductService
            int pageSize = 9;  // 9 sản phẩm mỗi trang
            var (products, totalCount) = await _productService
                .GetFilteredProductsAsync(categoryId, search, sort, page, pageSize);

            // Lấy 3 sản phẩm bán chạy nhất (TopSelling)
            var topSelling    = await _productService.GetTopSellingProductsAsync(3);
            var topSellingIds  = topSelling.Select(p => p.Id).ToList();

            // Đưa sản phẩm bán chạy lên 3 hàng đầu tiên
            var productList = products.ToList();
            if (topSellingIds.Any())
            {
                var sorted = new List<Product>();
                // Bước 1: Thêm 3 sản phẩm bán chạy vào đầu danh sách
                foreach (var id in topSellingIds)
                {
                    var item = productList.FirstOrDefault(p => p.Id == id);
                    if (item != null) { sorted.Add(item); productList.Remove(item); }
                }
                // Bước 2: Thêm các sản phẩm còn lại phía sau
                sorted.AddRange(productList);
                productList = sorted;
            }

            // Chuẩn bị dữ liệu cho View (bộ lọc, phân trang)
            ViewBag.Categories         = await _categoryService.GetActiveCategoriesAsync();
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SearchTerm        = search;
            ViewBag.SortOrder         = sort;
            ViewBag.CurrentPage        = page;
            ViewBag.TotalPages         = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.TotalCount         = totalCount;
            ViewBag.TopSellingIds      = topSellingIds;  // Để View highlight sản phẩm bán chạy

            return View(productList);
        }

        // ================================================================
        // DETAILS - Chi tiết sản phẩm
        // ================================================================

        /// <summary>
        /// GET: /Products/Details/{id}
        /// Hiển thị chi tiết sản phẩm gồm:
        ///   - Thông tin sản phẩm (tên, giá, mô tả, hình ảnh, tồn kho)
        ///   - Sản phẩm liên quan (4 sản phẩm cùng danh mục)
        ///   - Đánh giá của khách hàng
        ///   - Trạng thái wishlist (nếu đã đăng nhập)
        /// </summary>
        /// <param name="id">ID sản phẩm</param>
        public async Task<IActionResult> Details(int id)
        {
            // Lấy thông tin sản phẩm theo ID
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();  // Không tìm thấy -> 404

            // ── Sản phẩm liên quan (4 sản phẩm cùng danh mục, không bao gồm sản phẩm hiện tại)
            var relatedProducts = await _productService.GetRelatedProductsAsync(id, 4);

            // ── Đánh giá sản phẩm (danh sách review từ khách hàng)
            var reviews = await _productService.GetReviewsByProductIdAsync(id);

            // ── Biến thể sản phẩm (Size + Color)
            var variants = await _variantService.GetByProductIdAsync(id);
            var availableSizes = await _variantService.GetAvailableSizesAsync(id);
            var availableColors = await _variantService.GetAllAvailableColorsAsync(id);

            // ── Kiểm tra wishlist status (chỉ khi đã đăng nhập)
            // Nếu chưa đăng nhập -> mặc định không trong wishlist
            bool isInWishlist = false;
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                isInWishlist = await _wishlistService.IsInWishlistAsync(id, user.Id);

            // Truyền dữ liệu sang View
            ViewBag.RelatedProducts = relatedProducts;
            ViewBag.Reviews         = reviews;
            ViewBag.IsInWishlist    = isInWishlist;
            ViewBag.Variants        = variants;
            ViewBag.AvailableSizes  = availableSizes;
            ViewBag.AvailableColors = availableColors;

            return View(product);
        }

        // ================================================================
        // ADD REVIEW - Gửi đánh giá sản phẩm
        // ================================================================

        /// <summary>
        /// POST: /Products/AddReview
        /// Khách hàng gửi đánh giá sản phẩm (sao + bình luận).
        /// Nếu đã đăng nhập -> dùng FullName của user làm tên khách hàng.
        /// Nếu chưa đăng nhập -> dùng tên nhập vào form.
        /// </summary>
        /// <param name="review">Dữ liệu review từ form (ProductId, Rating, Comment...)</param>
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống CSRF
        public async Task<IActionResult> AddReview(Review review)
        {
            var user = await _userManager.GetUserAsync(User);

            // Nếu đã đăng nhập -> lấy FullName từ user thay vì nhập form
            if (user != null)
            {
                review.UserId = user.Id;  // Gán UserId để biết ai đánh giá
                if (string.IsNullOrEmpty(review.CustomerName) || review.CustomerName == "Logged-in User")
                    review.CustomerName = user.FullName ?? user.UserName ?? "Khách hàng";
            }

            // Validate dữ liệu đầu vào
            if (ModelState.IsValid)
            {
                review.CreatedDate = DateTime.Now;  // Ghi nhận thời điểm đánh giá
                await _productService.AddReviewAsync(review);  // Lưu vào DB
                TempData["ReviewSuccess"] = "Cảm ơn bạn đã gửi đánh giá!";
                return RedirectToAction(nameof(Details), new { id = review.ProductId });
            }

            TempData["ReviewError"] = "Vui lòng điền đầy đủ thông tin đánh giá.";
            return RedirectToAction(nameof(Details), new { id = review.ProductId });
        }

        // ================================================================
        // QUICK VIEW - Xem nhanh sản phẩm (AJAX)
        // ================================================================

        /// <summary>
        /// GET: /Products/QuickView/{id}
        /// Trả về Partial View (_QuickView) chứa thông tin cơ bản của sản phẩm.
        /// Dùng cho popup/modal "Xem nhanh" trên trang danh sách sản phẩm.
        /// Được gọi qua AJAX.
        /// </summary>
        /// <param name="id">ID sản phẩm muốn xem nhanh</param>
        public async Task<IActionResult> QuickView(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            // Lấy đánh giá để hiển thị rating trong popup
            ViewBag.Reviews = await _productService.GetReviewsByProductIdAsync(id);

            // Trả về Partial View (chỉ phần nội dung, không layout)
            return PartialView("_QuickView", product);
        }

        // ================================================================
        // TOGGLE WISHLIST - Thêm / Xóa yêu thích (AJAX)
        // ================================================================

        /// <summary>
        /// POST: /Products/ToggleWishlist
        /// Toggle wishlist: nếu chưa có thì thêm, nếu đã có thì xóa.
        /// Trả về JSON để AJAX cập nhật icon trái tim trên UI.
        ///
        /// Quy trình:
        ///   1. Kiểm tra đã đăng nhập chưa -> chưa thì báo lỗi
        ///   2. Kiểm tra sản phẩm đã trong wishlist chưa (IsInWishlistAsync)
        ///   3. Nếu có -> xóa (RemoveFromWishlistAsync)
        ///   4. Nếu chưa -> thêm (AddToWishlistAsync)
        ///   5. Trả về JSON kết quả
        /// </summary>
        /// <param name="productId">ID sản phẩm muốn toggle wishlist</param>
        [HttpPost]
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            // Bước 1: Kiểm tra đăng nhập
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });

            // Bước 2: Kiểm tra sản phẩm đã nằm trong wishlist chưa
            var isInWishlist = await _wishlistService.IsInWishlistAsync(productId, user.Id);

            if (isInWishlist)
            {
                // Trường hợp A: Đã có trong wishlist -> XÓA
                await _wishlistService.RemoveFromWishlistAsync(productId, user.Id);
                return Json(new
                {
                    success      = true,
                    action       = "removed",          // Để JS biết đổi icon trái tim
                    message      = "Đã xóa khỏi yêu thích.",
                    isInWishlist = false               // Trạng thái mới
                });
            }
            else
            {
                // Trường hợp B: Chưa có trong wishlist -> THÊM
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
