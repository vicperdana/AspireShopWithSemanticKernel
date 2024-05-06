using AspireShop.CatalogDb;
using Microsoft.EntityFrameworkCore;

namespace AspireShop.CatalogService;

public static class CatalogApi
{
    public static RouteGroupBuilder MapCatalogApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/catalog");

        group.WithTags("Catalog");

        group.MapGet("items/type/all/brand/{catalogBrandId?}", async (int? catalogBrandId, CatalogDbContext catalogContext, int? before, int? after, int pageSize = 8) =>
        {
            var itemsOnPage = await catalogContext.GetCatalogItemsCompiledAsync(catalogBrandId, before, after, pageSize);

            var (firstId, nextId) = itemsOnPage switch
            {
                [] => (0, 0),
                [var only] => (only.Id, only.Id),
                [var first, .., var last] => (first.Id, last.Id)
            };

            return new CatalogItemsPage(
                firstId,
                nextId,
                itemsOnPage.Count < pageSize,
                itemsOnPage.Take(pageSize));
        });

        group.MapGet("items/{catalogItemId:int}/image", async (int catalogItemId, CatalogDbContext catalogDbContext, IHostEnvironment environment) =>
        {
            var item = await catalogDbContext.CatalogItems.FindAsync(catalogItemId);

            if (item is null)
            {
                return Results.NotFound();
            }

            var path = Path.Combine(environment.ContentRootPath, "Images", item.PictureFileName);

            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            return Results.File(path, "image/jpeg");
        })
        .Produces(404)
        .Produces(200, contentType: "image/jpeg");
        
        group.MapGet("items/search", async (string? searchText, CatalogDbContext catalogContext, int pageSize = 8) =>
        {
            var items = catalogContext.CatalogItems.AsQueryable();

            if (!string.IsNullOrEmpty(searchText))
            {
                searchText = searchText.ToLower();
                items = items.Where(item => item.Description != null && (EF.Functions.Like(item.Name.ToLower(), $"%{searchText}%") || EF.Functions.Like(item.Description.ToLower(), $"%{searchText}%")));
            }

            var result = await items.ToListAsync();
    
            var (firstId, nextId) = result switch
            {
                [] => (0, 0),
                [var only] => (only.Id, only.Id),
                [var first, .., var last] => (first.Id, last.Id)
            };

            return new CatalogItemsPage(
                firstId,
                nextId,
                result.Count < pageSize,
                result.Take(pageSize),
                searchText);
        });
        
        group.MapGet("items/price/below/{price}", async (decimal price, CatalogDbContext catalogContext, int pageSize = 8) =>
        {
            var items = catalogContext.CatalogItems.AsQueryable();

            items = items.Where(item => item.Price < price);

            var result = await items.ToListAsync();

            var (firstId, nextId) = result switch
            {
                [] => (0, 0),
                [var only] => (only.Id, only.Id),
                [var first, .., var last] => (first.Id, last.Id)
            };

            return new CatalogItemsPage(
                firstId,
                nextId,
                result.Count < pageSize,
                result.Take(pageSize));
        });

        return group;
    }
}

public record CatalogItemsPage(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data, string? SearchText = null);
