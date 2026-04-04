// ================================================================
// Coupon - Model mã giảm giá
//
// Entity map với bảng [Coupons] trong SQL Server.
//
// Validate thứ tự (xem CouponService.ValidateCouponAsync):
//   1. Tồn tại? → 2. Active? → 3. Trong thời gian hiệu lực?
//   → 4. Còn lượt? → 5. Đơn tối thiểu? → Tính discount
//
// CalculateDiscount: logic giống ValidateCouponAsync (2 kiểm tra đầu)
//   nhưng trả về số tiền giảm. Dùng khi hiển thị preview.
// ================================================================
using System.ComponentModel.DataAnnotations;

namespace web1.Models
{
    public class Coupon
    {
        /// <summary>PK tự tăng.</summary>
        public int Id { get; set; }

        /// <summary>Mã coupon — unique, không phân biệt hoa/thường khi validate.</summary>
        [StringLength(50)]
        [Display(Name = "Mã giảm giá")]
        public string? Code { get; set; }

        [StringLength(200)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        /// <summary>Percent (%) hoặc FixedAmount (số tiền cố định).</summary>
        [Display(Name = "Loại giảm giá")]
        public DiscountType DiscountType { get; set; } = DiscountType.Percent;

        /// <summary>Giá trị giảm: % nếu Percent, số tiền (VNĐ) nếu FixedAmount.</summary>
        [Display(Name = "Giá trị giảm")]
        public decimal DiscountValue { get; set; }

        /// <summary>Giới hạn số tiền giảm tối đa (áp dụng cho Percent).</summary>
        [Display(Name = "Giảm tối đa")]
        public decimal? MaxDiscount { get; set; }

        /// <summary>Đơn hàng phải đạt tối thiểu mới dùng được coupon.</summary>
        [Display(Name = "Đơn hàng tối thiểu")]
        public decimal? MinOrderAmount { get; set; }

        /// <summary>Số lần sử dụng tối đa. Null = không giới hạn.</summary>
        [Display(Name = "Số lần sử dụng tối đa")]
        public int? MaxUsageCount { get; set; }

        /// <summary>Số lần đã sử dụng — tăng mỗi khi có đơn hàng thành công.</summary>
        [Display(Name = "Đã sử dụng")]
        public int UsedCount { get; set; }

        /// <summary>Ngày bắt đầu hiệu lực.</summary>
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        /// <summary>Ngày hết hạn. Null = không hết hạn.</summary>
        [Display(Name = "Ngày hết hạn")]
        public DateTime? ExpiredDate { get; set; }

        /// <summary>False → coupon bị vô hiệu hóa (bất kể ngày tháng).</summary>
        [Display(Name = "Kích hoạt")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tính số tiền giảm. Ném về 0 nếu không đủ điều kiện.
        /// Dùng để hiển thị preview trước khi xác nhận.
        /// </summary>
        public decimal CalculateDiscount(decimal orderAmount)
        {
            if (!IsActive) return 0;
            if (ExpiredDate.HasValue && DateTime.Now > ExpiredDate.Value) return 0;
            if (StartDate > DateTime.Now) return 0;
            if (MaxUsageCount.HasValue && UsedCount >= MaxUsageCount.Value) return 0;
            if (MinOrderAmount.HasValue && orderAmount < MinOrderAmount.Value) return 0;

            decimal discount = DiscountType == DiscountType.Percent
                ? orderAmount * DiscountValue / 100
                : DiscountValue;

            if (MaxDiscount.HasValue && discount > MaxDiscount.Value)
                discount = MaxDiscount.Value;

            return discount;
        }
    }

    /// <summary>Loại giảm giá.</summary>
    public enum DiscountType
    {
        /// <summary>Giảm theo phần trăm (%) của đơn hàng.</summary>
        Percent = 0,

        /// <summary>Giảm số tiền cố định (VNĐ).</summary>
        FixedAmount = 1
    }
}
