// ================================================================
// CartController - Quản lý giỏ hàng (session-based)
//
// Session key dùng chung: xem AppConstants.cs
// NOTE: Image upload xử lý trong AdminController/ProductCreate
// ================================================================
using Microsoft.AspNetCore.Mvc;
using web1.Models;
using System.Text.Json;

namespace web1.Controllers
{
    public class CartController : Controller
    {
        private readonly ProductService _productService;

        public CartController(ProductService productService)
        {
            _productService = productService;
        }

        // ================================================================
        // CART HELPERS
        // ================================================================

        private List<CartItem> GetCart()
        {
            var cartJson = HttpContext.Session.GetString(AppConstants.CartSessionKey);
            return string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonSerializer.Deserialize<List<CartItem>>(cartJson) ?? new List<CartItem>();
        }

        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString(AppConstants.CartSessionKey,
                JsonSerializer.Serialize(cart));
        }

        // ================================================================
        // READ
        // ================================================================

        /// <summary>Trang giỏ hàng.</summary>
        public IActionResult Index()
        {
            var cart = GetCart();
            return View(cart);
        }

        /// <summary>Partial view cho side-cart popup.</summary>
        public IActionResult GetSideCart()
            => PartialView("_SideCartContent", GetCart());

        // ================================================================
        // WRITE
        // ================================================================

        /// <summary>Thêm sản phẩm vào giỏ (AJAX). Kiểm tra tồn kho trước khi thêm.</summary>
        [HttpPost]
        public IActionResult AddToCart(
            int productId, int quantity = 1,
            string? selectedSize = null, string? selectedColor = null)
        {
            var product = _productService.GetProductById(productId);
            if (product == null)
                return Json(new { success = false, message = "Sản phẩm không tồn tại" });

            var cart = GetCart();

            var existing = cart.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.SelectedSize == selectedSize &&
                i.SelectedColor == selectedColor);

            int maxQty = product.Stock ?? 0;

            if (existing != null)
            {
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

            SaveCart(cart);
            return Json(new
            {
                success    = true,
                message    = "Đã thêm vào giỏ hàng",
                cartCount  = cart.Sum(i => i.Quantity)
            });
        }

        /// <summary>Cập nhật số lượng trong giỏ (AJAX).</summary>
        [HttpPost]
        public IActionResult UpdateQuantity(
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
                if (quantity <= 0)
                    cart.Remove(item);
                else
                {
                    var product = _productService.GetProductById(productId);
                    if (product != null && quantity > (product.Stock ?? 0))
                        return Json(new
                        {
                            success = false,
                            message = $"Chỉ còn {product.Stock} sản phẩm trong kho."
                        });
                    item.Quantity = quantity;
                }
                SaveCart(cart);
            }

            return Json(new { success = true, cartCount = cart.Sum(i => i.Quantity) });
        }

        /// <summary>Xóa một sản phẩm khỏi giỏ (AJAX).</summary>
        [HttpPost]
        public IActionResult RemoveFromCart(
            int productId, string? selectedSize = null, string? selectedColor = null)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.SelectedSize == selectedSize &&
                i.SelectedColor == selectedColor);

            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return Json(new { success = true, cartCount = cart.Sum(i => i.Quantity) });
        }

        /// <summary>Xóa toàn bộ giỏ hàng (AJAX).</summary>
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove(AppConstants.CartSessionKey);
            return Json(new { success = true });
        }
    }
}
