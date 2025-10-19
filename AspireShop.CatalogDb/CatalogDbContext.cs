using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspireShop.CatalogDb;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    // https://learn.microsoft.com/ef/core/performance/advanced-performance-topics#compiled-queries

    private static readonly Func<CatalogDbContext, int?, int?, int?, int, IAsyncEnumerable<CatalogItem>> GetCatalogItemsQuery =
        EF.CompileAsyncQuery((CatalogDbContext context, int? catalogBrandId, int? before, int? after, int pageSize) =>
           context.CatalogItems.AsNoTracking()
                  .OrderBy(ci => ci.Id)
                  .Where(ci => catalogBrandId == null || ci.CatalogBrandId == catalogBrandId)
                  .Where(ci => before == null || ci.Id <= before)
                  .Where(ci => after == null || ci.Id >= after)
                  .Take(pageSize + 1));

    public Task<List<CatalogItem>> GetCatalogItemsCompiledAsync(int? catalogBrandId, int? before, int? after, int pageSize)
    {
        return ToListAsync(GetCatalogItemsQuery(this, catalogBrandId, before, after, pageSize));
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    public DbSet<PaymentSession> PaymentSessions => Set<PaymentSession>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        DefineCatalogBrand(builder.Entity<CatalogBrand>());

        DefineCatalogItem(builder.Entity<CatalogItem>());

        DefineCatalogType(builder.Entity<CatalogType>());

        DefinePaymentSession(builder.Entity<PaymentSession>());

        DefineOrder(builder.Entity<Order>());

        DefineOrderItem(builder.Entity<OrderItem>());
    }

    private static void DefineCatalogType(EntityTypeBuilder<CatalogType> builder)
    {
        builder.ToTable("CatalogType");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .UseHiLo("catalog_type_hilo")
            .IsRequired();

        builder.Property(cb => cb.Type)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void DefineCatalogItem(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("Catalog");

        builder.Property(ci => ci.Id)
                    .UseHiLo("catalog_hilo")
                    .IsRequired();

        builder.Property(ci => ci.Name)
            .IsRequired(true)
            .HasMaxLength(50);

        builder.Property(ci => ci.Price)
            .IsRequired(true);

        builder.Property(ci => ci.PictureFileName)
            .IsRequired(false);

        builder.Ignore(ci => ci.PictureUri);

        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId);

        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId);
    }

    private static void DefineCatalogBrand(EntityTypeBuilder<CatalogBrand> builder)
    {
        builder.ToTable("CatalogBrand");
        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .UseHiLo("catalog_brand_hilo")
            .IsRequired();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);
    }

    private static void DefinePaymentSession(EntityTypeBuilder<PaymentSession> builder)
    {
        builder.ToTable("PaymentSession");
        builder.HasKey(ps => ps.StripeSessionId);

        builder.Property(ps => ps.StripeSessionId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ps => ps.CustomerEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ps => ps.Status)
            .IsRequired();

        builder.HasOne(ps => ps.Order)
            .WithOne(o => o.PaymentSession)
            .HasForeignKey<PaymentSession>(ps => ps.OrderId);

        builder.HasIndex(ps => new { ps.Status, ps.ExpiresUtc });
    }

    private static void DefineOrder(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Order");
        builder.HasKey(o => o.OrderId);

        builder.Property(o => o.CustomerEmail)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(o => o.TotalAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(o => o.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(o => o.PaymentStatus)
            .IsRequired();

        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId);

        builder.HasIndex(o => o.CustomerEmail);
        builder.HasIndex(o => o.PaymentStatus);
    }

    private static void DefineOrderItem(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItem");
        builder.HasKey(oi => oi.OrderItemId);

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.UnitPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(oi => oi.CatalogItemId)
            .IsRequired();
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> asyncEnumerable)
    {
        var results = new List<T>();
        await foreach (var value in asyncEnumerable)
        {
            results.Add(value);
        }

        return results;
    }
}
