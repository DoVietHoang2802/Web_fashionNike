// ================================================================
// OrderController - Xử lý đặt hàng, áp mã giảm giá, lịch sử đơn
//
// Session keys dùng chung: xem AppConstants.cs
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;
using System.Text.Json;

namespace web1.Controllers
{
    public class OrderController : Controller
    {
        // ================================================================
        // NESTED DTO - Lưu coupon đang áp dụng vào Session
        // ================================================================
        private class CouponSessionData
        {
            public string? Code    { get; set; }
            public decimal Discount { get; set; }
        }

        private readonly OrderService    _orderService;
        private readonly CouponService   _couponService;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(
            OrderService    orderService,
            CouponService   couponService,
            UserManager<ApplicationUser> userManager)
        {
            _orderService  = orderService;
            _couponService = couponService;
            _userManager   = userManager;
        }

        // ================================================================
        // CART HELPERS - Dùng chung
        // ================================================================

        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString(AppConstants.CartSessionKey);
            return string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        /// <summary>Tính phí ship — hiện tại luôn = 0.</summary>
        private static decimal CalculateShipping(decimal subtotal) => 0;

        // ================================================================
        // APPLY COUPON (AJAX API)
        // ================================================================

        /// <summary>API: Validate mã giảm giá — trả JSON cho AJAX.</summary>
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string code)
        {
            var cart      = GetCart();
            decimal subtotal   = cart.Sum(c => c.TotalPrice);
            decimal shippingFee = CalculateShipping(subtotal);

            // ── Xóa coupon đang áp dụng ───────────────────────────────
            if (string.IsNullOrWhiteSpace(code))
            {
                HttpContext.Session.Remove(AppConstants.CouponSessionKey);
                return Json(new
                {
                    success     = true,
                    removed     = true,
                    subtotal,
                    shippingFee,
                    discount    = 0m,
                    total       = subtotal + shippingFee
                });
            }

            // ── Validate coupon ────────────────────────────────────────
            var result = await _couponService.ValidateCouponAsync(code, subtotal);
            if (!result.IsValid)
                return Json(new { success = false, message = result.Message });

            decimal discount = result.DiscountAmount;
            decimal total    = subtotal + shippingFee - discount;
            if (total < 0) total = 0;

            // Lưu vào session
            HttpContext.Session.SetString(AppConstants.CouponSessionKey,
                JsonSerializer.Serialize(new CouponSessionData
                {
                    Code     = result.Coupon!.Code,
                    Discount = discount
                }));

            return Json(new
            {
                success       = true,
                removed       = false,
                code          = result.Coupon.Code,
                description   = result.Message,
                discount,
                subtotal,
                shippingFee,
                total,
                discountType  = result.Coupon.DiscountType.ToString(),
                discountValue = result.Coupon.DiscountValue
            });
        }

        // ================================================================
        // CHECKOUT
        // ================================================================

        /// <summary>GET: Trang xác nhận đơn hàng trước khi đặt.</summary>
        [Authorize]
        public IActionResult Checkout()
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["Message"] = "Giỏ hàng trống";
                return RedirectToAction("Index", "Cart");
            }

            decimal subtotal   = cart.Sum(c => c.TotalPrice);
            decimal shippingFee = CalculateShipping(subtotal);

            // Đọc coupon đang áp dụng từ session
            decimal   discount    = 0;
            string? appliedCode = null;
            var couponJson = HttpContext.Session.GetString(AppConstants.CouponSessionKey);
            if (!string.IsNullOrEmpty(couponJson))
            {
                var data = JsonSerializer.Deserialize<CouponSessionData>(couponJson);
                appliedCode = data?.Code;
                discount    = data?.Discount ?? 0;
            }

            decimal total = subtotal + shippingFee - discount;

            ViewBag.CartItems          = cart;
            ViewBag.Subtotal           = subtotal;
            ViewBag.ShippingFee        = shippingFee;
            ViewBag.Discount           = discount;
            ViewBag.AppliedCouponCode  = appliedCode;
            ViewBag.FinalTotal         = total;

            return View(new Order());
        }

        /// <summary>
        /// POST: Xử lý đặt hàng.
        /// Tạo Order → giảm tồn kho (trong transaction) → tăng UsedCount coupon
        /// → xóa session coupon & cart.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["Message"] = "Giỏ hàng trống";
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cart;
                ViewBag.Subtotal = cart.Sum(c => c.TotalPrice);
                ViewBag.FinalTotal = ViewBag.Subtotal;
                return View(order);
            }

            decimal subtotal   = cart.Sum(c => c.TotalPrice);
            decimal shippingFee = CalculateShipping(subtotal);

            // ── Coupon đang áp dụng ──────────────────────────────────
            decimal   discount    = 0;
            string? appliedCode = null;
            var couponJson = HttpContext.Session.GetString(AppConstants.CouponSessionKey);
            if (!string.IsNullOrEmpty(couponJson))
            {
                var data = JsonSerializer.Deserialize<CouponSessionData>(couponJson);
                appliedCode = data?.Code;
                discount    = data?.Discount ?? 0;
            }

            // ── Build Order ───────────────────────────────────────────
            order.OrderDate    = DateTime.Now;
            order.Status       = "Chờ xác nhận";
            order.ShippingFee  = shippingFee;
            order.CouponCode   = appliedCode;
            order.DiscountAmount = discount;
            order.TotalAmount  = subtotal + shippingFee - discount;

            var userId = _userManager.GetUserId(User);
            if (userId != null) order.UserId = userId;

            order.OrderItems = cart.Select(cartItem => new OrderItem
            {
                ProductId      = cartItem.ProductId,
                Quantity       = cartItem.Quantity,
                Price          = cartItem.Quantity > 0 && cartItem.TotalPrice > 0
                                     ? cartItem.TotalPrice / cartItem.Quantity
                                     : (cartItem.Product?.Price ?? 0),
                SelectedSize   = cartItem.SelectedSize,
                SelectedColor  = cartItem.SelectedColor
            }).ToList();

            // ── Lưu vào DB ───────────────────────────────────────────
            await _orderService.CreateOrderAsync(order);

            // ── Tăng UsedCount coupon ─────────────────────────────────
            if (!string.IsNullOrEmpty(appliedCode))
            {
                var coupon = await _couponService.GetByCodeAsync(appliedCode);
                if (coupon != null)
                    await _couponService.IncrementUsageAsync(coupon.Id);
            }

            // ── Dọn session ───────────────────────────────────────────
            HttpContext.Session.Remove(AppConstants.CouponSessionKey);
            HttpContext.Session.Remove(AppConstants.CartSessionKey);

            TempData["SuccessMessage"] = "Đặt hàng thành công! Chúng tôi sẽ liên hệ với bạn sớm nhất.";
            return RedirectToAction("Success", new { orderId = order.Id });
        }

        /// <summary>Trang xác nhận đặt hàng thành công.</summary>
        public async Task<IActionResult> Success(int orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null) return NotFound();
            return View(order);
        }

        // ================================================================
        // APPLY COUPON FROM CART PAGE
        // ================================================================

        /// <summary>POST: Áp / xóa mã giảm giá từ trang Cart.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCouponCart(string code, bool remove = false)
        {
            if (remove)
            {
                HttpContext.Session.Remove(AppConstants.CouponSessionKey);
                return RedirectToAction("Index", "Cart");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["CouponError"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index", "Cart");
            }

            var cart      = GetCart();
            decimal subtotal = cart.Sum(c => c.TotalPrice);
            var result = await _couponService.ValidateCouponAsync(code.Trim(), subtotal);

            if (!result.IsValid)
            {
                TempData["CouponError"] = result.Message;
                return RedirectToAction("Index", "Cart");
            }

            HttpContext.Session.SetString(AppConstants.CouponSessionKey,
                JsonSerializer.Serialize(new CouponSessionData
                {
                    Code     = result.Coupon!.Code,
                    Discount = result.DiscountAmount
                }));

            TempData["CouponSuccess"] = result.Message;
            return RedirectToAction("Index", "Cart");
        }

        // ================================================================
        // MY ORDERS / ORDER DETAILS
        // ================================================================

        /// <summary>
        /// Lấy danh sách đơn hàng của user hiện tại HOẶC theo email (khách chưa đăng nhập).
        /// </summary>
        public async Task<IActionResult> MyOrders(string? email)
        {
            List<Order> orders;
            var userId = _userManager.GetUserId(User);

            if (userId != null)
                orders = await _orderService.GetOrdersByUserIdAsync(userId);
            else if (!string.IsNullOrEmpty(email))
                orders = await _orderService.GetOrdersByEmailAsync(email);
            else
                orders = new List<Order>();

            // Gán giá trị mặc định cho các trường nullable để tránh lỗi hiển thị
            foreach (var order in orders)
            {
                order.Status        ??= "Chờ xác nhận";
                order.PaymentStatus ??= "Chưa thanh toán";
                order.PaymentMethod ??= "COD";
                order.CustomerName  ??= "Khách hàng";
                order.Phone         ??= "";
                order.Address       ??= "";
                order.ShippingFee   ??= 0;
                order.DiscountAmount ??= 0;
            }

            return View(orders);
        }

        /// <summary>Chi tiết đơn hàng. Kiểm tra quyền sở hữu trước khi hiển thị.</summary>
        public async Task<IActionResult> Details(int id)
        {
            var order  = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            var userId = _userManager.GetUserId(User);

            // User đã đăng nhập → chỉ xem đơn của mình
            if (userId != null && order.UserId != userId) return Forbid();

            // Khách chưa đăng nhập mà đơn có UserId → chặn
            if (userId == null && order.UserId != null) return RedirectToAction("Login", "Account");

            return View(order);
        }

        /// <summary>User hủy đơn hàng — chỉ cho phép khi đang chờ xác nhận hoặc đã xác nhận.</summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order  = await _orderService.GetOrderByIdAsync(id);
            var userId = _userManager.GetUserId(User);

            if (order == null || order.UserId != userId) return NotFound();

            if (order.Status == "Chờ xác nhận" || order.Status == "Đã xác nhận")
            {
                await _orderService.UpdateOrderStatusAsync(id, "Đã hủy");
                TempData["SuccessMessage"] = "Đơn hàng của bạn đã được hủy thành công.";
            }
            else
            {
                TempData["Message"] = "Không thể hủy đơn hàng vì đơn đã được bàn giao đơn vị vận chuyển hoặc đang giao.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// User xác nhận đã trả tiền cho shipper khi đơn "Đến nơi".
        /// Đồng thời cập nhật cả Status và PaymentStatus.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmShipperPayment(int id)
        {
            var order  = await _orderService.GetOrderByIdAsync(id);
            var userId = _userManager.GetUserId(User);

            if (order == null || order.UserId != userId) return NotFound();

            if (order.Status == "Đến nơi")
            {
                await _orderService.UpdateOrderStatusAsync(id, "Đã thanh toán");
                await _orderService.ConfirmPaymentAsync(id);
                TempData["SuccessMessage"] = "Thanh toán thành công! Admin sẽ sớm hoàn tất đơn hàng cho bạn.";
            }
            else
            {
                TempData["Message"] = "Chưa thể xác nhận thanh toán ở trạng thái này.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Xóa đơn hàng khỏi lịch sử (soft delete).
        /// Chỉ áp dụng khi đơn đã Hoàn tất hoặc Đã hủy.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteFromHistory(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            if (order.Status == "Hoàn tất" || order.Status == "Đã hủy")
            {
                await _orderService.SoftDeleteOrderAsync(id);
                TempData["SuccessMessage"] = "Đã xóa đơn hàng khỏi lịch sử.";
            }
            else
            {
                TempData["Message"] = "Không thể xóa đơn hàng đang trong quá trình xử lý.";
            }

            return RedirectToAction(nameof(MyOrders));
        }
    }
}
