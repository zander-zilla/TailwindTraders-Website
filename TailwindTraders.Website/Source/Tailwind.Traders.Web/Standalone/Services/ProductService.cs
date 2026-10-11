using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tailwind.Traders.Web.Standalone.Data;
using Tailwind.Traders.Web.Standalone.Data.Entities;
using Tailwind.Traders.Web.Standalone.Models;

namespace Tailwind.Traders.Web.Standalone.Services
{
    /// <summary>Catalog queries via EF Core 10 + Npgsql (replaces Dapper + hand-written T-SQL).</summary>
    public class ProductService : IProductService
    {
        private readonly CatalogDbContext db;
        private readonly string productImagesUrl;

        public ProductService(CatalogDbContext db, IOptions<Settings> settings)
        {
            this.db = db;
            var basePath = settings.Value.ProductImagesUrl;
            productImagesUrl = string.IsNullOrEmpty(basePath)
                ? "https://tailwindtraders.blob.core.windows.net/product-detail"
                : basePath;
        }

        public async Task<IEnumerable<ProductBrand>> GetBrands() =>
            await db.Brands.AsNoTracking().OrderBy(b => b.Id)
                .Select(b => new ProductBrand { Id = b.Id, Name = b.Name }).ToListAsync();

        public async Task<IEnumerable<ProductType>> GetTypes() =>
            await db.Types.AsNoTracking().OrderBy(t => t.Id)
                .Select(t => new ProductType { Id = t.Id, Code = t.Code, Name = t.Name }).ToListAsync();

        public async Task<Product> GetProduct(int id)
        {
            var p = await db.Products.AsNoTracking()
                .Include(x => x.Brand).Include(x => x.Type).Include(x => x.Features)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == id);
            if (p == null) return null; // legacy behavior kept: controller returns Ok(null)

            var product = ToModel(p);
            product.Features = p.Features.OrderBy(f => f.Id)
                .Select(f => new ProductFeature { Id = f.Id, Title = f.Title, Description = f.Description }).ToList();
            return product;
        }

        public async Task<IEnumerable<Product>> GetProducts(int[] brandIds = null, string[] typeCodes = null, string searchTerm = "")
        {
            // Legacy semantics kept: brand OR type OR name-search; no filter => no products.
            var brands = brandIds ?? new int[0];
            // SQL Server compared type codes case-insensitively (CI collation); normalize to lower case.
            var types = (typeCodes ?? new string[0]).Where(c => c != null).Select(c => c.ToLowerInvariant()).ToArray();
            var pattern = string.IsNullOrEmpty(searchTerm) ? null : "%" + EscapeLike(searchTerm) + "%";

            var rows = await db.Products.AsNoTracking()
                .Include(x => x.Brand).Include(x => x.Type)
                .Where(p => types.Contains(p.Type.Code.ToLower())             // was: t.Code IN @TypeCodes
                         || brands.Contains(p.BrandId)                        // was: b.Id IN @BrandIds   (-> = ANY(@p))
                         || (pattern != null && EF.Functions.ILike(p.Name, pattern))) // was: LIKE (CI) -> ILIKE
                .OrderBy(p => p.Id)
                .ToListAsync();

            return rows.Select(ToModel).ToList();
        }

        private Product ToModel(ProductEntity p) => new Product
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price,
            ImageUrl = $"{productImagesUrl}/{p.ImageName}",
            Brand = new ProductBrand { Id = p.Brand.Id, Name = p.Brand.Name },
            Type = new ProductType { Id = p.Type.Id, Code = p.Type.Code, Name = p.Type.Name },
        };

        // Npgsql ILIKE uses '\' as the default escape character; make %, _ and \ in user input literal.
        private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
