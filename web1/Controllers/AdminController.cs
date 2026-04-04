// ================================================================
// AdminController - Trang quản trị (yêu cầu vai trò Admin)
//
// Quy tắc:
//   • Tất cả actions đều có [Authorize(Roles = "Admin")].
//   • CRUD coupon dùng trực tiếp _context (CouponService chỉ phục vụ validation).
//   • Image upload: dùng IWebHostEnvironment.ContentRootPath thay vì Directory.GetCurrentDirectory().
//   • UpdateOrderStatus chỉ gọi OrderService — logic chuyển trạng thái đã có trong service.
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web1.Data;
using web1.Models;

namespace web1.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductService       _productService;
        private readonly OrderService        _orderService;
        private readonly CouponService        _couponService;
        private readonly CategoryService      _categoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            ApplicationDbContext           context,
            ProductService                 productService,
            OrderService                  orderService,
            CouponService                 couponService,
            CategoryService               categoryService,
            UserManager<ApplicationUser>   userManager,
            IWebHostEnvironment           env)
        {
            _context          = context;
            _productService   = productService;
            _orderService     = orderService;
            _couponService    = couponService;
            _categoryService  = categoryService;
            _userManager      = userManager;
            _env              = env;
        }

        // ================================================================
        // DASHBOARD
        // ================================================================
        public async Task<IActionResult> Index()
        {
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var orders = await _context.Orders
                .Where(o => o.OrderDate >= sixMonthsAgo && o.Status != "Đã hủy")
                .ToListAsync();

            var monthlyRevenue = orders
                .GroupBy(o => o.OrderDate?.ToString("MM/yyyy") ?? "N/A")
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount ?? 0));

            var dashboard = new AdminDashboardViewModel
            {
                TotalProducts  = await _context.Products.CountAsync(),
                TotalOrders    = await _context.Orders.CountAsync(),
                TotalUsers    = await _userManager.Users.CountAsync(),
                TotalRevenue  = (await _context.Orders
                    .Where(o => o.Status != "Đã hủy")
                    .SumAsync(o => o.TotalAmount)) ?? 0,
                PendingOrders    = await _context.Orders.CountAsync(o => o.Status == "Chờ xác nhận"),
                RecentOrders     = await _orderService.GetAllOrdersAsync(),
                LowStockProducts = await _context.Products.Where(p => p.Stock < 20).ToListAsync(),
                MonthlyRevenue   = monthlyRevenue
            };

            return View(dashboard);
        }

        // ================================================================
        // PRODUCTS
        // ================================================================

        /// <summary>Danh sách sản phẩm — hỗ trợ search / lọc / tồn kho thấp / phân trang.</summary>
        public async Task<IActionResult> Products(string? search, int? categoryId, bool? lowStock, int page = 1)
        {
            var products = await _productService.GetAllProductsAsync();

            if (!string.IsNullOrEmpty(search))
                products = products
                    .Where(p => p.Name != null && p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();

            if (lowStock == true)
                products = products.Where(p => p.Stock < 20).ToList();

            int pageSize = 10;
            var paged = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.TotalPages  = (int)Math.Ceiling(products.Count / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search      = search;
            ViewBag.CategoryId  = categoryId;
            ViewBag.LowStock    = lowStock;
            ViewBag.Categories  = await _categoryService.GetAllCategoriesAsync();

            return View(paged);
        }

        /// <summary>Form thêm sản phẩm mới.</summary>
        public async Task<IActionResult> ProductCreate()
        {
            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
            return View();
        }

        /// <summary>Tạo sản phẩm mới — ưu tiên file upload > URL nhập tay > placeholder.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductCreate(Product product, IFormFile? ImageFile)
        {
            ModelState.Remove("Price");
            if (product.Price <= 0)
                ModelState.AddModelError("Price", "Giá sản phẩm phải lớn hơn 0");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
                return View(product);
            }

            // Upload file nếu có
            if (ImageFile != null && ImageFile.Length > 0)
                product.ImageUrl = await SaveImageFile(ImageFile, "products");

            else if (string.IsNullOrWhiteSpace(product.ImageUrl))
                product.ImageUrl = null;

            product.CreatedDate = DateTime.Now;
            await _productService.CreateProductAsync(product);

            TempData["Success"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        /// <summary>Form chỉnh sửa sản phẩm.</summary>
        public async Task<IActionResult> ProductEdit(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
            return View(product);
        }

        /// <summary>
        /// Cập nhật sản phẩm.
        /// Ưu tiên: file upload > URL nhập tay > giữ nguyên ảnh cũ.
        /// Rating/ReviewCount/SoldCount/CreatedDate được giữ nguyên (ProductService đảm bảo).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProductEdit(Product product, IFormFile? ImageFile)
        {
            ModelState.Remove("Price");
            if (product.Price <= 0)
                ModelState.AddModelError("Price", "Giá sản phẩm phải lớn hơn 0");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
                return View(product);
            }

            var existing = await _productService.GetProductByIdAsync(product.Id);
            if (existing == null) return NotFound();

            // Cập nhật trường cơ bản
            existing.Name        = product.Name;
            existing.Description = product.Description;
            existing.Price       = product.Price;
            existing.CategoryId  = product.CategoryId;
            existing.Stock       = product.Stock;

            // Upload file mới nếu có
            if (ImageFile != null && ImageFile.Length > 0)
                existing.ImageUrl = await SaveImageFile(ImageFile, "products");

            // Hoặc cập nhật URL nếu người dùng nhập tay
            else if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                existing.ImageUrl = product.ImageUrl.Trim();

            // (ImageUrl cũ được giữ nguyên nếu cả 2 đều trống)

            await _productService.UpdateProductAsync(existing);
            TempData["Success"] = "Cập nhật sản phẩm thành công!";

            return RedirectToAction(nameof(Products));
        }

        /// <summary>Xóa sản phẩm (xóa luôn OrderItems liên quan).</summary>
        [HttpPost]
        public async Task<IActionResult> ProductDelete(int id)
        {
            await _productService.DeleteProductAsync(id);
            TempData["Success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        // ================================================================
        // ORDERS
        // ================================================================

        /// <summary>Danh sách đơn hàng — lọc theo status / search / phân trang.</summary>
        public async Task<IActionResult> Orders(string? status, string? search, int page = 1)
        {
            int pageSize = 15;
            var (orders, totalCount) = await _orderService
                .GetFilteredOrdersAsync(status, search, page, pageSize);

            ViewBag.TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Status      = status;
            ViewBag.Search      = search;
            ViewBag.StatusList   = new[]
                { "Chờ xác nhận", "Đã xác nhận", "Giao ĐVVC", "Đang giao", "Hoàn tất", "Đã hủy" };

            return View(orders);
        }

        /// <summary>Chi tiết đơn hàng.</summary>
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        /// <summary>
        /// Cập nhật trạng thái đơn hàng.
        /// NOTE: Logic chuyển trạng thái (hoàn tồn kho, giảm tồn kho khi khôi phục…)
        /// đã được xử lý TRONG OrderService.UpdateOrderStatusAsync rồi.
        /// Controller chỉ gọi service + kiểm tra quyền trước khi chuyển.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null) return NotFound();

            // Không cho thay đổi đơn đã kết thúc
            if (order.Status == "Hoàn tất" || order.Status == "Đã hủy")
            {
                TempData["Message"] = "Đơn hàng đã kết thúc, không thể thay đổi trạng thái.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // Không cho hủy đơn đã vận chuyển / hoàn tất
            if (status == "Đã hủy")
            {
                var lockedStatuses = new[] { "Giao ĐVVC", "Đang giao", "Đến nơi", "Đã thanh toán", "Hoàn tất" };
                if (lockedStatuses.Contains(order.Status))
                {
                    TempData["Message"] = "Không thể hủy đơn hàng khi đơn đã được xử lý vận chuyển hoặc hoàn tất!";
                    return RedirectToAction(nameof(OrderDetails), new { id = orderId });
                }
            }

            // Hoàn tất phải thanh toán trước
            if (status == "Hoàn tất" && order.Status != "Đã thanh toán")
            {
                TempData["Message"] = "Đơn hàng phải được thanh toán trước khi hoàn tất!";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // Đánh dấu "Đã thanh toán" chỉ khi đơn đã Đến nơi
            if (status == "Đã thanh toán" && order.Status != "Đến nơi")
            {
                TempData["Message"] = "Chỉ có thể xác nhận đã thanh toán khi đơn hàng đã Đến nơi.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            await _orderService.UpdateOrderStatusAsync(orderId, status);
            TempData["Success"] = $"Đơn hàng #{orderId} đã được cập nhật sang trạng thái: {status}";
            return RedirectToAction(nameof(OrderDetails), new { id = orderId });
        }

        /// <summary>Xóa vĩnh viễn đơn hàng (hard delete).</summary>
        [HttpPost]
        public async Task<IActionResult> OrderDelete(int id)
        {
            var success = await _orderService.DeleteOrderAsync(id);
            TempData[success ? "Success" : "Message"] =
                success ? "Đã xóa đơn hàng vĩnh viễn khỏi hệ thống."
                        : "Không thể xóa đơn hàng.";
            return RedirectToAction(nameof(Orders));
        }

        // ================================================================
        // COUPONS
        // NOTE: CouponService chỉ dùng cho validation/apply.
        // CRUD coupon dùng trực tiếp DbContext để linh hoạt hơn.
        // ================================================================

        /// <summary>Danh sách tất cả coupon.</summary>
        public async Task<IActionResult> Coupons()
            => View(await _context.Coupons
                .OrderByDescending(c => c.Id)
                .ToListAsync());

        public IActionResult CouponCreate() => View();

        /// <summary>Tạo coupon — kiểm tra trùng mã trước khi lưu.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CouponCreate(Coupon coupon)
        {
            if (!ModelState.IsValid) return View(coupon);

            var existing = await _couponService.GetByCodeAsync(coupon.Code ?? "");
            if (existing != null)
            {
                ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại.");
                return View(coupon);
            }

            coupon.UsedCount = 0;
            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thêm mã giảm giá thành công!";
            return RedirectToAction(nameof(Coupons));
        }

        public async Task<IActionResult> CouponEdit(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            return View(coupon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CouponEdit(Coupon coupon)
        {
            if (!ModelState.IsValid) return View(coupon);

            _context.Coupons.Update(coupon);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật mã giảm giá thành công!";
            return RedirectToAction(nameof(Coupons));
        }

        [HttpPost]
        public async Task<IActionResult> CouponDelete(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                _context.Coupons.Remove(coupon);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa mã giảm giá thành công!";
            }
            return RedirectToAction(nameof(Coupons));
        }

        // ================================================================
        // CATEGORIES (dùng CategoryService — cache tự động)
        // ================================================================

        public async Task<IActionResult> Categories()
            => View(await _categoryService.GetAllCategoriesAsync());

        public IActionResult CategoryCreate() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryCreate(Category category)
        {
            if (!ModelState.IsValid) return View(category);
            await _categoryService.CreateAsync(category);
            TempData["Success"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        public async Task<IActionResult> CategoryEdit(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryEdit(Category category)
        {
            if (!ModelState.IsValid) return View(category);
            await _categoryService.UpdateAsync(category);
            TempData["Success"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        public async Task<IActionResult> CategoryDelete(int id)
        {
            await _categoryService.DeleteAsync(id);
            TempData["Success"] = "Xóa danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        // ================================================================
        // USERS
        // ================================================================

        /// <summary>Danh sách người dùng — search / phân trang.</summary>
        public async Task<IActionResult> Users(string? search, int page = 1)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    (u.Email    != null && u.Email.Contains(search))       ||
                    (u.FullName != null && u.FullName.Contains(search)));

            int pageSize = 15;
            int total    = await query.CountAsync();
            var paged    = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var userRoles = new Dictionary<string, List<string>>();
            foreach (var u in paged)
                userRoles[u.Id] = (await _userManager.GetRolesAsync(u)).ToList();

            ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search      = search;
            ViewBag.UserRoles   = userRoles;

            return View(paged);
        }

        /// <summary>Chi tiết người dùng — kèm đơn hàng & vai trò.</summary>
        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            ViewBag.Orders  = await _orderService.GetOrdersByUserIdAsync(id);
            ViewBag.Roles   = await _userManager.GetRolesAsync(user);
            ViewBag.AllRoles = (await _userManager.GetRolesAsync(user)).ToList();

            return View(user);
        }

        /// <summary>Thêm / xóa vai trò của người dùng.</summary>
        [HttpPost]
        public async Task<IActionResult> UpdateUserRole(string userId, string role, bool isAdd)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (isAdd)
                await _userManager.AddToRoleAsync(user, role);
            else
                await _userManager.RemoveFromRoleAsync(user, role);

            TempData["Success"] = $"Đã {(isAdd ? "thêm" : "xóa")} vai trò {role} cho người dùng.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Lưu file upload vào wwwroot/uploads/{subFolder}/.
        /// Dùng _env.WebRootPath thay vì Directory.GetCurrentDirectory()
        /// để đảm bảo đúng thư mục gốc web trên mọi môi trường deployment.
        /// </summary>
        private async Task<string> SaveImageFile(IFormFile file, string subFolder)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{subFolder}/{fileName}";
        }
    }

    // ================================================================
    // VIEW MODELS
    // ================================================================
    public class AdminDashboardViewModel
    {
        public int TotalProducts  { get; set; }
        public int TotalOrders    { get; set; }
        public int TotalUsers    { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingOrders    { get; set; }
        public List<Order>   RecentOrders     { get; set; } = new();
        public List<Product> LowStockProducts { get; set; } = new();
        public Dictionary<string, decimal> MonthlyRevenue { get; set; } = new();
    }
}
