using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using System.Linq;

namespace AspireShop.ChatService.Services;

[Experimental("SKEXP0010")]
public class CatalogChatClientVector
{
    private readonly AzureOpenAITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    private readonly QdrantVectorStore _vectorStore;
    private readonly string _qdrantCollectionName;
    

    public CatalogChatClientVector(QdrantClient qdrantClient, string openAiDeploymentName, string openAiEndpoint,
        string openAIKey, string qdrantCollectionName)
    {
        _embeddingService =
            new AzureOpenAITextEmbeddingGenerationService(openAiDeploymentName, openAiEndpoint, apiKey: openAIKey);
        _qdrantClient = qdrantClient;
        _vectorStore = new QdrantVectorStore(_qdrantClient);
        _qdrantCollectionName = qdrantCollectionName;
    }

    public async Task<List<CatalogItemVector<TKey>>> SearchVectorAsync<TKey>(string? searchText)
    {
        var recordDefinition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Id", typeof(ulong)),
                new VectorStoreRecordDataProperty("Name", typeof(string)),
                new VectorStoreRecordDataProperty("Description", typeof(string)),
                new VectorStoreRecordDataProperty("Price", typeof(double)),
                new VectorStoreRecordDataProperty("PictureUri", typeof(string)),
                new VectorStoreRecordDataProperty("CatalogBrandId", typeof(int)),
                new VectorStoreRecordDataProperty("CatalogTypeId", typeof(int)),
                new VectorStoreRecordVectorProperty("DefinitionEmbedding", typeof(ReadOnlyMemory<float>))
                    { Dimensions = 1536 }
            }
        };
        var collection = _vectorStore.GetCollection<TKey, CatalogItemVector<TKey>>(_qdrantCollectionName);
        
        var searchVector = await _embeddingService.GenerateEmbeddingAsync(searchText);
        var searchResult = await collection.VectorizedSearchAsync(searchVector);
        var catalogItems = new List<CatalogItemVector<TKey>>();

        await foreach (var searchResultItem in searchResult.Results)
        {
            catalogItems.Add(new CatalogItemVector<TKey>()
            {
                Id = searchResultItem.Record.Id,
                Name = searchResultItem.Record.Name,
                Description = searchResultItem.Record.Description,
                Price = searchResultItem.Record.Price,
                PictureFileName = searchResultItem.Record.PictureFileName,
                CatalogBrandId = searchResultItem.Record.CatalogBrandId,
                CatalogTypeId = searchResultItem.Record.CatalogTypeId,
                DefinitionEmbedding = searchResultItem.Record.DefinitionEmbedding
            });
        }

       
        return catalogItems;
    }
}

public record CatalogItemsPageVector(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data, string? SearchText = null);

public record CatalogItemVector<TKey>
{
    [VectorStoreRecordKey]
    public ulong Id { get; init; }
    
    [VectorStoreRecordData(IsFilterable = true)]
    public required string Name { get; init; }
    
    [VectorStoreRecordData(IsFilterable = true)]
    public required string Description { get; init; }
    
    [VectorStoreRecordData]
    public double Price { get; init; }
    
    [VectorStoreRecordData]
    public string? PictureFileName { get; init; }
    
    [VectorStoreRecordData]
    public int CatalogBrandId { get; init; }
    
    [VectorStoreRecordData]
    public int CatalogTypeId { get; init; }
    
    [VectorStoreRecordVector(1536)]
    public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
}