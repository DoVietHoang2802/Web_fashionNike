// ================================================================
// Program.cs - Entry point (điểm khởi đầu) của ứng dụng web1 (Fashion Store)
//
// File này chạy ĐẦU TIÊN khi ứng dụng khởi động.
// Chịu trách nhiệm:
//   1. Đăng ký tất cả dịch vụ (Services) vào DI Container
//   2. Cấu hình Middleware (chuỗi xử lý request)
//   3. Seed data (tạo dữ liệu mặc định lần đầu)
//   4. Khởi chạy ứng dụng
//
// Keys dùng chung: xem AppConstants.cs
// ================================================================

// builder: đối tượng chứa toàn bộ cấu hình ứng dụng
// CreateBuilder() khởi tạo WebApplicationBuilder - đọc config từ appsettings.json
var builder = WebApplication.CreateBuilder(args);

// ================================================================
// 1. SERVICES CONTAINER - Đăng ký dịch vụ vào DI Container
//
// DI Container là nơi quản lý "sự phụ thuộc" giữa các class.
// Khi Controller cần dùng Service, ASP.NET Core tự động inject vào constructor.
// ================================================================

// AddControllersWithViews: đăng ký MVC (Controller + View)
// AddRazorRuntimeCompilation: cho phép sửa View (.cshtml) mà không cần restart server (dev only)
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

// AddMemoryCache: đăng ký bộ nhớ đệm (dùng cho cache danh mục, session...)
builder.Services.AddMemoryCache();


// ── Database (SQL Server) ────────────────────────────────────────
// AddDbContext: đăng ký ApplicationDbContext vào DI Container
// ApplicationDbContext quản lý kết nối và thao tác với SQL Server
// UseSqlServer: kết nối đến SQL Server qua connection string trong appsettings.json
// CommandTimeout(60): query tối đa 60 giây (tránh treo lâu)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(60)
    )
);


// ── ASP.NET Identity ─────────────────────────────────────────────
// AddIdentity: đăng ký hệ thống xác thực & phân quyền người dùng
// Tham số:
//   ApplicationUser: class user tùy chỉnh (thêm FullName, Address...)
//   IdentityRole: class role mặc định (Admin, Customer...)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Cấu hình chính sách password:
    options.Password.RequireDigit = true;          // Phải có số
    options.Password.RequireLowercase = true;       // Phải có chữ thường
    options.Password.RequireUppercase = false;     // Không bắt buộc chữ hoa
    options.Password.RequireNonAlphanumeric = false; // Không cần ký tự đặc biệt (@#$...)
    options.Password.RequiredLength = 6;          // Tối thiểu 6 ký tự
    options.User.RequireUniqueEmail = true;         // Email phải duy nhất (không trùng)
})
// AddEntityFrameworkStores: lưu Identity data (user, role) vào DB qua EF Core
.AddEntityFrameworkStores<ApplicationDbContext>()
// AddDefaultTokenProviders: tạo token cho reset password, xác nhận email...
.AddDefaultTokenProviders();


// ── Business Services ───────────────────────────────────────────
// AddScoped: đăng ký service vào DI Container với vòng đời "Scoped"
//
// Scoped = tạo MỚI cho MỖI request HTTP, dùng CHUNG trong request đó
// Ví dụ: Request A gọi CartController -> tạo 1 ProductService
//        Request B gọi CartController -> tạo 1 ProductService KHÁC
//
// Các vòng đời khác:
//   Singleton: tạo 1 lần, dùng chung toàn bộ app (không thread-safe)
//   Transient: tạo MỚI mỗi lần được inject (tốn bộ nhớ)
//   Scoped: vừa đủ, dùng phổ biến nhất (đây là lựa chọn đúng)
builder.Services.AddScoped<ProductService>();    // Xử lý sản phẩm
builder.Services.AddScoped<OrderService>();     // Xử lý đơn hàng
builder.Services.AddScoped<CouponService>();    // Xử lý mã giảm giá
builder.Services.AddScoped<CategoryService>();  // Xử lý danh mục (có cache)
builder.Services.AddScoped<WishlistService>();   // Xử lý wishlist


// ── Session ──────────────────────────────────────────────────────
// Session: lưu dữ liệu tạm trong phiên làm việc của user (giỏ hàng, coupon...)
//
// AddDistributedMemoryCache: lưu session data vào RAM của server
//   → Nhanh, đơn giản
//   → → Mất khi server restart
//   → → Không chia sẻ được giữa nhiều server (load balancer)
//
// Các cách lưu session khác:
//   Redis: AddStackExchangeRedisCache (nhanh, chia sẻ được)
//   SQL Server: AddDistributedSqlServerCache (lưu lâu dài)
//   Cookie: AddCookie (không tốn RAM, giới hạn ~4KB)
builder.Services.AddDistributedMemoryCache();

// Cấu hình session cookie (lưu trên trình duyệt user)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // Hết hạng sau 30 phút không hoạt động
    options.Cookie.HttpOnly = true;                  // Cookie không đọc được bằng JavaScript (bảo mật)
    options.Cookie.IsEssential = true;               // Cookie bắt buộc - không thể từ chối (GDPR)
});


// ── Cookie settings (Identity) ───────────────────────────────────
// Cấu hình cookie đăng nhập của ASP.NET Identity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";       // Redirect đến trang login khi chưa đăng nhập
    options.LogoutPath = "/Account/Logout";      // Đường dẫn đăng xuất
    options.AccessDeniedPath = "/Account/AccessDenied"; // Trang từ chối truy cập (không đủ quyền)
    options.ExpireTimeSpan = TimeSpan.FromDays(7);     // Cookie đăng nhập sống 7 ngày
});


// ================================================================
// 2. MIDDLEWARE PIPELINE - Chuỗi xử lý request HTTP
//
// Middleware là các "bước" xử lý request theo thứ tự.
// Mỗi middleware có thể:
//   - Xử lý request trước khi chuyển tiếp
//   - Xử lý response sau khi các bước sau hoàn thành
//   - Ngắn chặn (không chuyển tiếp) nếu đã xử lý xong
//
// THỨ TỰ RẤT QUAN TRỌNG - sai thứ tự có thể gây lỗi bảo mật
// ================================================================
var app = builder.Build();  // build ra đối tượng app (WebApplication)

// Bước A: Xử lý lỗi
if (!app.Environment.IsDevelopment())  // Chỉ bật trong Production
{
    // Hiển thị trang lỗi chung thay vì crash trang (che thông tin nhạy cảm)
    app.UseExceptionHandler("/Home/Error");

    // HSTS: bắt buộc dùng HTTPS (Production)
    app.UseHsts();
}

// Bước B: Chuyển HTTP sang HTTPS (Production)
app.UseHttpsRedirection();

// Bước C: Đọc file tĩnh từ wwwroot (CSS, JS, hình ảnh, favicon...)
app.UseStaticFiles();

// Bước D: Cấu hình routing (định tuyến URL -> Controller/Action)
app.UseRouting();

// Bước E: Xác thực người dùng (đọc cookie đăng nhập, token...)
// Sau bước này, User.IsAuthenticated sẽ có giá trị
app.UseAuthentication();

// Bước F: Kiểm tra quyền (Authorize attribute sẽ hoạt động)
app.UseAuthorization();

// Bước G: Kích hoạt Session (đọc/ghi session data)
app.UseSession();

// Bước H: Ánh xạ URL -> Controller/Action
// pattern: "{controller=Home}/{action=Index}/{id?}"
// Ví dụ: /Products/Details/5 -> ProductsController.Details(5)
//        /Admin               -> AdminController.Index()
//        /                    -> HomeController.Index()
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// ================================================================
// 3. SEED DATA - Tạo dữ liệu mặc định khi khởi động lần đầu
//
// using (var scope = app.Services.CreateScope()):
//   Tạo một "phạm vi" để lấy các service từ DI Container
//   Tự động giải phóng khi kết thúc (using block)
//
// Chạy khi:
//   - Lần đầu khởi động app (chưa có role/admin)
//   - Không chạy lại khi đã có dữ liệu (vì có kiểm tra tồn tại)
// ================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Lấy các service cần thiết từ DI Container
        var context     = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Đặt timeout cho tất cả query EF Core trong seed: 30 giây
        context.Database.SetCommandTimeout(30);

        // ── Tạo vai trò Admin nếu chưa có ───────────────────────────
        // RoleExistsAsync: kiểm tra role đã tồn tại chưa
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            // CreateAsync: tạo role mới trong bảng AspNetRoles
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // ── Tạo tài khoản admin mặc định nếu chưa có ───────────────
        var adminEmail = "admin@fashionstore.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            // Tạo ApplicationUser mới
            var user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Quản trị viên",
                EmailConfirmed = true  // Email đã xác nhận (không cần gửi mail)
            };

            // CreateAsync: tạo user + hash password + lưu vào AspNetUsers
            // Password: "Admin@123" (đã set chính sách ở trên)
            var createResult = await userManager.CreateAsync(user, "Admin@123");

            if (createResult.Succeeded)
            {
                // AddToRoleAsync: gán role "Admin" cho user này
                await userManager.AddToRoleAsync(user, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        // Nếu seed thất bại -> ghi log để debug
        Console.WriteLine(">>> ERROR SEEDING: " + ex.Message);

        // ILogger: ghi log theo chuẩn (có thể xuất ra file, console, monitoring...)
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu.");
    }
    // NOTE: Seed sản phẩm/coupon đã chuyển sang SQL script, không chạy ở đây.
}

// ================================================================
// 4. CHẠY ỨNG DỤNG
// ================================================================

// app.Run(): khởi chạy web server (Kestrel)
// Sau dòng này, ứng dụng bắt đầu lắng nghe HTTP request
app.Run();

// NOTE: Code sau app.Run() sẽ KHÔNG bao giờ chạy (vì Run() là blocking call)
