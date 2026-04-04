using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using web1.Data;
using web1.Models;

// ================================================================
// Program.cs - Entry point của ứng dụng web1 (Fashion Store)
// Keys dùng chung: xem AppConstants.cs
// ================================================================

var builder = WebApplication.CreateBuilder(args);

// ================================================================
// 1. SERVICES CONTAINER
// ================================================================
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddMemoryCache();

// ── Database (SQL Server) ────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.CommandTimeout(60)
    )
);

// ── ASP.NET Identity ─────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Business Services ───────────────────────────────────────────
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CouponService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<WishlistService>();

// ── Session ──────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ── Cookie settings (Identity) ───────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// ================================================================
// 2. MIDDLEWARE PIPELINE
// ================================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ================================================================
// 3. SEED DATA - Chạy 1 lần khi khởi động app
// Tạo vai trò Admin + tài khoản admin mặc định
// ================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context     = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        context.Database.SetCommandTimeout(30);

        // ── Tạo vai trò Admin nếu chưa có ───────────────────────────
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // ── Tạo tài khoản admin mặc định nếu chưa có ───────────────
        var adminEmail = "admin@fashionstore.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Quản trị viên",
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, "Admin@123");
            if (createResult.Succeeded)
                await userManager.AddToRoleAsync(user, "Admin");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(">>> ERROR SEEDING: " + ex.Message);
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Lỗi khi khởi tạo dữ liệu.");
    }
    // NOTE: Seed sản phẩm/coupon đã chuyển sang SQL script, không chạy ở đây.
}

app.Run();
