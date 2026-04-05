// ================================================================
// HomeController - Trang chủ và trang lỗi
//
// Chức năng:
//   • Trang chủ: hiển thị sản phẩm mới nhất + danh mục active
//   • Privacy: trang chính sách bảo mật
//   • Error: trang lỗi chung
// ================================================================
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using web1.Models;

namespace web1.Controllers
{
    /// <summary>
    /// Controller trang chủ - không yêu cầu đăng nhập.
    /// Cung cấp dữ liệu sản phẩm và danh mục cho trang chủ.
    /// </summary>
    public class HomeController : Controller
    {
        // ILogger<HomeController>: ghi log lỗi / cảnh báo ra console hoặc file log
        // Dùng trong Error() action để log request bị lỗi
        private readonly ILogger<HomeController> _logger;

        // ProductService: lấy danh sách sản phẩm cho trang chủ
        private readonly ProductService _productService;

        // CategoryService: lấy danh sách danh mục active để hiển thị menu/filter
        private readonly CategoryService _categoryService;

        // ================================================================
        // CONSTRUCTOR - Tiêm dependency
        // ================================================================
        public HomeController(ILogger<HomeController> logger, ProductService productService, CategoryService categoryService)
        {
            _logger = _logger;
            _productService = productService;
            _categoryService = categoryService;
        }

        // ================================================================
        // INDEX - Trang chủ
        // ================================================================

        /// <summary>
        /// GET: / hoặc /Home
        /// Hiển thị trang chủ với:
        ///   - 6 sản phẩm mới nhất (sắp xếp theo "newest")
        ///   - Danh sách danh mục active (hiển thị trên menu/carousel)
        ///   - Phân trang nếu sản phẩm nhiều
        /// </summary>
        /// <param name="page">Số trang hiện tại (default = 1)</param>
        public async Task<IActionResult> Index(int page = 1)
        {
            // Lấy sản phẩm đã lọc + phân trang
            // categoryId = null: không lọc theo danh mục (hiện tất cả)
            // search = null: không tìm kiếm
            // sort = "newest": sắp xếp sản phẩm mới nhất lên đầu
            const int pageSize = 6;  // Chỉ 6 sản phẩm trên trang chủ (ít hơn trang Products 9 sản/phân trang)
            var (products, totalCount) = await _productService
                .GetFilteredProductsAsync(null, null, "newest", page, pageSize);

            // Lấy danh mục active để hiển thị navigation bar / menu
            ViewBag.Categories = await _categoryService.GetActiveCategoriesAsync();

            // Phân trang
            ViewBag.CurrentPage = Math.Max(1, page);   // Đảm bảo page >= 1
            ViewBag.TotalPages  = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return View(products.ToList());
        }

        // ================================================================
        // PRIVACY - Trang chính sách bảo mật
        // ================================================================

        /// <summary>
        /// GET: /Home/Privacy
        /// Trang tĩnh hiển thị chính sách bảo mật của website.
        /// Không cần load dữ liệu từ DB.
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        // ================================================================
        // ERROR - Trang lỗi chung
        // ================================================================

        /// <summary>
        /// GET: /Home/Error
        /// Hiển thị trang lỗi khi có exception không xử lý được.
        /// [ResponseCache]: không cache trang lỗi này.
        /// RequestId dùng để user báo lỗi khi liên hệ support.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Activity.Current?.Id: lấy ID của request hiện tại (correlation ID)
            // HttpContext.TraceIdentifier: trace ID từ server
            // Dùng để support team tra cứu log theo ID này
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
