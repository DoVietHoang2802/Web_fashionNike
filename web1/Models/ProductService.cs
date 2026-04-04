// ================================================================
// ProductService - Xử lý logic nghiệp vụ liên quan đến sản phẩm
//
// Quy tắc:
//   • Dùng projection (ProductDto) để tránh tracking entity → giảm memory, tăng perf.
//   • Cache danh mục (GetCategoriesAsync): 30 phút, key nằm trong AppConstants.
//   • Rating/ReviewCount/SoldCount/CreatedDate không bị ghi đè khi UpdateProductAsync.
//   • Xóa sản phẩm đồng thời xóa hết OrderItem liên quan (FK: Restrict → chặn trước).
// ================================================================
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using web1.Data;

namespace web1.Models
{
    public class ProductService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache          _cache;

        private const int CacheDurationMinutes = 30;

        public ProductService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache   = cache;
        }

        // ================================================================
        // PROJECTION (DTO) - Tránh tracking entity gốc
        // ================================================================

        /// <summary>Projection query: select ra ProductDto thay vì Product entity.</summary>
        private IQueryable<ProductDto> GetProductProjection()
            => _context.Products.Select(p => new ProductDto
            {
                Id            = p.Id,
                Name          = p.Name,
                Description   = p.Description,
                Price         = p.Price,
                Stock         = p.Stock,
                ImageUrl      = p.ImageUrl,
                CreatedDate   = p.CreatedDate,
                CategoryId    = p.CategoryId,
                Rating        = p.Rating,
                ReviewCount   = p.ReviewCount,
                SoldCount     = p.SoldCount,
                CategoryName  = p.Category != null ? p.Category.Name : null,
                CategorySlug  = p.Category != null ? p.Category.Slug : null
            });

        /// <summary>Map ProductDto (untracked) → Product entity để trả về cho Controller/View.</summary>
        private static Product MapDtoToProduct(ProductDto dto)
            => new()
            {
                Id          = dto.Id,
                Name        = dto.Name        ?? "Sản phẩm không tên",
                Description = dto.Description ?? "",
                Price       = dto.Price        ?? 0,
                Stock       = dto.Stock        ?? 0,
                ImageUrl    = dto.ImageUrl     ?? "",
                CreatedDate = dto.CreatedDate  ?? DateTime.Now,
                CategoryId  = dto.CategoryId   ?? 0,
                Rating      = dto.Rating       ?? 0,
                ReviewCount = dto.ReviewCount  ?? 0,
                SoldCount   = dto.SoldCount    ?? 0,
                Category    = dto.CategoryName == null ? null : new Category
                {
                    Id   = dto.CategoryId ?? 0,
                    Name = dto.CategoryName,
                    Slug = dto.CategorySlug ?? ""
                }
            };

        // ================================================================
        // READ METHODS
        // ================================================================

        /// <summary>Lấy tất cả sản phẩm (untracked).</summary>
        public async Task<List<Product>> GetAllProductsAsync()
        {
            var dtos = await GetProductProjection().ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        /// <summary>Tìm sản phẩm theo Id (untracked).</summary>
        public async Task<Product?> GetProductByIdAsync(int id)
        {
            var dto = await GetProductProjection().FirstOrDefaultAsync(p => p.Id == id);
            return dto == null ? null : MapDtoToProduct(dto);
        }

        /// <summary>
        /// Phiên bản sync của GetProductByIdAsync.
        /// Dùng trong CartController vì các action giỏ hàng là sync (AJAX).
        /// </summary>
        public Product? GetProductById(int id)
        {
            var dto = GetProductProjection().FirstOrDefault(p => p.Id == id);
            return dto == null ? null : MapDtoToProduct(dto);
        }

        /// <summary>
        /// Lọc sản phẩm theo danh mục.
        /// Nếu categoryName rỗng → trả tất cả.
        /// </summary>
        public async Task<List<Product>> GetProductsByCategoryAsync(string? categoryName)
        {
            if (string.IsNullOrEmpty(categoryName)) return await GetAllProductsAsync();
            var dtos = await GetProductProjection()
                .Where(p => p.CategoryName != null
                    && p.CategoryName.ToLower() == categoryName.ToLower())
                .ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        /// <summary>Tìm kiếm theo tên / mô tả / danh mục.</summary>
        public async Task<List<Product>> SearchProductsAsync(string? searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm)) return await GetAllProductsAsync();
            var dtos = await GetProductProjection()
                .Where(p => (p.Name != null && p.Name.Contains(searchTerm))
                    || (p.Description != null && p.Description.Contains(searchTerm))
                    || (p.CategoryName != null && p.CategoryName.Contains(searchTerm)))
                .ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        /// <summary>
        /// Lọc + sắp xếp + phân trang.
        /// sort: "price_asc" | "price_desc" | "newest" | "name_asc" | "name_desc"
        /// </summary>
        public async Task<(List<Product> Items, int TotalCount)> GetFilteredProductsAsync(
            int? categoryId, string? search, string? sort, int page, int pageSize)
        {
            var query = GetProductProjection();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p =>
                    (p.Name != null && p.Name.Contains(search))
                    || (p.Description != null && p.Description.Contains(search)));

            query = sort switch
            {
                "price_asc"   => query.OrderBy(p => p.Price),
                "price_desc"  => query.OrderByDescending(p => p.Price),
                "newest"      => query.OrderByDescending(p => p.CreatedDate),
                "name_asc"    => query.OrderBy(p => p.Name),
                "name_desc"   => query.OrderByDescending(p => p.Name),
                _             => query.OrderByDescending(p => p.Id)
            };

            int totalCount = await query.CountAsync();
            var dtos = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (dtos.Select(MapDtoToProduct).ToList(), totalCount);
        }

        /// <summary>Lấy sản phẩm cùng danh mục (trừ sản phẩm hiện tại).</summary>
        public async Task<List<Product>> GetRelatedProductsAsync(int productId, int count = 4)
        {
            var product = await GetProductByIdAsync(productId);
            if (product == null) return new List<Product>();

            var dtos = await GetProductProjection()
                .Where(p => p.CategoryId == product.CategoryId && p.Id != productId)
                .OrderByDescending(p => p.Id)
                .Take(count)
                .ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        /// <summary>
        /// Lấy top N sản phẩm bán chạy nhất (SoldCount > 0).
        /// Kết quả được cache 30 phút.
        /// </summary>
        public async Task<List<string>> GetCategoriesAsync()
        {
            if (!_cache.TryGetValue(AppConstants.CategoriesCacheKey, out List<string>? categories)
                || categories == null)
            {
                categories = await _context.Categories
                    .Where(c => c.IsActive && c.Name != null)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => c.Name!)
                    .ToListAsync();

                _cache.Set(
                    AppConstants.CategoriesCacheKey,
                    categories,
                    new MemoryCacheEntryOptions().SetSlidingExpiration(
                        TimeSpan.FromMinutes(CacheDurationMinutes)));
            }
            return categories;
        }

        /// <summary>Lấy top N sản phẩm bán chạy (SoldCount > 0), sắp giảm theo SoldCount.</summary>
        public async Task<List<Product>> GetTopSellingProductsAsync(int count = 3)
        {
            var dtos = await GetProductProjection()
                .Where(p => (p.SoldCount ?? 0) > 0)
                .OrderByDescending(p => p.SoldCount)
                .Take(count)
                .ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        // ================================================================
        // CRUD METHODS
        // ================================================================

        /// <summary>Tạo sản phẩm mới.</summary>
        public async Task CreateProductAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Cập nhật sản phẩm — chỉ cập nhật trường cơ bản.
        /// Rating, ReviewCount, SoldCount, CreatedDate được GIỮ NGUYÊN.
        /// </summary>
        public async Task<bool> UpdateProductAsync(Product product)
        {
            var existing = await _context.Products.FindAsync(product.Id);
            if (existing == null) return false;

            existing.Name        = product.Name;
            existing.Description = product.Description;
            existing.Price       = product.Price;
            existing.CategoryId  = product.CategoryId;
            existing.ImageUrl    = product.ImageUrl;
            existing.Stock       = product.Stock;
            // NOTE: Rating, ReviewCount, SoldCount, CreatedDate → giữ nguyên

            return await _context.SaveChangesAsync() > 0;
        }

        /// <summary>Xóa sản phẩm + xóa hết OrderItem liên quan.</summary>
        public async Task DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return;

            // Xóa OrderItems trước (FK Restrict → không chặn được nếu không xóa trước)
            await _context.OrderItems.Where(oi => oi.ProductId == id).ExecuteDeleteAsync();
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }

        /// <summary>Lấy sản phẩm nổi bật — sắp mới nhất trước.</summary>
        public async Task<List<Product>> GetFeaturedProductsAsync(int count = 8)
        {
            var dtos = await GetProductProjection()
                .OrderByDescending(p => p.Id)
                .Take(count)
                .ToListAsync();
            return dtos.Select(MapDtoToProduct).ToList();
        }

        // ================================================================
        // REVIEWS
        // ================================================================

        /// <summary>Lấy đánh giá theo sản phẩm, mới nhất trước.</summary>
        public async Task<List<Review>> GetReviewsByProductIdAsync(int productId)
            => await _context.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

        /// <summary>
        /// Thêm đánh giá + cập nhật Rating & ReviewCount của sản phẩm.
        /// Công thức: (TB_cũ × SL_cũ + Điểm_mới) / (SL_cũ + 1)
        /// </summary>
        public async Task AddReviewAsync(Review review)
        {
            _context.Reviews.Add(review);

            var product = await _context.Products.FindAsync(review.ProductId);
            if (product != null)
            {
                decimal currentRating  = product.Rating      ?? 0;
                int     currentCount   = product.ReviewCount ?? 0;

                decimal newTotal = (currentRating * currentCount) + (decimal)(review.Rating ?? 0);
                product.ReviewCount = currentCount + 1;
                product.Rating      = newTotal / product.ReviewCount;
            }

            await _context.SaveChangesAsync();
        }
    }

    // ================================================================
    // ProductDto - DTO nội bộ dùng projection tránh tracking entity gốc
    // ================================================================
    internal class ProductDto
    {
        public int     Id           { get; set; }
        public string? Name         { get; set; }
        public string? Description  { get; set; }
        public decimal? Price       { get; set; }
        public int?    Stock        { get; set; }
        public string? ImageUrl     { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int?    CategoryId   { get; set; }
        public decimal? Rating       { get; set; }
        public int?    ReviewCount  { get; set; }
        public int?    SoldCount    { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
    }
}
