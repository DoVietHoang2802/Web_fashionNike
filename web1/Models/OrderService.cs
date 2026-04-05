// ================================================================
// OrderService - Xử lý logic nghiệp vụ đơn hàng
//
// Quy tắc:
//   • CreateOrderAsync: dùng Transaction đảm bảo tính nhất quán.
//   • Tồn kho (Stock) & số lượng bán (SoldCount) được cập nhật bằng ExecuteUpdate
//     (không load entity Product) để tăng hiệu năng.
//   • UpdateOrderStatusAsync: tự động hoàn tồn kho khi hủy / trả hàng,
//     và tự động giảm tồn kho khi khôi phục đơn.
//   • Chỉ dùng AsNoTracking cho các query chỉ-đọc.
// ================================================================
using web1.Data;
using Microsoft.EntityFrameworkCore;

namespace web1.Models
{
    public class OrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductVariantService _variantService;

        public OrderService(ApplicationDbContext context, ProductVariantService variantService)
        {
            _context = context;
            _variantService = variantService;
        }

        // ================================================================
        // CREATE ORDER
        // ================================================================

        /// <summary>
        /// Tạo đơn hàng + giảm tồn kho + tăng SoldCount.
        /// Dùng Transaction để đảm bảo tính nhất quán — nếu lỗi thì Rollback.
        ///
        /// Tồn kho được giảm theo BIẾN THỂ (Size + Color) nếu có trong ProductVariant.
        /// Nếu không có biến thể -> fallback giảm Product.Stock như cũ.
        /// </summary>
        public async Task<Order> CreateOrderAsync(Order order)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in order.OrderItems ?? new List<OrderItem>())
                {
                    if (!item.ProductId.HasValue || !item.Quantity.HasValue) continue;

                    var productInfo = await _context.Products
                        .Where(p => p.Id == item.ProductId)
                        .Select(p => new { p.Name })
                        .FirstOrDefaultAsync();

                    if (productInfo == null) continue;

                    // ── Giảm tồn kho theo BIẾN THỂ ──
                    var size = item.SelectedSize ?? "";
                    var color = item.SelectedColor ?? "";

                    if (!string.IsNullOrEmpty(size) || !string.IsNullOrEmpty(color))
                    {
                        // Thử giảm tồn kho biến thể trước
                        var variantDecreaseOk = await _variantService.DecreaseStockAsync(
                            item.ProductId.Value, size, color, item.Quantity.Value);

                        if (!variantDecreaseOk)
                        {
                            // Biến thể không đủ tồn kho
                            var variant = await _variantService.GetBySizeColorAsync(
                                item.ProductId.Value, size, color);
                            throw new InvalidOperationException(
                                $"Sản phẩm {productInfo.Name} (Size: {size}, Màu: {color}) không đủ tồn kho.");
                        }
                    }
                    else
                    {
                        // Không có Size/Color -> fallback giảm Product.Stock
                        var currentStock = await _context.Products
                            .Where(p => p.Id == item.ProductId)
                            .Select(p => p.Stock)
                            .FirstOrDefaultAsync();

                        if ((currentStock ?? 0) < item.Quantity.Value)
                        {
                            throw new InvalidOperationException(
                                $"Sản phẩm {productInfo.Name} không đủ tồn kho " +
                                $"(Còn {currentStock}, yêu cầu {item.Quantity})");
                        }

                        await _context.Products
                            .Where(p => p.Id == item.ProductId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(p => p.Stock, p2 => (p2.Stock ?? 0) - item.Quantity.Value));
                    }

                    // Tăng SoldCount (vẫn trên Product, không cần theo biến thể)
                    await _context.Products
                        .Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.SoldCount, p2 => (p2.SoldCount ?? 0) + item.Quantity.Value));
                }

                await transaction.CommitAsync();
                return order;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ================================================================
        // READ METHODS (AsNoTracking — chỉ đọc)
        // ================================================================

        /// <summary>Lấy đơn hàng theo Id (kèm OrderItems + Products, untracked).</summary>
        public async Task<Order?> GetOrderByIdAsync(int id)
            => await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

        /// <summary>Lấy tất cả đơn hàng (admin dùng), mới nhất trước.</summary>
        public async Task<List<Order>> GetAllOrdersAsync()
            => await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

        /// <summary>Lọc đơn hàng theo status + search + phân trang.</summary>
        public async Task<(List<Order> Items, int TotalCount)> GetFilteredOrdersAsync(
            string? status, string? search, int page, int pageSize)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o =>
                    (o.CustomerName != null && o.CustomerName.Contains(search)) ||
                    (o.Phone        != null && o.Phone.Contains(search))        ||
                    o.Id.ToString() == search);
            }

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        /// <summary>Lấy đơn hàng theo email (khách chưa đăng nhập).</summary>
        public async Task<List<Order>> GetOrdersByEmailAsync(string email)
            => await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.Email == email && o.IsDeletedByUser != true)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

        /// <summary>Lấy đơn hàng theo UserId (user đã đăng nhập).</summary>
        public async Task<List<Order>> GetOrdersByUserIdAsync(string userId)
            => await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId && o.IsDeletedByUser != true)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

        // ================================================================
        // UPDATE ORDER STATUS
        // ================================================================

        /// <summary>
        /// Cập nhật trạng thái đơn hàng.
        ///   • "Hoàn tất"          → đồng thời set PaymentStatus = "Đã thanh toán"
        ///   • "Đã hủy" / "Trả hàng" → hoàn lại tồn kho + giảm SoldCount
        ///   • Khôi phục từ hủy     → giảm lại tồn kho
        /// </summary>
        public async Task<Order?> UpdateOrderStatusAsync(int orderId, string status)
        {
            // Dùng projection để lấy thông tin mà không cần load entity gốc
            var orderInfo = await _context.Orders
                .Where(o => o.Id == orderId)
                .Select(o => new
                {
                    o.Status,
                    Items = o.OrderItems!
                        .Select(oi => new { oi.ProductId, oi.Quantity, oi.SelectedSize, oi.SelectedColor })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (orderInfo == null) return null;

            var oldStatus = orderInfo.Status;

            // Cập nhật trạng thái bằng ExecuteUpdate (không load entity)
            if (status == "Hoàn tất")
            {
                await _context.Orders
                    .Where(o => o.Id == orderId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(o => o.Status, status)
                        .SetProperty(o => o.PaymentStatus, "Đã thanh toán"));
            }
            else
            {
                await _context.Orders
                    .Where(o => o.Id == orderId)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status));
            }

            // Hoàn tồn kho khi hủy / trả hàng
            if ((status == "Đã hủy" || status == "Trả hàng")
                && oldStatus != "Đã hủy" && oldStatus != "Trả hàng")
            {
                foreach (var item in orderInfo.Items)
                {
                    if (!item.ProductId.HasValue || !item.Quantity.HasValue) continue;

                    // Hoàn tồn kho theo biến thể trước
                    await _variantService.IncreaseStockAsync(
                        item.ProductId.Value, item.SelectedSize ?? "", item.SelectedColor ?? "", item.Quantity.Value);

                    // Giảm SoldCount
                    await _context.Products
                        .Where(p => p.Id == item.ProductId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.SoldCount, p2 =>
                                (p2.SoldCount ?? 0) - item.Quantity.Value < 0
                                    ? 0
                                    : (p2.SoldCount ?? 0) - item.Quantity.Value));
                }
            }
            // Giảm tồn kho khi khôi phục đơn từ hủy
            else if ((oldStatus == "Đã hủy" || oldStatus == "Trả hàng")
                && status != "Đã hủy" && status != "Trả hàng")
            {
                foreach (var item in orderInfo.Items)
                {
                    if (!item.ProductId.HasValue || !item.Quantity.HasValue) continue;

                    // Giảm tồn kho theo biến thể
                    await _variantService.DecreaseStockAsync(
                        item.ProductId.Value, item.SelectedSize ?? "", item.SelectedColor ?? "", item.Quantity.Value);
                }
            }

            return new Order { Id = orderId, Status = status };
        }

        // ================================================================
        // PAYMENT STATUS HELPERS
        // ================================================================

        /// <summary>Đánh dấu đơn hàng đã thanh toán.</summary>
        public async Task<bool> ConfirmPaymentAsync(int orderId)
            => await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Đã thanh toán")) > 0;

        /// <summary>Đánh dấu thanh toán thất bại.</summary>
        public async Task<bool> MarkPaymentFailedAsync(int orderId)
            => await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Thất bại")) > 0;

        /// <summary>Đánh dấu đơn đang chờ thanh toán.</summary>
        public async Task<bool> MarkPaymentPendingAsync(int orderId)
            => await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, "Chờ thanh toán")) > 0;

        // ================================================================
        // UTILITY
        // ================================================================

        /// <summary>Kiểm tra đơn hàng có tồn tại không.</summary>
        public async Task<bool> ExistsAsync(int orderId)
            => await _context.Orders.AnyAsync(o => o.Id == orderId);

        /// <summary>Kiểm tra đơn hàng đã được thanh toán chưa.</summary>
        public async Task<bool> IsPaidAsync(int orderId)
            => await _context.Orders.AnyAsync(o => o.Id == orderId && o.PaymentStatus == "Đã thanh toán");

        /// <summary>Xóa vĩnh viễn đơn hàng khỏi DB (hard delete).</summary>
        public async Task<bool> DeleteOrderAsync(int orderId)
            => await _context.Orders.Where(o => o.Id == orderId).ExecuteDeleteAsync() > 0;

        /// <summary>Xóa mềm đơn hàng (ẩn khỏi view của user).</summary>
        public async Task<bool> SoftDeleteOrderAsync(int orderId)
            => await _context.Orders
                .Where(o => o.Id == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsDeletedByUser, true)) > 0;
    }
}
