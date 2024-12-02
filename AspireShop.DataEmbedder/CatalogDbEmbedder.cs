using System.Diagnostics.CodeAnalysis;
using AspireShop.CatalogDb;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Npgsql;
using Qdrant.Client.Grpc;

namespace AspireShop.DataEmbedder;

[Experimental("SKEXP0010")]
public class CatalogDbEmbedder
{
    private readonly AzureOpenAITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    private readonly NpgsqlDataSource _dataSource;

    public CatalogDbEmbedder(NpgsqlDataSource dataSource, QdrantClient qdrantClient, string openAiDeploymentName, string openAiEndpoint, string openAIKey)
    {
        _embeddingService = new AzureOpenAITextEmbeddingGenerationService(openAiDeploymentName, openAiEndpoint, apiKey: openAIKey);
        _qdrantClient = qdrantClient;
        _dataSource = dataSource;
    }

    public async Task IngestAndVectorizeDataAsync<TKey>()
    {
        List<CatalogItem> data = await RetrieveDataFromDatabaseAsync();
        //var embeddings = await CreateEmbeddingsAsync(data);
        await IngestVectorizedDataToQdrantAsync<TKey>(data);
    }

    private async Task<List<CatalogItem>> RetrieveDataFromDatabaseAsync()
    {
        var data = new List<CatalogItem>();

        await using var conn = _dataSource.CreateConnection();
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("SELECT * FROM \"Catalog\"", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            // Map database row to CatalogItem object
            var item = new CatalogItem
            {
                Id = (ulong)reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                Price = reader.GetInt32(reader.GetOrdinal("Price")),
                PictureFileName = reader.GetString(reader.GetOrdinal("PictureFileName")),
                CatalogBrandId = reader.GetInt32(reader.GetOrdinal("CatalogBrandId")),
                CatalogTypeId = reader.GetInt32(reader.GetOrdinal("CatalogTypeId")),
                AvailableStock = reader.GetInt32(reader.GetOrdinal("AvailableStock"))
            };

            // Add the mapped CatalogItem to the data list
            data.Add(item);
        }
        return data;
    }

    private async Task<List<ReadOnlyMemory<float>>> CreateEmbeddingsAsync(List<CatalogItem> data)
    {
        var embeddings = new List<ReadOnlyMemory<float>>();

        foreach (var item in data)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(item.Description);
            embeddings.Add(embedding.ToArray().AsMemory());
        }

        return embeddings;
    }

    private async Task IngestVectorizedDataToQdrantAsync<TKey>(List<CatalogItem> data)
    {
        const string collectionName = "catalog_items";
        var vectorStore = new QdrantVectorStore(_qdrantClient);
        var recordDefinition = new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty("Id", typeof(ulong)),
                new VectorStoreRecordDataProperty("Name", typeof(string)),
                new VectorStoreRecordDataProperty("Description", typeof(string)),
                new VectorStoreRecordDataProperty("Price", typeof(double)),
                new VectorStoreRecordDataProperty("PictureFileName", typeof(string)),
                new VectorStoreRecordDataProperty("CatalogBrandId", typeof(int)),
                new VectorStoreRecordDataProperty("CatalogTypeId", typeof(int)),
                new VectorStoreRecordVectorProperty("DefinitionEmbedding", typeof(ReadOnlyMemory<float>))
                {
                    Dimensions = 1536   
                }
            }
        };
        var collection = new QdrantVectorStoreRecordCollection<CatalogItem>(_qdrantClient, collectionName, new()
        {
            VectorStoreRecordDefinition = recordDefinition

        });

        // Ensure the collection exists in Qdrant
        await collection.CreateCollectionIfNotExistsAsync();
        
        // Upsert data into the Qdrant vector store
        foreach (var item in data)
        {
            await collection.UpsertAsync(new CatalogItem
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                PictureFileName = item.PictureFileName,
                CatalogBrandId = item.CatalogBrandId,
                CatalogTypeId = item.CatalogTypeId,
                DefinitionEmbedding = await _embeddingService.GenerateEmbeddingAsync(item.Description)
            });
        }
        
        //await _qdrantClient.CreateCollectionAsync(collectionName, new VectorParams { Size = 1536, Distance = Distance.Cosine });

        /* Upsert data into the Qdrant vector store
        var points = new List<PointStruct>();

        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            points.Add(new PointStruct
            {
                Id = (ulong)item.Id,
                Vectors = embeddings[i].ToArray(),
                Payload =
                {
                    ["name"] = new Qdrant.Client.Grpc.Value { StringValue = item.Name },
                    ["description"] = new Qdrant.Client.Grpc.Value { StringValue = item.Description },
                    ["price"] = new Qdrant.Client.Grpc.Value { DoubleValue = (double)item.Price },
                    ["picture_file_name"] = new Qdrant.Client.Grpc.Value { StringValue = item.PictureFileName ?? string.Empty },
                    ["catalog_brand_id"] = new Qdrant.Client.Grpc.Value { StringValue = item.CatalogBrandId.ToString() },
                    ["catalog_type_id"] = new Qdrant.Client.Grpc.Value { StringValue = item.CatalogTypeId.ToString() },
                    ["available_stock"] = new Qdrant.Client.Grpc.Value { IntegerValue = item.AvailableStock }
                }
            });
        }*/
        

        // Upsert points into the collection
        //var updateResult = await _qdrantClient.UpsertAsync(collectionName, points);
    }
    
    private sealed class CatalogItemVector<TKey>
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
        public string? PictureUri { get; init; }
    
        [VectorStoreRecordData]
        public int CatalogBrandId { get; init; }
    
        [VectorStoreRecordData]
        public int CatalogTypeId { get; init; }
    
        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
    }
}