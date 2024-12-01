using System.ComponentModel;
using Microsoft.SemanticKernel;
using AspireShop.ChatService.Services;

namespace AspireShop.ChatService.Plugins;

#pragma warning disable SKEXP0010
[Description("Filter catalog items with Vector Embeddings & image recognition")]
public class FilterCatalogItemVector(CatalogChatClientVector catalogChatClient)
{
    [KernelFunction, Description("Return a list of catalog items filtered by name or description")]
    public async Task<CatalogItemsPageVector?> GetCatalogItems(
        [Description("Name or description of the item to be queried")]
        string? searchText)
    {
        if (catalogChatClient is not null)
        {
            var items = await catalogChatClient.SearchVectorAsync<ulong>(searchText);
            var catalogItems = items.Select(item => new CatalogItem
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                PictureUri = item.PictureUri,
                CatalogBrandId = item.CatalogBrandId,
                CatalogTypeId = item.CatalogTypeId
            }).ToList();
            return new CatalogItemsPageVector(0, 0, true, catalogItems);
        }
        else
        {
            throw new InvalidOperationException("CatalogServiceClient is not available");
        }
    }
}
#pragma warning restore SKEXP0010