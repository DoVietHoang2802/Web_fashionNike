using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using web1.Models;

namespace web1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;

        public HomeController(ILogger<HomeController> logger, ProductService productService, CategoryService categoryService)
        {
            _logger = logger;
            _productService = productService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            // Chỉ lấy 6 sản phẩm mới nhất cho trang chủ
            const int pageSize = 6;
            var (products, totalCount) = await _productService.GetFilteredProductsAsync(null, null, "newest", page, pageSize);

            ViewBag.Categories = await _categoryService.GetActiveCategoriesAsync();
            ViewBag.CurrentPage = Math.Max(1, page);
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return View(products.ToList());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
