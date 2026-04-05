// ================================================================
// ProductVariantService - Xử lý nghiệp vụ biến thể sản phẩm (Size + Color)
//
// Quy tắc:
//   • CRUD biến thể: thêm, sửa, xóa biến thể
//   • Quản lý tồn kho theo biến thể (Size + Color)
//   • Kiểm tra tồn kho trước khi thêm vào giỏ
//   • Lấy danh sách Size/Color có sẵn của sản phẩm
//   • Tính lại Product.Stock = SUM(Variant.Stock) khi biến thể thay đổi
// ================================================================
using Microsoft.EntityFrameworkCore;
using web1.Data;

namespace web1.Models
{
    public class ProductVariantService
    {
        private readonly ApplicationDbContext _context;

        public ProductVariantService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================================================================
        // READ - Lấy dữ liệu
        // ================================================================

        /// <summary>
        /// Lấy tất cả biến thể của một sản phẩm.
        /// </summary>
        /// <param name="productId">ID sản phẩm</param>
        public async Task<List<ProductVariant>> GetByProductIdAsync(int productId)
            => await _context.ProductVariants
                .Where(v => v.ProductId == productId)
                .OrderBy(v => v.Size)
                .ToListAsync();

        /// <summary>
        /// Lấy biến thể theo Size + Color.
        /// </summary>
        public async Task<ProductVariant?> GetBySizeColorAsync(int productId, string size, string color)
            => await _context.ProductVariants
                .FirstOrDefaultAsync(v =>
                    v.ProductId == productId &&
                    v.Size == size &&
                    v.Color == color);

        /// <summary>
        /// Lấy biến thể theo ID.
        /// </summary>
        public async Task<ProductVariant?> GetByIdAsync(int id)
            => await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == id);

        /// <summary>
        /// Lấy danh sách Size có hàng của sản phẩm (Stock > 0 và IsActive = true).
        /// </summary>
        public async Task<List<string>> GetAvailableSizesAsync(int productId)
            => await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.Stock > 0 && v.IsActive)
                .Select(v => v.Size)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

        /// <summary>
        /// Lấy danh sách Color có hàng theo Size đã chọn.
        /// </summary>
        public async Task<List<string>> GetAvailableColorsAsync(int productId, string size)
            => await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.Size == size && v.Stock > 0 && v.IsActive)
                .Select(v => v.Color)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

        /// <summary>
        /// Lấy tất cả Color có hàng của sản phẩm (bất kể size nào).
        /// Dùng khi chưa chọn size.
        /// </summary>
        public async Task<List<string>> GetAllAvailableColorsAsync(int productId)
            => await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.Stock > 0 && v.IsActive)
                .Select(v => v.Color)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

        /// <summary>
        /// Kiểm tra tồn kho của một biến thể cụ thể.
        /// Trả về số lượng tồn kho, hoặc -1 nếu biến thể không tồn tại.
        /// </summary>
        public async Task<int> CheckStockAsync(int productId, string size, string color)
        {
            var variant = await GetBySizeColorAsync(productId, size, color);
            if (variant == null || !variant.IsActive) return -1;
            return variant.Stock;
        }

        /// <summary>
        /// Kiểm tra tồn kho + IsActive của biến thể.
        /// </summary>
        public async Task<(bool exists, bool hasStock, bool isActive, int stock)> CheckVariantStatusAsync(
            int productId, string size, string color)
        {
            var variant = await GetBySizeColorAsync(productId, size, color);
            if (variant == null)
                return (false, false, false, 0);
            return (true, variant.Stock > 0, variant.IsActive, variant.Stock);
        }

        /// <summary>
        /// Lấy giá điều chỉnh (PriceModifier) của biến thể.
        /// Trả về 0 nếu không có modifier.
        /// </summary>
        public async Task<decimal> GetPriceModifierAsync(int productId, string size, string color)
        {
            var variant = await GetBySizeColorAsync(productId, size, color);
            return variant?.PriceModifier ?? 0;
        }

        /// <summary>
        /// Lấy giá bán thực tế = Giá gốc + PriceModifier.
        /// </summary>
        public async Task<decimal> GetEffectivePriceAsync(int productId, string size, string color)
        {
            var product = await _context.Products.FindAsync(productId);
            var modifier = await GetPriceModifierAsync(productId, size, color);
            return (product?.Price ?? 0) + modifier;
        }

        // ================================================================
        // WRITE - Tạo / Sửa / Xóa
        // ================================================================

        /// <summary>
        /// Tạo biến thể mới.
        /// Tự động cập nhật Product.Stock = SUM(Variant.Stock).
        /// </summary>
        public async Task<ProductVariant> CreateAsync(ProductVariant variant)
        {
            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            // Cập nhật tồn kho tổng của sản phẩm
            await UpdateProductStockAsync(variant.ProductId);
            return variant;
        }

        /// <summary>
        /// Cập nhật biến thể.
        /// Tự động cập nhật Product.Stock = SUM(Variant.Stock).
        /// </summary>
        public async Task<bool> UpdateAsync(ProductVariant variant)
        {
            var existing = await _context.ProductVariants.FindAsync(variant.Id);
            if (existing == null) return false;

            existing.Size = variant.Size;
            existing.Color = variant.Color;
            existing.Stock = variant.Stock;
            existing.PriceModifier = variant.PriceModifier;
            existing.IsActive = variant.IsActive;

            await _context.SaveChangesAsync();

            // Cập nhật tồn kho tổng của sản phẩm
            await UpdateProductStockAsync(existing.ProductId);
            return true;
        }

        /// <summary>
        /// Xóa biến thể.
        /// Tự động cập nhật Product.Stock = SUM(Variant.Stock).
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null) return false;

            var productId = variant.ProductId;
            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            // Cập nhật tồn kho tổng của sản phẩm
            await UpdateProductStockAsync(productId);
            return true;
        }

        // ================================================================
        // STOCK MANAGEMENT - Quản lý tồn kho
        // ================================================================

        /// <summary>
        /// Giảm tồn kho biến thể khi đặt hàng.
        /// Dùng ExecuteUpdate để không cần load entity.
        /// </summary>
        public async Task<bool> DecreaseStockAsync(int productId, string size, string color, int quantity)
        {
            var variant = await GetBySizeColorAsync(productId, size, color);
            if (variant == null || variant.Stock < quantity)
                return false;

            await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.Size == size && v.Color == color)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Stock, v2 => v2.Stock - quantity));

            await UpdateProductStockAsync(productId);
            return true;
        }

        /// <summary>
        /// Tăng tồn kho biến thể khi hủy đơn hàng.
        /// </summary>
        public async Task<bool> IncreaseStockAsync(int productId, string size, string color, int quantity)
        {
            var variant = await GetBySizeColorAsync(productId, size, color);
            if (variant == null) return false;

            await _context.ProductVariants
                .Where(v => v.ProductId == productId && v.Size == size && v.Color == color)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Stock, v2 => v2.Stock + quantity));

            await UpdateProductStockAsync(productId);
            return true;
        }

        /// <summary>
        /// Tính lại tồn kho tổng của Product = SUM(Variant.Stock).
        /// Được gọi tự động sau mỗi thao tác thay đổi biến thể.
        /// </summary>
        public async Task UpdateProductStockAsync(int productId)
        {
            var totalStock = await _context.ProductVariants
                .Where(v => v.ProductId == productId)
                .SumAsync(v => v.Stock);

            await _context.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, totalStock));
        }

        /// <summary>
        /// Đồng bộ tồn kho tổng cho TẤT CẢ sản phẩm.
        /// Dùng khi migrate dữ liệu cũ sang hệ thống biến thể.
        /// </summary>
        public async Task SyncAllProductStockAsync()
        {
            var productIds = await _context.ProductVariants
                .Select(v => v.ProductId)
                .Distinct()
                .ToListAsync();

            foreach (var productId in productIds)
            {
                await UpdateProductStockAsync(productId);
            }
        }
    }
}
