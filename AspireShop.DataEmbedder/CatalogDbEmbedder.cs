using System.Diagnostics.CodeAnalysis;
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

    public CatalogDbEmbedder(NpgsqlDataSource dataSource, QdrantClient qdrantClient, string openAiDeploymentName, string openAiEndpoint)
    {
        _embeddingService = new AzureOpenAITextEmbeddingGenerationService(openAiDeploymentName, openAiEndpoint, new Azure.Identity.AzureCliCredential());
        _qdrantClient = qdrantClient;
        _dataSource = dataSource;
    }

    public async Task IngestAndVectorizeDataAsync()
    {
        var data = await RetrieveDataFromDatabaseAsync();
        var embeddings = await CreateEmbeddingsAsync(data);
        await IngestVectorizedDataToQdrantAsync(data, embeddings);
    }

    private async Task<List<string>> RetrieveDataFromDatabaseAsync()
    {
        var data = new List<string>();

        await using var conn = _dataSource.CreateConnection();
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("SELECT description FROM catalog_items", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            data.Add(reader.GetString(0)); // Adjust based on your table schema
        }

        return data;
    }

    private async Task<List<ReadOnlyMemory<float>>> CreateEmbeddingsAsync(List<string> data)
    {
        var embeddings = new List<ReadOnlyMemory<float>>();

        foreach (var item in data)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(item);
            embeddings.Add(embedding.ToArray().AsMemory());
        }

        return embeddings;
    }

    private async Task IngestVectorizedDataToQdrantAsync(List<string> data, List<ReadOnlyMemory<float>> embeddings)
    {
        const string collectionName = "catalog_items";

        // Ensure the collection exists in Qdrant
        await _qdrantClient.CreateCollectionAsync(collectionName, new VectorParams{Size=1536, Distance = Distance.Cosine });

        // Upsert data into the Qdrant vector store
        var points = new List<PointStruct>();

        for (int i = 0; i < data.Count; i++)
        {
            points.Add(new PointStruct
            {
                Id = (ulong)i, // Use an integer or unique identifier for the ID
                Vectors = embeddings[i].ToArray(), // Ensure embeddings[i] is already a float[]
                Payload =
                {
                    ["description"] = new Qdrant.Client.Grpc.Value { StringValue = data[i] }
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
