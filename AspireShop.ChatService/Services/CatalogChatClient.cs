using System.Text;

namespace AspireShop.ChatService.Services;

public class CatalogChatClient(HttpClient client)
{
    public Task<CatalogItemsPage?> SearchItemsAsync(string? searchText)
    {
        // Make the query string with encoded parameters
        var query = new StringBuilder("/api/v1/catalog/items/search?");
        if (!string.IsNullOrEmpty(searchText))
        {
            query.Append($"searchText={searchText}");
        }
        var result = client.GetFromJsonAsync<CatalogItemsPage>(query.ToString());
        return result;
    }
}

public record CatalogItemsPage(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data, string? SearchText = null);

public record CatalogItem
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal Price { get; init; }
    public string? PictureUri { get; init; }
    public int CatalogBrandId { get; init; }
    public required string CatalogBrand { get; init; }
    public int CatalogTypeId { get; init; }
    public required string CatalogType { get; init; }
}