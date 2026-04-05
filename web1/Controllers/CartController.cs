// ================================================================
// CartController - Quản lý giỏ hàng (session-based)
//
// Cách hoạt động:
//   • Giỏ hàng được lưu trong Session của server (HttpContext.Session).
//   • Dữ liệu giỏ hàng được serialize thành JSON string để lưu vào Session.
//   • Key dùng chung: AppConstants.CartSessionKey (xem AppConstants.cs).
//   • Mỗi CartItem lưu: ProductId, Product (object), Quantity, Size, Color.
//
// NOTE:
//   • Giỏ hàng KHÔNG lưu vào DB - chỉ tồn tại trong phiên làm việc của user.
//   • Khi user đăng nhập / thanh toán -> chuyển thành Order trong DB.
//   • Image upload xử lý trong AdminController/ProductCreate.
// ================================================================


using Microsoft.AspNetCore.Mvc;
using web1.Models;
using System.Text.Json;

namespace web1.Controllers
{
    /// <summary>
    /// Controller quản lý giỏ hàng cho khách hàng.
    /// Tất cả các action xử lý phía server đều trả về JSON (AJAX) hoặc View thông thường.
    /// Không yêu cầu đăng nhập - ai cũng có thể thêm vào giỏ.
    /// </summary>
    public class CartController : Controller
    {
        // ProductService: dùng để lấy thông tin sản phẩm (tên, giá)
        private readonly ProductService _productService;

        // ProductVariantService: kiểm tra tồn kho theo biến thể (Size + Color)
        private readonly ProductVariantService _variantService;

        public CartController(ProductService productService, ProductVariantService variantService)
        {
            _productService = productService;
            _variantService = variantService;
        }

        // ================================================================
        // CART HELPERS - Đọc / Ghi giỏ hàng từ Session
        // ================================================================

        /// <summary>
        /// Đọc giỏ hàng từ Session.
        ///
        /// Cơ chế:
        ///   Session (server-side) lưu JSON string dưới key AppConstants.CartSessionKey
        ///   → JsonSerializer.Deserialize<List<CartItem>>: chuyển JSON string về List<CartItem>
        ///   → Nếu Session chưa có (cartJson rỗng) -> trả về list rỗng mới
        ///
        /// Tại sao lưu JSON vào Session thay vì object trực tiếp?
        ///   Vì Session chỉ lưu được kiểu primitive (string, byte[]).
        ///   Cần serialize object -> JSON string -> lưu vào Session.
        /// </summary>
        /// <returns>Danh sách CartItem hiện có trong giỏ</returns>
        private List<CartItem> GetCart()
        {
            // Đọc chuỗi JSON từ Session
            var cartJson = HttpContext.Session.GetString(AppConstants.CartSessionKey);

            // Nếu Session trống -> giỏ hàng rỗng
            return string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                // Deserialize JSON string thành List<CartItem>
                : JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        /// <summary>
        /// Lưu giỏ hàng vào Session.
        ///
        /// Cơ chế:
        ///   List<CartItem> -> JsonSerializer.Serialize() -> chuỗi JSON
        ///   -> Session.SetString(key, jsonString)
        ///   Mỗi lần thêm / sửa / xóa đều gọi SaveCart() để cập nhật Session.
        /// </summary>
        /// <param name="cart">Danh sách CartItem cần lưu</param>
        private void SaveCart(List<CartItem> cart)
        {
            // Serialize List<CartItem> thành chuỗi JSON
            HttpContext.Session.SetString(AppConstants.CartSessionKey,
                JsonSerializer.Serialize(cart));
        }

        // ================================================================
        // READ - Xem giỏ hàng
        // ================================================================

        /// <summary>
        /// GET: /Cart hoặc /Cart/Index
        /// Hiển thị trang giỏ hàng đầy đủ với danh sách sản phẩm.
        /// User chưa đăng nhập vẫn xem được giỏ hàng (vì lưu trong Session).
        /// </summary>
        public IActionResult Index()
        {
            var cart = GetCart();  // Đọc giỏ hàng từ Session
            return View(cart);      // Truyền danh sách CartItem sang View
        }

        /// <summary>
        /// GET: /Cart/GetSideCart
        /// Trả về Partial View cho side-cart popup (thanh bên/phía dưới màn hình).
        /// Được gọi qua AJAX khi user thêm sản phẩm để cập nhật popup nhanh.
        /// </summary>
        public IActionResult GetSideCart()
            => PartialView("_SideCartContent", GetCart());

        // ================================================================
        // WRITE - Thêm / Sửa / Xóa giỏ hàng (tất cả trả JSON - AJAX)
        // ================================================================

        /// <summary>
        /// POST: /Cart/AddToCart
        /// Thêm sản phẩm vào giỏ hàng (AJAX).
        ///
        /// Quy trình:
        ///   1. Lấy sản phẩm từ DB, kiểm tra tồn tại
        ///   2. Kiểm tra tồn kho (số lượng không vượt quá Stock)
        ///   3. Nếu sản phẩm đã có trong giỏ (cùng productId + size + color):
        ///      → Cộng thêm quantity
        ///   4. Nếu chưa có:
        ///      → Thêm CartItem mới vào danh sách
        ///   5. Lưu giỏ hàng vào Session
        ///   6. Trả về JSON cho AJAX xử lý UI
        ///
        /// Lưu ý: Giỏ hàng phân biệt theo Size + Color (cùng 1 sản phẩm,
        /// size M và size L được coi là 2 item khác nhau).
        /// </summary>
        /// <param name="productId">ID sản phẩm muốn thêm</param>
        /// <param name="quantity">Số lượng muốn thêm (default = 1)</param>
        /// <param name="selectedSize">Size đã chọn (null nếu không có)</param>
        /// <param name="selectedColor">Màu đã chọn (null nếu không có)</param>
        /// <returns>JSON: { success, message, cartCount }</returns>
        [HttpPost]
        public async Task<IActionResult> AddToCart(
            int productId, int quantity = 1,
            string? selectedSize = null, string? selectedColor = null)
        {
            // Bước 1: Lấy sản phẩm từ DB để kiểm tra tồn tại
            var product = _productService.GetProductById(productId);
            if (product == null)
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });

            // Bước 2: Đọc giỏ hàng hiện tại từ Session
            var cart = GetCart();

            // Bước 3: Tìm xem sản phẩm đã có trong giỏ chưa (cùng Id + Size + Color)
            var existing = cart.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.SelectedSize == selectedSize &&
                i.SelectedColor == selectedColor);

            // Lấy số lượng tồn kho tối đa TỪ BIẾN THỂ
            int maxQty = await _variantService.CheckStockAsync(productId, selectedSize ?? "", selectedColor ?? "");

            // Nếu không có biến thể (hệ thống cũ hoặc chưa có biến thể) -> fallback về Product.Stock
            if (maxQty < 0)
                maxQty = product.Stock ?? 0;

            if (existing != null)
            {
                // Trường hợp A: Sản phẩm đã có trong giỏ -> cộng dồn số lượng
                if (existing.Quantity + quantity > maxQty)
                    return Json(new
                    {
                        success = false,
                        message = $"Chỉ còn {maxQty} sản phẩm trong kho."
                    });
                existing.Quantity += quantity;
            }
            else
            {
                // Trường hợp B: Sản phẩm chưa có trong giỏ -> thêm mới
                if (quantity > maxQty)
                    return Json(new
                    {
                        success = false,
                        message = $"Chỉ còn {maxQty} sản phẩm trong kho."
                    });

                cart.Add(new CartItem
                {
                    ProductId     = productId,
                    Product       = product,
                    Quantity      = quantity,
                    SelectedSize  = selectedSize,
                    SelectedColor = selectedColor
                });
            }

            // Bước 4: Lưu giỏ hàng đã cập nhật vào Session
            SaveCart(cart);

            // Bước 5: Trả về JSON cho AJAX cập nhật UI (số lượng badge, popup...)
            return Json(new
            {
                success    = true,
                message    = "Đã thêm vào giỏ hàng",
                cartCount  = cart.Sum(i => i.Quantity)  // Tổng số lượng để cập nhật icon giỏ hàng
            });
        }

        /// <summary>
        /// POST: /Cart/UpdateQuantity
        /// Cập nhật số lượng của một sản phẩm trong giỏ (AJAX).
        ///
        /// Quy tắc:
        ///   - quantity <= 0: xóa sản phẩm khỏi giỏ
        ///   - quantity > Stock: báo lỗi, không cập nhật
        ///   - 1 <= quantity <= Stock: cập nhật số lượng mới
        /// </summary>
        /// <param name="productId">ID sản phẩm cần cập nhật</param>
        /// <param name="quantity">Số lượng mới (0 = xóa)</param>
        /// <param name="selectedSize">Size để xác định đúng CartItem</param>
        /// <param name="selectedColor">Color để xác định đúng CartItem</param>
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(
            int productId, int quantity,
            string? selectedSize = null, string? selectedColor = null)
        {
            var cart = GetCart();

            var item = cart.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.SelectedSize == selectedSize &&
                i.SelectedColor == selectedColor);

            if (item != null)
            {
                // Nếu quantity <= 0 -> xóa item khỏi giỏ
                if (quantity <= 0)
                    cart.Remove(item);
                else
                {
                    // Kiểm tra tồn kho TỪ BIẾN THỂ
                    int maxQty = await _variantService.CheckStockAsync(productId, selectedSize ?? "", selectedColor ?? "");
                    if (maxQty < 0)
                    {
                        var product = _productService.GetProductById(productId);
                        maxQty = product?.Stock ?? 0;
                    }

                    if (quantity > maxQty)
                        return Json(new
                        {
                            success = false,
                            message = $"Chỉ còn {maxQty} sản phẩm trong kho."
                        });
                    item.Quantity = quantity;
                }
                SaveCart(cart);
            }

            return Json(new { success = true, cartCount = cart.Sum(i => i.Quantity) });
        }

        /// <summary>
        /// POST: /Cart/RemoveFromCart
        /// Xóa một sản phẩm khỏi giỏ hàng (AJAX).
        /// Xóa đúng CartItem dựa vào ProductId + Size + Color.
        /// </summary>
        /// <param name="productId">ID sản phẩm cần xóa</param>
        /// <param name="selectedSize">Size để xác định đúng CartItem</param>
        /// <param name="selectedColor">Color để xác định đúng CartItem</param>
        [HttpPost]
        public IActionResult RemoveFromCart(
            int productId, string? selectedSize = null, string? selectedColor = null)
        {
            var cart = GetCart();  // Đọc giỏ hàng từ Session

            // Tìm CartItem cần xóa
            var item = cart.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.SelectedSize == selectedSize &&
                i.SelectedColor == selectedColor);

            if (item != null)
            {
                cart.Remove(item);  // Xóa khỏi danh sách
                SaveCart(cart);      // Lưu lại Session
            }

            return Json(new { success = true, cartCount = cart.Sum(i => i.Quantity) });
        }

        /// <summary>
        /// POST: /Cart/ClearCart
        /// Xóa toàn bộ giỏ hàng (AJAX).
        /// Dùng Session.Remove() để xóa key khỏi Session (không cần SaveCart).
        /// </summary>
        [HttpPost]
        public IActionResult ClearCart()
        {
            // Xóa key CartSessionKey khỏi Session
            // KHÔNG cần Serialize + Save vì xóa key = giỏ hàng rỗng
            HttpContext.Session.Remove(AppConstants.CartSessionKey);
            return Json(new { success = true });
        }
    }
}
