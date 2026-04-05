        using Microsoft.EntityFrameworkCore;
        using Microsoft.AspNetCore.Identity;
        using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
        using web1.Models;

        namespace web1.Data
        {
            public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
            {
                public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
                {
                }

                public DbSet<Product> Products { get; set; }
                public DbSet<Order> Orders { get; set; }
                public DbSet<OrderItem> OrderItems { get; set; }
                public DbSet<Coupon> Coupons { get; set; }
                public DbSet<Category> Categories { get; set; }
                public DbSet<Review> Reviews { get; set; }
                public DbSet<WishlistItem> WishlistItems { get; set; }
                public DbSet<ProductVariant> ProductVariants { get; set; }

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    base.OnModelCreating(modelBuilder);

                    // Rename AspNet tables cho chuẩn production
                    modelBuilder.Entity<ApplicationUser>().ToTable("Users");
                    modelBuilder.Entity<IdentityRole>().ToTable("Roles");
                    modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
                    modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
                    modelBuilder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");
                    modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
                    modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");

                    // Cấu hình Product
                    modelBuilder.Entity<Product>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("Products");
                        entity.Property(e => e.Name).HasMaxLength(200);
                        entity.Property(e => e.Description).HasMaxLength(1000);
                        entity.Property(e => e.CategoryId).HasColumnName("CategoryId");
                        entity.Property(e => e.ImageUrl).HasMaxLength(500);
                        
                        // Relationships
                        entity.HasOne(e => e.Category)
                              .WithMany()
                              .HasForeignKey(e => e.CategoryId)
                              .OnDelete(DeleteBehavior.Restrict);
                        entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.Rating).HasColumnType("decimal(3,2)");
                        entity.Property(e => e.Stock).HasDefaultValue(100);

                        // Indexes
                        entity.HasIndex(e => e.CategoryId);
                        entity.HasIndex(e => e.CreatedDate);
                    });

                    // Cấu hình Order
                    modelBuilder.Entity<Order>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("Orders");
                        entity.Property(e => e.CustomerName).HasMaxLength(200);
                        entity.Property(e => e.Email).HasMaxLength(200);
                        entity.Property(e => e.Phone).HasMaxLength(20);
                        entity.Property(e => e.Address).HasMaxLength(500);
                        entity.Property(e => e.Status).HasMaxLength(50);
                        entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.ShippingFee).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.PaymentMethod).HasMaxLength(50);
                        entity.Property(e => e.CouponCode).HasMaxLength(50);
                        entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");

                        // Relationship với User
                        entity.HasOne<ApplicationUser>()
                              .WithMany(u => u.Orders)
                              .HasForeignKey(e => e.UserId)
                              .OnDelete(DeleteBehavior.SetNull);

                        // Indexes
                        entity.HasIndex(e => e.UserId);
                        entity.HasIndex(e => e.OrderDate);
                    });

                    // Cấu hình OrderItem
                    modelBuilder.Entity<OrderItem>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("OrderItems");
                        entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.SelectedSize).HasMaxLength(20);
                        entity.Property(e => e.SelectedColor).HasMaxLength(50);

                        // Relationships
                        entity.HasOne(e => e.Order)
                              .WithMany(o => o.OrderItems)
                              .HasForeignKey(e => e.OrderId)
                              .OnDelete(DeleteBehavior.Cascade);

                        entity.HasOne(e => e.Product)
                              .WithMany()
                              .HasForeignKey(e => e.ProductId)
                              .OnDelete(DeleteBehavior.Restrict);

                        // Indexes
                        entity.HasIndex(e => e.OrderId);
                        entity.HasIndex(e => e.ProductId);
                    });

                    // Cấu hình Coupon
                    modelBuilder.Entity<Coupon>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("Coupons");
                        entity.Property(e => e.Code).HasMaxLength(50);
                        entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.MaxDiscount).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18,2)");
                        entity.HasIndex(e => e.Code).IsUnique();
                    });

                    // 4. Cấu hình Category
                    modelBuilder.Entity<Category>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("Categories");
                        entity.Property(e => e.Name).HasMaxLength(100);
                        entity.Property(e => e.Slug).HasMaxLength(100);
                        entity.HasIndex(e => e.Slug).IsUnique();
                    });

                    // 5. Cấu hình Review
                    modelBuilder.Entity<Review>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("Reviews");
                        entity.Property(e => e.CustomerName).HasMaxLength(200);
                        entity.Property(e => e.Content).HasMaxLength(1000);
                        entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");

                        // Relationships
                        entity.HasOne(r => r.Product)
                              .WithMany()
                              .HasForeignKey(r => r.ProductId)
                              .OnDelete(DeleteBehavior.Cascade);

                        entity.HasOne(r => r.User)
                              .WithMany()
                              .HasForeignKey(r => r.UserId)
                              .OnDelete(DeleteBehavior.SetNull);
                    });

                    // 6. Cấu hình WishlistItem
                    modelBuilder.Entity<WishlistItem>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("WishlistItems");

                        // Relationships
                        entity.HasOne(w => w.Product)
                              .WithMany()
                              .HasForeignKey(w => w.ProductId)
                              .OnDelete(DeleteBehavior.Cascade);  // xóa wishlist khi xóa sản phẩm

                        entity.HasOne(w => w.User)
                              .WithMany(u => u.WishlistItems)
                              .HasForeignKey(w => w.UserId)
                              .OnDelete(DeleteBehavior.Cascade);  // xóa wishlist khi xóa user

                        // Indexes
                        entity.HasIndex(e => e.UserId);
                        entity.HasIndex(e => e.ProductId);

                        // Unique: mỗi user chỉ wishlist 1 sản phẩm 1 lần
                        entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
                    });

                    // ================================================================
                    // Cấu hình ProductVariant
                    // ================================================================
                    modelBuilder.Entity<ProductVariant>(entity =>
                    {
                        entity.HasKey(e => e.Id);
                        entity.ToTable("ProductVariants");

                        // Cấu hình các cột
                        entity.Property(e => e.Size).HasMaxLength(50).IsRequired();
                        entity.Property(e => e.Color).HasMaxLength(100).IsRequired();
                        entity.Property(e => e.Stock).HasDefaultValue(0);
                        entity.Property(e => e.PriceModifier).HasColumnType("decimal(18,2)");
                        entity.Property(e => e.IsActive).HasDefaultValue(true);

                        // Relationship: ProductVariant → Product (N:1)
                        entity.HasOne(e => e.Product)
                              .WithMany()
                              .HasForeignKey(e => e.ProductId)
                              .OnDelete(DeleteBehavior.Cascade);  // xóa biến thể khi xóa sản phẩm

                        // Indexes
                        entity.HasIndex(e => e.ProductId);
                        // Unique: mỗi sản phẩm không có 2 biến thể trùng Size + Color
                        entity.HasIndex(e => new { e.ProductId, e.Size, e.Color }).IsUnique();
                    });
                }
            }
        }
