// ================================================================
// CategoryService - Xử lý CRUD danh mục sản phẩm
//
// Caching: GetActiveCategoriesAsync cache 30 phút trong IMemoryCache.
// Mỗi khi Create / Update / Delete → xóa cache để đảm bảo dữ liệu mới nhất.
// ================================================================
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using web1.Data;

namespace web1.Models
{
    public class CategoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache          _cache;

        private const int CacheDurationMinutes = 30;

        public CategoryService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache   = cache;
        }

        /// <summary>Lấy tất cả danh mục (kể cả inactive) — không cache.</summary>
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        /// <summary>Lấy danh mục đang active. Kết quả được cache 30 phút.</summary>
        public async Task<List<Category>> GetActiveCategoriesAsync()
        {
            if (!_cache.TryGetValue(AppConstants.CategoriesCacheKey, out List<Category>? categories)
                || categories == null)
            {
                categories = await _context.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                _cache.Set(
                    AppConstants.CategoriesCacheKey,
                    categories,
                    new MemoryCacheEntryOptions().SetSlidingExpiration(
                        TimeSpan.FromMinutes(CacheDurationMinutes)));
            }
            return categories;
        }

        /// <summary>Tìm danh mục theo Id.</summary>
        public async Task<Category?> GetByIdAsync(int id)
            => await _context.Categories.FindAsync(id);

        /// <summary>Tìm danh mục theo Slug (URL-friendly), chỉ danh mục đang active.</summary>
        public async Task<Category?> GetBySlugAsync(string slug)
            => await _context.Categories
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

        /// <summary>
        /// Tạo mới danh mục. Tự động tạo Slug từ Name, đồng thời xóa cache
        /// để GetActiveCategoriesAsync trả kết quả mới nhất.
        /// </summary>
        public async Task<Category> CreateAsync(Category category)
        {
            category.Slug = GenerateSlug(category.Name ?? "");
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            _cache.Remove(AppConstants.CategoriesCacheKey);
            return category;
        }

        /// <summary>Cập nhật danh mục + cập nhật Slug tự động + xóa cache.</summary>
        public async Task<Category> UpdateAsync(Category category)
        {
            category.Slug = GenerateSlug(category.Name ?? "");
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            _cache.Remove(AppConstants.CategoriesCacheKey);
            return category;
        }

        /// <summary>Xóa danh mục theo Id + xóa cache.</summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            _cache.Remove(AppConstants.CategoriesCacheKey);
            return true;
        }

        /// <summary>Đếm số sản phẩm thuộc một danh mục.</summary>
        public async Task<int> GetProductCountAsync(int categoryId)
            => await _context.Products.CountAsync(p => p.CategoryId == categoryId);

        // ================================================================
        // PRIVATE HELPERS
        // ================================================================

        /// <summary>Tạo URL slug từ tên danh mục. Ví dụ: "Giày Sneakers" → "giay-sneakers"</summary>
        private static string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string slug = name.ToLowerInvariant().Trim();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            return System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        }
    }
}
