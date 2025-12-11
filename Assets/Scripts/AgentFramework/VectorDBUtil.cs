using UnityEngine;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEditor;
using Microsoft.Extensions.AI;
using OllamaSharp;

public static class VectorDBUtil
{
    private static string OllamaUrl = "http://localhost:11434";
    private static string OllamaEmbeddingModelName = "qwen3-embedding:4b";
    private const int DEFAULT_TOP_K = 10;
    private const float DEFAULT_THRESHOLD = 0.0f;

    private const string collectionName = "ddd_collection";
    private const string SAVE_FILENAME = "ddd_db.bin";
    private const string TEXT_FOLDERNAME = "text";

    private static string SavePath => Path.Combine(Application.streamingAssetsPath, SAVE_FILENAME);
    private static string LoadTextPath => Path.Combine(Application.streamingAssetsPath, TEXT_FOLDERNAME);

    private static InMemoryVectorStore vectorStore;
    private static IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;

    private static VectorStoreCollection<int, Document.DocumentChunk> collection;

    private static bool isInitialized = false;

#if UNITY_EDITOR
    [MenuItem("Tools/Generate VectorDB")]
    public static async void GenerateDatabaseFromMenu()
    {
        await Save();
    }
#endif

    private static void InitializeVectorDatabase()
    {
        if (isInitialized) return;

        if (!Directory.Exists(Application.streamingAssetsPath))
        {
            Directory.CreateDirectory(Application.streamingAssetsPath);
        }

        vectorStore = new InMemoryVectorStore();

        var ollamaClient = new OllamaApiClient(new Uri(OllamaUrl), OllamaEmbeddingModelName);
        embeddingGenerator = ollamaClient;
        
        isInitialized = true;
    }
    
    public static async Task Save()
    {
        InitializeVectorDatabase();
        collection = await InitializeCollection();
        
        if (!Directory.Exists(LoadTextPath))
        {
            Debug.LogError($"[VectorDBUtil] Text directory not found: {LoadTextPath}");
            return;
        }

        var documents = await Document.ProcessTextFilesToDocumentChunks(LoadTextPath);

        if (documents.Count == 0)
        {
            Debug.LogWarning("[VectorDBUtil] No text files found or processed in the specified directory");
            return;
        }

        await GenerateEmbeddings(documents);
        await Document.SaveDocumentsToFile(documents, SavePath);

        Debug.Log($"[VectorDBUtil] Data generation completed successfully. Processed {documents.Count} chunks.");
    }

    public static async Task Load()
    {
        InitializeVectorDatabase();
        collection = await InitializeCollection();
        
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning($"[VectorDBUtil] Save file not found at: {SavePath}");
            return;
        }

        var documents = await Document.LoadDocumentsFromFile(SavePath);
        
        if (documents != null && documents.Any())
        {
            var upsertTasks = documents.Select(doc => collection.UpsertAsync(doc));
            await Task.WhenAll(upsertTasks);
            Debug.Log("[VectorDBUtil] Vector database loaded successfully");
        }
        else
        {
            Debug.LogWarning("[VectorDBUtil] No documents found in the saved file");
        }
    }

    public static async Task<List<string>> SearchOnVectorDB(string query, int topK = DEFAULT_TOP_K, float threshold = DEFAULT_THRESHOLD)
    {
        InitializeVectorDatabase();

        if (collection == null)
        {
            Debug.Log("[VectorDBUtil] Database not loaded. Attempting to auto-load...");
            await Load();

            if (collection == null)
            {
                Debug.LogError("[VectorDBUtil] Database load failed during search.");
                return new List<string>();
            }
        }

        var embeddingResult = await embeddingGenerator.GenerateAsync(query);

        var searchVector = embeddingResult.Vector;
        var searchResults = collection.SearchAsync(searchVector, topK);


        List<string> results = new List<string>();
        int contentIndex = 1;
        
        await foreach (var result in searchResults)
        {
            if (result.Score >= threshold)
            {
                results.Add($"Context{contentIndex++} (prob:{result.Score:F3}): {result.Record.Content}");
            }
        }

        Debug.Log($"[VectorDBUtil] Search completed. Found {results.Count} results:\n {results}");

        return results;
    }

    private static async Task<VectorStoreCollection<int, Document.DocumentChunk>> InitializeCollection()
    {
        var collection = vectorStore.GetCollection<int, Document.DocumentChunk>(collectionName);
        await collection.EnsureCollectionExistsAsync();
        return collection;
    }

    private static async Task GenerateEmbeddings(IEnumerable<Document.DocumentChunk> documents)
    {
        var documentList = documents.ToList();
        const int batchSize = 50;
        
        for (int i = 0; i < documentList.Count; i += batchSize)
        {
            var batch = documentList.Skip(i).Take(batchSize);
            
            var embeddingTasks = batch.Select(doc => Task.Run(async () =>
            {
                try 
                {
                    var embeddingResult = await embeddingGenerator.GenerateAsync(doc.Content);
                    doc.ContentEmbedding = embeddingResult.Vector;
                    return doc;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VectorDBUtil] Failed to create embeddings: {ex.Message}");
                    return null;
                }
            }));
            
            var completedDocs = (await Task.WhenAll(embeddingTasks)).Where(d => d != null);
            
            var upsertTasks = completedDocs.Select(doc => collection.UpsertAsync(doc));
            await Task.WhenAll(upsertTasks);
                        
            Debug.Log($"[VectorDBUtil] Processed documents: {i + batch.Count()}/{documentList.Count}");
        }
    }
}