// ================================================================
// OrderController - Xử lý đặt hàng, áp mã giảm giá, lịch sử đơn hàng
//
// Session keys dùng chung: xem AppConstants.cs
//
// Luồng đặt hàng:
//   Giỏ hàng (Session)
//     → Checkout (xem trước đơn hàng)
//     → Áp mã giảm giá (CouponService.ValidateCouponAsync)
//     → Xác nhận đặt hàng (POST Checkout)
//     → Tạo Order + OrderItems trong DB
//     → Xóa Session (giỏ hàng + coupon)
//     → Trang thành công
//
// Phân biệt:
//   OrderController: xử lý đặt hàng (cho khách)
//   AdminController: quản lý đơn hàng (cho admin - cập nhật trạng thái)
// ================================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using web1.Models;
using System.Text.Json;

namespace web1.Controllers
{
    /// <summary>
    /// Controller xử lý đặt hàng cho khách hàng.
    /// Checkout yêu cầu đăng nhập. Lịch sử đơn hàng cho phép xem theo email (khách chưa đăng nhập).
    /// </summary>
    public class OrderController : Controller
    {
        // ================================================================
        // NESTED DTO - CouponSessionData
        // Vì CouponService trả về object, cần chuyển thành DTO đơn giản
        // để serialize/deserialize khi lưu vào Session.
        // ================================================================
        private class CouponSessionData
        {
            public string? Code    { get; set; }       // Mã coupon (VD: "SUMMER20")
            public decimal Discount { get; set; }      // Số tiền giảm (đã tính toán)
        }

        // OrderService: tạo đơn, lấy đơn, cập nhật trạng thái
        private readonly OrderService    _orderService;

        // CouponService: validate coupon, tăng UsedCount
        private readonly CouponService   _couponService;

        // UserManager: lấy UserId khi đặt hàng (gán vào Order)
        private readonly UserManager<ApplicationUser> _userManager;

        // ================================================================
        // CONSTRUCTOR - Tiêm dependency
        // ================================================================
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

        /// <summary>
        /// Đọc giỏ hàng từ Session.
        /// Cơ chế giống CartController.GetCart().
        /// </summary>
        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString(AppConstants.CartSessionKey);
            return string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        /// <summary>
        /// Tính phí ship — hiện tại luôn = 0 (miễn phí vận chuyển).
        /// Có thể mở rộng sau: tính theo khoảng cách, trọng lượng...
        /// </summary>
        private static decimal CalculateShipping(decimal subtotal) => 0;

        // ================================================================
        // APPLY COUPON (AJAX API)
        // ================================================================

        /// <summary>
        /// POST: /Order/ApplyCoupon
        /// API AJAX: validate mã giảm giá.
        /// Dùng khi user nhập mã coupon trên trang checkout hoặc cart.
        ///
        /// Luồng:
        ///   1. Nếu code rỗng -> xóa coupon khỏi session, trả về không giảm
        ///   2. Gọi CouponService.ValidateCouponAsync(code, subtotal) để kiểm tra
        ///   3. Nếu hợp lệ -> lưu vào Session, trả về số tiền giảm
        ///   4. Nếu không hợp lệ -> trả về lỗi
        /// </summary>
        /// <param name="code">Mã coupon nhập vào (VD: "SUMMER20")</param>
        /// <returns>JSON: { success, message, discount, total }</returns>
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string code)
        {
            var cart      = GetCart();
            decimal subtotal   = cart.Sum(c => c.TotalPrice);   // Tổng tiền hàng
            decimal shippingFee = CalculateShipping(subtotal);

            // ── Xóa coupon: code trống hoặc null ──────────────────────
            if (string.IsNullOrWhiteSpace(code))
            {
                HttpContext.Session.Remove(AppConstants.CouponSessionKey);
                return Json(new
                {
                    success     = true,
                    removed     = true,       // Báo AJAX biết là đã xóa
                    subtotal,
                    shippingFee,
                    discount    = 0m,
                    total       = subtotal + shippingFee
                });
            }

            // ── Validate coupon ────────────────────────────────────────
            // ValidateCouponAsync kiểm tra:
            //   - Coupon có tồn tại không?
            //   - Còn hạn sử dụng không?
            //   - Đã đạt giới hạn sử dụng chưa (UsedCount < MaxUsage)?
            //   - Đơn hàng có đủ điều kiện áp dụng (min order amount)?
            var result = await _couponService.ValidateCouponAsync(code, subtotal);
            if (!result.IsValid)
                return Json(new { success = false, message = result.Message });

            // Tính tổng tiền sau giảm
            decimal discount = result.DiscountAmount;
            decimal total    = subtotal + shippingFee - discount;
            if (total < 0) total = 0;

            // Lưu coupon đã áp vào Session để dùng lại khi checkout
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
                description   = result.Message,  // Mô tả coupon (VD: "Giảm 20% tối đa 200K")
                discount,
                subtotal,
                shippingFee,
                total,
                discountType  = result.Coupon.DiscountType.ToString(),
                discountValue = result.Coupon.DiscountValue
            });
        }

        // ================================================================
        // CHECKOUT - Thanh toán
        // ================================================================

        /// <summary>
        /// GET: /Order/Checkout
        /// Trang xác nhận đơn hàng trước khi đặt.
        /// Hiển thị: danh sách sản phẩm, tạm tính, phí ship, giảm giá, tổng cộng.
        /// Yêu cầu đăng nhập.
        /// </summary>
        [Authorize]  // Phải đăng nhập mới checkout được
        public IActionResult Checkout()
        {
            var cart = GetCart();

            // Chặn: giỏ hàng trống thì không cho vào checkout
            if (!cart.Any())
            {
                TempData["Message"] = "Giỏ hàng trống";
                return RedirectToAction("Index", "Cart");
            }

            // Tính tạm tính (subtotal = sum of TotalPrice mỗi CartItem)
            decimal subtotal   = cart.Sum(c => c.TotalPrice);
            decimal shippingFee = CalculateShipping(subtotal);

            // Đọc coupon đang áp dụng từ Session (nếu có)
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

            // Truyền dữ liệu sang View để hiển thị
            ViewBag.CartItems          = cart;
            ViewBag.Subtotal           = subtotal;
            ViewBag.ShippingFee        = shippingFee;
            ViewBag.Discount           = discount;
            ViewBag.AppliedCouponCode  = appliedCode;
            ViewBag.FinalTotal         = total;

            return View(new Order());  // new Order() để bind form với model
        }

        /// <summary>
        /// POST: /Order/Checkout
        /// Xử lý đặt hàng - tạo Order vào DB.
        ///
        /// Quy trình:
        ///   1. Validate giỏ hàng không trống + ModelState hợp lệ
        ///   2. Tính tổng tiền (subtotal + ship - discount)
        ///   3. Build Order object: thông tin khách hàng + danh sách OrderItems
        ///   4. Lưu Order vào DB (OrderService.CreateOrderAsync)
        ///   5. Tăng UsedCount coupon (nếu có áp dụng)
        ///   6. Xóa Session: coupon đã dùng + giỏ hàng đã đặt
        ///   7. Redirect đến trang thành công
        /// </summary>
        /// <param name="order">Dữ liệu form: tên, địa chỉ, số điện thoại, ghi chú...</param>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]  // Chống CSRF - bắt buộc
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

            // ── Đọc coupon đang áp dụng từ Session ──────────────────
            decimal   discount    = 0;
            string? appliedCode = null;
            var couponJson = HttpContext.Session.GetString(AppConstants.CouponSessionKey);
            if (!string.IsNullOrEmpty(couponJson))
            {
                var data = JsonSerializer.Deserialize<CouponSessionData>(couponJson);
                appliedCode = data?.Code;
                discount    = data?.Discount ?? 0;
            }

            // ── Build Order object ────────────────────────────────────
            order.OrderDate    = DateTime.Now;
            order.Status       = "Chờ xác nhận";   // Trạng thái ban đầu
            order.ShippingFee  = shippingFee;
            order.CouponCode   = appliedCode;
            order.DiscountAmount = discount;
            order.TotalAmount  = subtotal + shippingFee - discount;

            // Gán UserId nếu đã đăng nhập (để lọc đơn hàng theo user)
            var userId = _userManager.GetUserId(User);
            if (userId != null) order.UserId = userId;

            // Chuyển CartItem -> OrderItem để lưu vào DB
            // Mỗi CartItem tạo 1 OrderItem tương ứng
            order.OrderItems = cart.Select(cartItem => new OrderItem
            {
                ProductId      = cartItem.ProductId,
                Quantity       = cartItem.Quantity,
                // Tính giá từ TotalPrice / Quantity (chính xác hơn)
                Price          = cartItem.Quantity > 0 && cartItem.TotalPrice > 0
                                     ? cartItem.TotalPrice / cartItem.Quantity
                                     : (cartItem.Product?.Price ?? 0),
                SelectedSize   = cartItem.SelectedSize,
                SelectedColor  = cartItem.SelectedColor
            }).ToList();

            // ── Lưu vào DB ───────────────────────────────────────────
            // CreateOrderAsync: lưu Order + OrderItems (trong transaction)
            await _orderService.CreateOrderAsync(order);

            // ── Tăng UsedCount coupon ─────────────────────────────────
            // Đếm số lần coupon đã được sử dụng (để giới hạn MaxUsage)
            if (!string.IsNullOrEmpty(appliedCode))
            {
                var coupon = await _couponService.GetByCodeAsync(appliedCode);
                if (coupon != null)
                    await _couponService.IncrementUsageAsync(coupon.Id);
            }

            // ── Dọn Session ───────────────────────────────────────────
            // Xóa coupon đã dùng + giỏ hàng đã đặt
            HttpContext.Session.Remove(AppConstants.CouponSessionKey);
            HttpContext.Session.Remove(AppConstants.CartSessionKey);

            TempData["SuccessMessage"] = "Đặt hàng thành công! Chúng tôi sẽ liên hệ với bạn sớm nhất.";
            return RedirectToAction("Success", new { orderId = order.Id });
        }

        /// <summary>
        /// GET: /Order/Success?orderId=X
        /// Trang xác nhận đặt hàng thành công.
        /// Hiển thị thông tin đơn hàng vừa tạo.
        /// </summary>
        public async Task<IActionResult> Success(int orderId)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null) return NotFound();
            return View(order);
        }

        // ================================================================
        // APPLY COUPON FROM CART PAGE
        // ================================================================

        /// <summary>
        /// POST: /Order/ApplyCouponCart
        /// Áp / xóa mã giảm giá từ trang Cart (form POST, không phải AJAX).
        /// Dùng TempData để truyền thông báo sau redirect.
        /// </summary>
        /// <param name="code">Mã coupon nhập vào</param>
        /// <param name="remove">true = xóa coupon</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCouponCart(string code, bool remove = false)
        {
            // Xóa coupon
            if (remove)
            {
                HttpContext.Session.Remove(AppConstants.CouponSessionKey);
                return RedirectToAction("Index", "Cart");
            }

            // Validate coupon
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

            // Lưu coupon vào Session
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
        // MY ORDERS - Lịch sử đơn hàng
        // ================================================================

        /// <summary>
        /// GET: /Order/MyOrders
        /// Lấy danh sách đơn hàng của user hiện tại.
        /// Nếu chưa đăng nhập -> cho phép tìm theo email (khách chưa có tài khoản).
        /// </summary>
        /// <param name="email">Email để tìm đơn hàng (dành cho khách chưa đăng nhập)</param>
        public async Task<IActionResult> MyOrders(string? email)
        {
            List<Order> orders;
            var userId = _userManager.GetUserId(User);

            // Ưu tiên: lấy theo UserId nếu đã đăng nhập
            if (userId != null)
                orders = await _orderService.GetOrdersByUserIdAsync(userId);
            // Nếu chưa đăng nhập: tìm theo email
            else if (!string.IsNullOrEmpty(email))
                orders = await _orderService.GetOrdersByEmailAsync(email);
            else
                orders = new List<Order>();

            // Gán giá trị mặc định cho các trường nullable
            // Để tránh lỗi hiển thị null trên View
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

        /// <summary>
        /// GET: /Order/Details/{id}
        /// Chi tiết đơn hàng.
        ///
        /// Kiểm tra quyền sở hữu:
        ///   - Đã đăng nhập: chỉ xem đơn của mình (UserId khớp)
        ///   - Chưa đăng nhập mà đơn có UserId: chặn (yêu cầu đăng nhập)
        ///   - Khách chưa đăng nhập + đơn không có UserId: cho xem
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var order  = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            var userId = _userManager.GetUserId(User);

            // User đã đăng nhập → chỉ xem đơn của mình
            if (userId != null && order.UserId != userId) return Forbid();

            // Khách chưa đăng nhập mà đơn có UserId → yêu cầu đăng nhập
            if (userId == null && order.UserId != null) return RedirectToAction("Login", "Account");

            return View(order);
        }

        /// <summary>
        /// POST: /Order/CancelOrder
        /// User hủy đơn hàng của mình.
        /// Chỉ cho hủy khi đơn đang ở trạng thái "Chờ xác nhận" hoặc "Đã xác nhận".
        /// Sau khi bàn giao ĐVVC thì không được hủy.
        /// </summary>
        /// <param name="id">ID đơn hàng muốn hủy</param>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order  = await _orderService.GetOrderByIdAsync(id);
            var userId = _userManager.GetUserId(User);

            // Kiểm tra đơn tồn tại + thuộc về user này
            if (order == null || order.UserId != userId) return NotFound();

            // Chỉ cho hủy đơn chưa xử lý vận chuyển
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
        /// POST: /Order/ConfirmShipperPayment
        /// User xác nhận đã trả tiền mặt cho shipper khi nhận hàng.
        /// Cập nhật cả Status ("Đã thanh toán") và PaymentStatus.
        /// Chỉ thực hiện khi đơn đã ở trạng thái "Đến nơi".
        /// </summary>
        /// <param name="id">ID đơn hàng</param>
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
                // Cập nhật trạng thái đơn hàng
                await _orderService.UpdateOrderStatusAsync(id, "Đã thanh toán");
                // Cập nhật trạng thái thanh toán
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
        /// POST: /Order/DeleteFromHistory
        /// Xóa đơn hàng khỏi lịch sử (soft delete).
        /// Chỉ áp dụng khi đơn đã "Hoàn tất" hoặc "Đã hủy".
        /// Đơn đang xử lý không được xóa.
        /// </summary>
        /// <param name="id">ID đơn hàng muốn xóa</param>
        [HttpPost]
        public async Task<IActionResult> DeleteFromHistory(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            if (order == null) return NotFound();

            // Soft delete: chỉ đánh dấu đã xóa, không xóa vật lý khỏi DB
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
