// ================================================================
// CouponService - Quản lý & xác thực mã giảm giá
//
// ValidateCouponAsync kiểm tra tuần tự:
//   1. Tồn tại? → 2. Active? → 3. Trong thời gian hiệu lực?
//   → 4. Còn lượt? → 5. Đơn tối thiểu đạt? → Tính discount
// ================================================================
using Microsoft.EntityFrameworkCore;
using web1.Data;

namespace web1.Models
{
    // ================================================================
    // CouponValidationResult - Kết quả xác thực coupon
    // ================================================================
    public class CouponValidationResult
    {
        public bool        IsValid         { get; set; }
        public string      Message         { get; set; } = string.Empty;
        public Coupon?     Coupon          { get; set; }
        public decimal     DiscountAmount  { get; set; }
    }

    // ================================================================
    // CouponService
    // ================================================================
    public class CouponService
    {
        private readonly ApplicationDbContext _context;

        public CouponService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Tìm coupon theo mã (không phân biệt hoa/thường).</summary>
        public async Task<Coupon?> GetByCodeAsync(string code)
        {
            var lowerCode = code.ToLowerInvariant();
            var all = await _context.Coupons.ToListAsync();
            return all.FirstOrDefault(c =>
                c.Code != null && c.Code.ToLowerInvariant() == lowerCode);
        }

        /// <summary>
        /// Xác thực coupon theo tuần tự các điều kiện.
        /// Mỗi điều kiện fail → trả về ngay với Message cụ thể.
        /// </summary>
        public async Task<CouponValidationResult> ValidateCouponAsync(string code, decimal orderAmount)
        {
            var coupon = await GetByCodeAsync(code);

            if (coupon == null)
                return Fail("Mã giảm giá không tồn tại.");

            if (!coupon.IsActive)
                return Fail("Mã giảm giá hiện không khả dụng.");

            if (coupon.StartDate > DateTime.Now)
                return Fail("Chương trình giảm giá chưa bắt đầu.");

            if (coupon.ExpiredDate.HasValue && DateTime.Now > coupon.ExpiredDate.Value)
                return Fail("Mã giảm giá đã hết hạn.");

            if (coupon.MaxUsageCount.HasValue && coupon.UsedCount >= coupon.MaxUsageCount.Value)
                return Fail("Mã giảm giá đã hết lượt sử dụng.");

            if (coupon.MinOrderAmount.HasValue && orderAmount < coupon.MinOrderAmount.Value)
            {
                var min = coupon.MinOrderAmount.Value.ToString("N0") + "đ";
                return Fail($"Đơn hàng tối thiểu {min} để áp dụng mã này.");
            }

            decimal discount = coupon.CalculateDiscount(orderAmount);
            return new CouponValidationResult
            {
                IsValid        = true,
                Message        = "Áp dụng mã giảm giá thành công.",
                Coupon         = coupon,
                DiscountAmount = discount
            };
        }

        /// <summary>Tăng UsedCount sau mỗi đơn hàng thành công.</summary>
        public async Task IncrementUsageAsync(int couponId)
        {
            var coupon = await _context.Coupons.FindAsync(couponId);
            if (coupon != null)
            {
                coupon.UsedCount++;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>Lấy tất cả coupon đang active, mới nhất trước.</summary>
        public async Task<List<Coupon>> GetAllActiveCouponsAsync()
            => await _context.Coupons
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================
        private static CouponValidationResult Fail(string message)
            => new() { IsValid = false, Message = message };
    }
}
