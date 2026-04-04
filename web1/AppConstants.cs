// ================================================================
// AppConstants - Cache/Session keys dùng chung toàn app
// Đặt ở đây thay vì trong Program.cs để tránh xung đột với
// top-level statements (C# 10 / .NET 6+)
// ================================================================
public static class AppConstants
{
    // Cache keys — dùng cho IMemoryCache (30 phút)
    public const string CategoriesCacheKey = "ProductCategories";

    // Session keys — dùng cho HttpContext.Session
    public const string CartSessionKey   = "Cart";
    public const string CouponSessionKey = "AppliedCoupon";
}
