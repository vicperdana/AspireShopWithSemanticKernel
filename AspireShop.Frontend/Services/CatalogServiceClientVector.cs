using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using System.Linq;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;

namespace AspireShop.Frontend.Services;

public class CatalogServiceClientVector
{
#pragma warning disable
    private readonly AzureOpenAITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    private readonly QdrantVectorStore _vectorStore;
    private readonly string _qdrantCollectionName;

    public CatalogServiceClientVector(QdrantClient qdrantClient, string openAiDeploymentName, string openAiEndpoint,
        string openAIKey, string qdrantCollectionName)
    {
        _embeddingService =
            new AzureOpenAITextEmbeddingGenerationService(openAiDeploymentName, openAiEndpoint, apiKey: openAIKey);
#pragma warning restore
        _qdrantClient = qdrantClient;
        _vectorStore = new QdrantVectorStore(_qdrantClient);
        _qdrantCollectionName = qdrantCollectionName;
    }

    public async Task<List<CatalogItemVector<TKey>>> SearchVectorAsync<TKey>(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            throw new ArgumentException("Search text cannot be null or empty.", nameof(searchText));
        }

        // Generate embeddings from search text
        var searchVector = await _embeddingService.GenerateEmbeddingAsync(searchText);

        // Perform vectorized search in the collection
        var collection = _vectorStore.GetCollection<TKey, CatalogItemVector<TKey>>(_qdrantCollectionName);
        var searchResult = await collection.VectorizedSearchAsync(searchVector, new() { Top = 10 });

        // Extract and return search results
        var resultRecords = await searchResult.Results.Select(result => result.Record).ToListAsync();
        return resultRecords;
    }
}


public record CatalogItemsPageVector(int FirstId, int NextId, bool IsLastPage, IEnumerable<CatalogItem> Data);

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
