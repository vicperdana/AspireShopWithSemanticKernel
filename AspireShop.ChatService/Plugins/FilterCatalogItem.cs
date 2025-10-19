using System.ComponentModel;
using AspireShop.ChatService.Services;

namespace AspireShop.ChatService.Plugins;

public class FilterCatalogItem(CatalogChatClient catalogChatClient)
{
    [Description("Return a list of catalog items filtered by name or description")]
    public async Task<CatalogItemsPage?> GetCatalogItems(
        [Description("Name or description of the item to be queried")]
        string? searchText)
    {
        if (catalogChatClient is not null)
        {
            var items = await catalogChatClient.SearchItemsAsync(searchText);
            return items;
        }
        else
        {
            throw new InvalidOperationException("CatalogServiceClient is not available");
        }
    }
}