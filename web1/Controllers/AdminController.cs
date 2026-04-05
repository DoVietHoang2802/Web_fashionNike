// ================================================================
// AdminController - Trang quản trị (yêu cầu vai trò Admin)
//
// Quy tắc:
//   • Tất cả actions đều có [Authorize(Roles = "Admin")].
///  • Sử dụng ASP.NET Core Identity + Entity Framework Core.
//   • CRUD coupon dùng trực tiếp _context (CouponService chỉ phục vụ validation).
//   • Image upload: dùng IWebHostEnvironment.WebRootPath thay vì Directory.GetCurrentDirectory().
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
    /// <summary>
    /// Controller quản trị hệ thống - chỉ user có role "Admin" mới được truy cập.
    /// Quản lý: Dashboard, Sản phẩm, Đơn hàng, Mã giảm giá, Danh mục, Người dùng.
    /// Phân biệt: dùng Service cho Product/Order/Coupon/Category để tách logic nghiệp vụ ra khỏi Controller.
    /// </summary>
    [Authorize(Roles = "Admin")]  // Mọi action trong controller này đều yêu cầu quyền Admin
    public class AdminController : Controller
    {
        // ApplicationDbContext: truy cập database trực tiếp qua EF Core
        // Dùng cho: Coupon CRUD (trực tiếp), đếm/sum thống kê, user query
        private readonly ApplicationDbContext _context;

        // ProductService: xử lý nghiệp vụ sản phẩm (Create, Update, Delete, GetAll, GetById)
        private readonly ProductService       _productService;

        // OrderService: xử lý nghiệp vụ đơn hàng (CRUD, lọc, cập nhật trạng thái)
        private readonly OrderService        _orderService;

        // CouponService: chỉ dùng để validate/áp mã coupon (GetByCodeAsync)
        // CRUD coupon dùng trực tiếp _context để linh hoạt hơn
        private readonly CouponService        _couponService;

        // CategoryService: xử lý CRUD danh mục (có cache tự động)
        private readonly CategoryService      _categoryService;

        // UserManager: quản lý user (search, lấy roles, thêm/xóa role)
        private readonly UserManager<ApplicationUser> _userManager;

        // IWebHostEnvironment: truy cập thư mục gốc web (wwwroot) để lưu file upload
        private readonly IWebHostEnvironment _env;

        // ================================================================
        // CONSTRUCTOR - Tiêm tất cả dependency cần thiết
        // ================================================================
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
        // DASHBOARD - Trang tổng quan quản trị
        // ================================================================

        /// <summary>
        /// GET: /Admin
        /// Hiển thị thống kê tổng quan:
        ///   - Tổng sản phẩm, đơn hàng, người dùng, doanh thu
        ///   - Số đơn hàng đang chờ xác nhận
        ///   - Danh sách đơn hàng gần đây
        ///   - Sản phẩm tồn kho thấp (Stock < 20)
        ///   - Doanh thu theo tháng (6 tháng gần nhất)
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Lọc đơn hàng 6 tháng gần nhất, loại trừ đơn đã hủy
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var orders = await _context.Orders
                .Where(o => o.OrderDate >= sixMonthsAgo && o.Status != "Đã hủy")
                .ToListAsync();

            // Tính doanh thu theo tháng bằng LINQ to Object
            // (vì ToListAsync đã load data lên memory)
            var monthlyRevenue = orders
                .GroupBy(o => o.OrderDate?.ToString("MM/yyyy") ?? "N/A")  // Nhóm theo "MM/yyyy"
                .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount ?? 0));  // Sum cho mỗi nhóm

            // Build ViewModel để truyền sang View
            var dashboard = new AdminDashboardViewModel
            {
                TotalProducts  = await _context.Products.CountAsync(),
                TotalOrders    = await _context.Orders.CountAsync(),
                TotalUsers    = await _userManager.Users.CountAsync(),
                // Tổng doanh thu: chỉ tính đơn không bị hủy, SumAsync trảnh decimal?
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
        // PRODUCTS - Quản lý sản phẩm
        // ================================================================

        /// <summary>
        /// GET: /Admin/Products
        /// Danh sách sản phẩm với bộ lọc:
        ///   - search: tìm theo tên sản phẩm (không phân biệt hoa thường)
        ///   - categoryId: lọc theo danh mục
        ///   - lowStock: lọc sản phẩm tồn kho thấp (Stock < 20)
        ///   - Phân trang: mỗi trang 10 sản phẩm
        /// </summary>
        /// <param name="search">Từ khóa tìm kiếm theo tên</param>
        /// <param name="categoryId">Lọc theo danh mục (null = tất cả)</param>
        /// <param name="lowStock">true = chỉ hiện sản phẩm tồn kho thấp</param>
        /// <param name="page">Số trang hiện tại (default = 1)</param>
        public async Task<IActionResult> Products(string? search, int? categoryId, bool? lowStock, int page = 1)
        {
            // Lấy tất cả sản phẩm (ProductService đã bao gồm Include Category)
            var products = await _productService.GetAllProductsAsync();

            // Bước 1: Lọc theo từ khóa tìm kiếm (Contains với OrdinalIgnoreCase)
            if (!string.IsNullOrEmpty(search))
                products = products
                    .Where(p => p.Name != null && p.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Bước 2: Lọc theo danh mục
            if (categoryId.HasValue)
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();

            // Bước 3: Lọc sản phẩm tồn kho thấp
            if (lowStock == true)
                products = products.Where(p => p.Stock < 20).ToList();

            // Bước 4: Phân trang (Skip/Take của LINQ)
            int pageSize = 10;
            var paged = products.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Truyền dữ liệu phân trang + filter sang View qua ViewBag
            ViewBag.TotalPages  = (int)Math.Ceiling(products.Count / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search      = search;
            ViewBag.CategoryId  = categoryId;
            ViewBag.LowStock    = lowStock;
            ViewBag.Categories  = await _categoryService.GetAllCategoriesAsync();

            return View(paged);
        }

        /// <summary>
        /// GET: /Admin/ProductCreate - Form thêm sản phẩm mới
        /// Load danh sách danh mục để user chọn khi tạo sản phẩm.
        /// </summary>
        public async Task<IActionResult> ProductCreate()
        {
            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
            return View();
        }

        /// <summary>
        /// POST: /Admin/ProductCreate - Tạo sản phẩm mới
        /// Quy trình:
        ///   1. Validate giá > 0 (ModelState.Remove để bỏ auto-validation không cần thiết)
        ///   2. Upload ảnh nếu có (SaveImageFile) > dùng URL nhập tay > placeholder null
        ///   3. Gán CreatedDate = DateTime.Now
        ///   4. Gọi ProductService.CreateProductAsync
        /// </summary>
        /// <param name="product">Dữ liệu sản phẩm từ form</param>
        /// <param name="ImageFile">File ảnh upload (IFormFile)</param>
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống CSRF - bắt buộc cho mọi form POST
        public async Task<IActionResult> ProductCreate(Product product, IFormFile? ImageFile)
        {
            // Bỏ validation tự động của model binding cho Price
            // (vì Price là decimal? nên cần validate thủ công bên dưới)
            ModelState.Remove("Price");
            if (product.Price <= 0)
                ModelState.AddModelError("Price", "Giá sản phẩm phải lớn hơn 0");

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
                return View(product);
            }

            // Ưu tiên 1: Upload file ảnh mới (nếu có chọn file)
            if (ImageFile != null && ImageFile.Length > 0)
                product.ImageUrl = await SaveImageFile(ImageFile, "products");

            // Ưu tiên 2: Dùng URL nhập tay (nếu không upload file nhưng có nhập URL)
            else if (string.IsNullOrWhiteSpace(product.ImageUrl))
                product.ImageUrl = null;  // Ưu tiên 3: không có ảnh

            product.CreatedDate = DateTime.Now;
            await _productService.CreateProductAsync(product);

            TempData["Success"] = "Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        /// <summary>
        /// GET: /Admin/ProductEdit/{id} - Form chỉnh sửa sản phẩm
        /// Lấy sản phẩm theo id, nếu không tồn tại -> 404.
        /// </summary>
        public async Task<IActionResult> ProductEdit(int id)
        {
            var product = await _productService.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            ViewBag.Categories = await _categoryService.GetAllCategoriesAsync();
            return View(product);
        }

        /// <summary>
        /// POST: /Admin/ProductEdit - Cập nhật sản phẩm
        /// Luồng xử lý:
        ///   1. Validate giá > 0
        ///   2. Lấy sản phẩm hiện tại từ DB (để giữ nguyên các trường không sửa)
        ///   3. Cập nhật trường cơ bản: Name, Description, Price, CategoryId, Stock
        ///   4. Ưu tiên upload file > URL nhập tay > giữ nguyên ảnh cũ
        ///   5. Rating/ReviewCount/SoldCount/CreatedDate được giữ nguyên (không cho sửa)
        /// </summary>
        /// <param name="product">Dữ liệu cập nhật từ form</param>
        /// <param name="ImageFile">File ảnh upload mới (optional)</param>
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

            // Lấy sản phẩm hiện tại từ DB để merge (EF Core tracking)
            var existing = await _productService.GetProductByIdAsync(product.Id);
            if (existing == null) return NotFound();

            // Cập nhật các trường được phép sửa
            existing.Name        = product.Name;
            existing.Description = product.Description;
            existing.Price       = product.Price;
            existing.CategoryId  = product.CategoryId;
            existing.Stock       = product.Stock;

            // Ưu tiên 1: upload file mới (thay thế ảnh cũ)
            if (ImageFile != null && ImageFile.Length > 0)
                existing.ImageUrl = await SaveImageFile(ImageFile, "products");

            // Ưu tiên 2: cập nhật URL nhập tay (nếu người dùng nhập)
            else if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                existing.ImageUrl = product.ImageUrl.Trim();

            // Ưu tiên 3: giữ nguyên ảnh cũ (cả 2 đều trống)

            await _productService.UpdateProductAsync(existing);
            TempData["Success"] = "Cập nhật sản phẩm thành công!";

            return RedirectToAction(nameof(Products));
        }

        /// <summary>
        /// POST: /Admin/ProductDelete/{id} - Xóa sản phẩm
        /// Gọi ProductService.DeleteProductAsync (xóa luôn OrderItems liên quan).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ProductDelete(int id)
        {
            await _productService.DeleteProductAsync(id);
            TempData["Success"] = "Xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Products));
        }

        // ================================================================
        // ORDERS - Quản lý đơn hàng
        // ================================================================

        /// <summary>
        /// GET: /Admin/Orders
        /// Danh sách đơn hàng với bộ lọc:
        ///   - status: lọc theo trạng thái đơn hàng
        ///   - search: tìm theo mã đơh / tên khách hàng (tùy implementation OrderService)
        ///   - Phân trang: mỗi trang 15 đơn hàng
        /// </summary>
        public async Task<IActionResult> Orders(string? status, string? search, int page = 1)
        {
            int pageSize = 15;
            // OrderService trả về tuple: (danh sách đơn, tổng số)
            var (orders, totalCount) = await _orderService
                .GetFilteredOrdersAsync(status, search, page, pageSize);

            ViewBag.TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Status      = status;
            ViewBag.Search      = search;
            // Danh sách trạng thái để hiển thị dropdown filter
            ViewBag.StatusList   = new[]
                { "Chờ xác nhận", "Đã xác nhận", "Giao ĐVVC", "Đang giao", "Hoàn tất", "Đã hủy" };

            return View(orders);
        }

        /// <summary>
        /// GET: /Admin/OrderDetails/{id} - Chi tiết đơn hàng
        /// Lấy đơn hàng + danh sách OrderItems để hiển thị chi tiết.
        /// </summary>
        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        /// <summary>
        /// POST: /Admin/UpdateOrderStatus - Cập nhật trạng thái đơn hàng
        /// Các quy tắc chuyển trạng thái:
        ///   1. Không cho thay đổi đơn "Hoàn tất" hoặc "Đã hủy" (trạng thái kết thúc)
        ///   2. Không cho hủy đơn đã qua giai đoạn "Giao ĐVVC"
        ///   3. "Hoàn tất" chỉ khi đơn đã ở trạng thái "Đã thanh toán"
        ///   4. "Đã thanh toán" chỉ khi đơn đã "Đến nơi"
        /// NOTE: Logic cập nhật tồn kho (hoàn / giảm) nằm TRONG OrderService.
        /// </summary>
        /// <param name="orderId">ID đơn hàng cần cập nhật</param>
        /// <param name="status">Trạng thái mới muốn chuyển sang</param>
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null) return NotFound();

            // Quy tắc 1: Đơn đã kết thúc (Hoàn tất / Đã hủy) -> không cho sửa
            if (order.Status == "Hoàn tất" || order.Status == "Đã hủy")
            {
                TempData["Message"] = "Đơn hàng đã kết thúc, không thể thay đổi trạng thái.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // Quy tắc 2: Không cho hủy đơn đã vận chuyển
            if (status == "Đã hủy")
            {
                var lockedStatuses = new[] { "Giao ĐVVC", "Đang giao", "Đến nơi", "Đã thanh toán", "Hoàn tất" };
                if (lockedStatuses.Contains(order.Status))
                {
                    TempData["Message"] = "Không thể hủy đơn hàng khi đơn đã được xử lý vận chuyển hoặc hoàn tất!";
                    return RedirectToAction(nameof(OrderDetails), new { id = orderId });
                }
            }

            // Quy tắc 3: "Hoàn tất" phải thanh toán trước
            if (status == "Hoàn tất" && order.Status != "Đã thanh toán")
            {
                TempData["Message"] = "Đơn hàng phải được thanh toán trước khi hoàn tất!";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // Quy tắc 4: "Đã thanh toán" chỉ khi đơn đã "Đến nơi"
            if (status == "Đã thanh toán" && order.Status != "Đến nơi")
            {
                TempData["Message"] = "Chỉ có thể xác nhận đã thanh toán khi đơn hàng đã Đến nơi.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            // Gọi OrderService để xử lý (cập nhật DB + logic tồn kho)
            await _orderService.UpdateOrderStatusAsync(orderId, status);
            TempData["Success"] = $"Đơn hàng #{orderId} đã được cập nhật sang trạng thái: {status}";
            return RedirectToAction(nameof(OrderDetails), new { id = orderId });
        }

        /// <summary>
        /// POST: /Admin/OrderDelete/{id} - Xóa vĩnh viễn đơn hàng (hard delete)
        /// Gọi OrderService.DeleteOrderAsync.
        /// </summary>
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
        // COUPONS - Quản lý mã giảm giá
        //
        // NOTE: CouponService chỉ dùng cho validation/apply.
        // CRUD coupon dùng trực tiếp DbContext để linh hoạt hơn
        // (ví dụ: lấy toàn bộ danh sách, cập nhật nhanh mà không cần map DTO).
        // ================================================================

        /// <summary>
        /// GET: /Admin/Coupons - Danh sách tất cả coupon
        /// Sắp xếp theo Id giảm dần (mới nhất lên đầu).
        /// </summary>
        public async Task<IActionResult> Coupons()
            => View(await _context.Coupons
                .OrderByDescending(c => c.Id)
                .ToListAsync());

        /// <summary>GET: /Admin/CouponCreate - Form tạo mã giảm giá</summary>
        public IActionResult CouponCreate() => View();

        /// <summary>
        /// POST: /Admin/CouponCreate - Tạo coupon mới
        /// Quy tắc: mã coupon (Code) không được trùng với coupon đã có.
        /// </summary>
        /// <param name="coupon">Dữ liệu coupon từ form</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CouponCreate(Coupon coupon)
        {
            if (!ModelState.IsValid) return View(coupon);

            // Kiểm tra mã coupon chưa tồn tại (để tránh trùng lặp)
            var existing = await _couponService.GetByCodeAsync(coupon.Code ?? "");
            if (existing != null)
            {
                ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại.");
                return View(coupon);
            }

            // UsedCount = 0: coupon mới chưa được ai sử dụng
            coupon.UsedCount = 0;

            // Dùng DbContext trực tiếp thay vì service
            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thêm mã giảm giá thành công!";
            return RedirectToAction(nameof(Coupons));
        }

        /// <summary>GET: /Admin/CouponEdit/{id} - Form chỉnh sửa coupon</summary>
        public async Task<IActionResult> CouponEdit(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            return View(coupon);
        }

        /// <summary>
        /// POST: /Admin/CouponEdit - Cập nhật coupon
        /// Dùng _context.Coupons.Update() để đánh dấu entity đã sửa.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CouponEdit(Coupon coupon)
        {
            if (!ModelState.IsValid) return View(coupon);

            _context.Coupons.Update(coupon);  // EF Core sẽ track entity này
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật mã giảm giá thành công!";
            return RedirectToAction(nameof(Coupons));
        }

        /// <summary>POST: /Admin/CouponDelete/{id} - Xóa coupon</summary>
        [HttpPost]
        public async Task<IActionResult> CouponDelete(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                _context.Coupons.Remove(coupon);  // Đánh dấu entity để xóa
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa mã giảm giá thành công!";
            }
            return RedirectToAction(nameof(Coupons));
        }

        // ================================================================
        // CATEGORIES - Quản lý danh mục sản phẩm
        // Dùng CategoryService (có cache tự động) để tối ưu hiệu năng
        // ================================================================

        /// <summary>GET: /Admin/Categories - Danh sách danh mục</summary>
        public async Task<IActionResult> Categories()
            => View(await _categoryService.GetAllCategoriesAsync());

        /// <summary>GET: /Admin/CategoryCreate - Form tạo danh mục</summary>
        public IActionResult CategoryCreate() => View();

        /// <summary>POST: /Admin/CategoryCreate - Tạo danh mục mới</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryCreate(Category category)
        {
            if (!ModelState.IsValid) return View(category);
            await _categoryService.CreateAsync(category);
            TempData["Success"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        /// <summary>GET: /Admin/CategoryEdit/{id} - Form chỉnh sửa danh mục</summary>
        public async Task<IActionResult> CategoryEdit(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        /// <summary>POST: /Admin/CategoryEdit - Cập nhật danh mục</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CategoryEdit(Category category)
        {
            if (!ModelState.IsValid) return View(category);
            await _categoryService.UpdateAsync(category);
            TempData["Success"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        /// <summary>POST: /Admin/CategoryDelete/{id} - Xóa danh mục</summary>
        [HttpPost]
        public async Task<IActionResult> CategoryDelete(int id)
        {
            await _categoryService.DeleteAsync(id);
            TempData["Success"] = "Xóa danh mục thành công!";
            return RedirectToAction(nameof(Categories));
        }

        // ================================================================
        // USERS - Quản lý người dùng
        // ================================================================

        /// <summary>
        /// GET: /Admin/Users
        /// Danh sách người dùng với search + phân trang.
        /// Search được: UserName, Email, FullName.
        /// Kèm danh sách roles của mỗi user để hiển thị badge.
        /// </summary>
        /// <param name="search">Từ khóa tìm kiếm</param>
        /// <param name="page">Số trang (default = 1)</param>
        public async Task<IActionResult> Users(string? search, int page = 1)
        {
            // Bắt đầu query từ UserManager ( AspNetUsers table)
            var query = _userManager.Users.AsQueryable();

            // Lọc theo từ khóa (UserName OR Email OR FullName)
            if (!string.IsNullOrEmpty(search))
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    (u.Email    != null && u.Email.Contains(search))       ||
                    (u.FullName != null && u.FullName.Contains(search)));

            // Phân trang
            int pageSize = 15;
            int total    = await query.CountAsync();
            var paged    = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Lấy danh sách roles cho từng user (vì UserManager không include sẵn)
            var userRoles = new Dictionary<string, List<string>>();
            foreach (var u in paged)
                userRoles[u.Id] = (await _userManager.GetRolesAsync(u)).ToList();

            ViewBag.TotalPages  = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search      = search;
            ViewBag.UserRoles   = userRoles;  // Dictionary để View tra cứu nhanh

            return View(paged);
        }

        /// <summary>
        /// GET: /Admin/UserDetails/{id} - Chi tiết người dùng
        /// Hiển thị: thông tin user + danh sách đơn hàng + vai trò hiện tại.
        /// </summary>
        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Lấy đơn hàng của user này
            ViewBag.Orders  = await _orderService.GetOrdersByUserIdAsync(id);

            // Lấy danh sách roles hiện tại của user
            ViewBag.Roles   = await _userManager.GetRolesAsync(user);
            ViewBag.AllRoles = (await _userManager.GetRolesAsync(user)).ToList();

            return View(user);
        }

        /// <summary>
        /// POST: /Admin/UpdateUserRole - Thêm hoặc xóa vai trò của người dùng
        /// Ví dụ: thêm vai trò "Admin", xóa vai trò "Customer"...
        /// </summary>
        /// <param name="userId">ID người dùng</param>
        /// <param name="role">Tên vai trò muốn thêm/xóa</param>
        /// <param name="isAdd">true = thêm role, false = xóa role</param>
        [HttpPost]
        public async Task<IActionResult> UpdateUserRole(string userId, string role, bool isAdd)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (isAdd)
                await _userManager.AddToRoleAsync(user, role);   // Thêm role mới
            else
                await _userManager.RemoveFromRoleAsync(user, role);  // Xóa role

            TempData["Success"] = $"Đã {(isAdd ? "thêm" : "xóa")} vai trò {role} cho người dùng.";
            return RedirectToAction(nameof(UserDetails), new { id = userId });
        }

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Lưu file upload vào thư mục wwwroot/uploads/{subFolder}/
        ///
        /// Tại sao dùng _env.WebRootPath thay vì Directory.GetCurrentDirectory()?
        ///   - WebRootPath: luôn trỏ đúng thư mục wwwroot của web app
        ///   - Directory.GetCurrentDirectory(): trả về thư mục hiện tại của process
        ///     (có thể khác khi deploy trên IIS, Docker, Linux...)
        ///
        /// Tên file: Guid.NewGuid() + extension gốc (đảm bảo DUY NHẤT tuyệt đối)
        /// Đường dẫn trả về dạng: /uploads/{subFolder}/{fileName}
        /// </summary>
        /// <param name="file">File upload từ form (IFormFile)</param>
        /// <param name="subFolder">Thư mục con trong wwwroot/uploads/ (ví dụ: "products")</param>
        /// <returns>Đường dẫn tương đối để lưu vào DB (VD: "/uploads/products/abc.jpg")</returns>
        private async Task<string> SaveImageFile(IFormFile file, string subFolder)
        {
            // Xây dựng đường dẫn tuyệt đối: wwwroot/uploads/{subFolder}
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", subFolder);
            Directory.CreateDirectory(uploadsDir);  // Tạo thư mục nếu chưa tồn tại

            // Tạo tên file duy nhất bằng GUID + giữ nguyên extension gốc
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Copy file vào đường dẫn đích bằng FileStream (async để không block thread)
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Trả về đường dẫn tương đối để lưu vào DB và dùng trong <img src="...">
            return $"/uploads/{subFolder}/{fileName}";
        }
    }

    // ================================================================
    // VIEW MODELS - Các class model dùng riêng cho Admin (không shared)
    // ================================================================

    /// <summary>
    /// ViewModel cho trang Dashboard (Admin/Index).
    /// Gom tất cả số liệu thống kê cần hiển thị trên 1 trang.
    /// </summary>
    public class AdminDashboardViewModel
    {
        public int TotalProducts  { get; set; }      // Tổng số sản phẩm
        public int TotalOrders    { get; set; }      // Tổng số đơn hàng
        public int TotalUsers    { get; set; }      // Tổng số người dùng
        public decimal TotalRevenue { get; set; }   // Tổng doanh thu (chỉ tính đơn không hủy)
        public int PendingOrders    { get; set; }    // Số đơn đang chờ xác nhận
        public List<Order>   RecentOrders     { get; set; } = new();   // Đơn hàng gần đây
        public List<Product> LowStockProducts { get; set; } = new();   // Sản phẩm sắp hết hàng
        public Dictionary<string, decimal> MonthlyRevenue { get; set; } = new();  // Doanh thu theo tháng
    }
}
