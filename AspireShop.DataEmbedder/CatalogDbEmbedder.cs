using System.Diagnostics.CodeAnalysis;
using AspireShop.CatalogDb;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
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

    public async Task IngestAndVectorizeDataAsync()
    {
        List<CatalogItem> data = await RetrieveDataFromDatabaseAsync();
        var embeddings = await CreateEmbeddingsAsync(data);
        await IngestVectorizedDataToQdrantAsync(data, embeddings);
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

    private async Task IngestVectorizedDataToQdrantAsync(List<CatalogItem> data, List<ReadOnlyMemory<float>> embeddings)
    {
        const string collectionName = "catalog_items";

        // Ensure the collection exists in Qdrant
        await _qdrantClient.CreateCollectionAsync(collectionName, new VectorParams { Size = 1536, Distance = Distance.Cosine });

        // Upsert data into the Qdrant vector store
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
        }

        // Upsert points into the collection
        var updateResult = await _qdrantClient.UpsertAsync(collectionName, points);

        if (updateResult.Status == UpdateStatus.Completed)
        {
            Console.WriteLine("Data successfully upserted into Qdrant.");
        }
        else
        {
            Console.WriteLine("Failed to upsert data into Qdrant.");
        }
    }
}