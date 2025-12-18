using UnityEngine;
using Microsoft.Extensions.AI;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEditor;
using OllamaSharp;

public static class VectorDBUtil
{
    private static string OllamaUrl = "http://localhost:11434";
    private static string OllamaEmbeddingModelName = "qwen3-embedding:4b";
    
    private const int DEFAULT_TOP_K = 10;
    private const float DEFAULT_THRESHOLD = 0.3f;

    private const string SAVE_FILENAME = "ddd_db.json";
    private const string TEXT_FOLDERNAME = "text";

    private static string SavePath => Path.Combine(Application.streamingAssetsPath, SAVE_FILENAME);
    private static string LoadTextPath => Path.Combine(Application.streamingAssetsPath, TEXT_FOLDERNAME);

    private static IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
    
    private static List<Document.DocumentChunk> internalMemoryDb = new List<Document.DocumentChunk>();

    private static bool isInitialized = false;

#if UNITY_EDITOR
    [MenuItem("Tools/Generate VectorDB")]
    public static async void GenerateDatabaseFromMenu()
    {
        await Save();
    }
#endif

    private static void Initialize()
    {
        if (isInitialized) return;

        var ollamaClient = new OllamaApiClient(new Uri(OllamaUrl), OllamaEmbeddingModelName);
        embeddingGenerator = ollamaClient;

        isInitialized = true;
    }
    
    public static async Task Save()
    {
        Initialize();

        if (!Directory.Exists(LoadTextPath))
        {
            Debug.LogError($"[VectorDBUtil] Text directory not found: {LoadTextPath}");
            return;
        }

        var documents = await Document.ProcessTextFilesToDocumentChunks(LoadTextPath);

        if (documents == null || documents.Count == 0)
        {
            Debug.LogWarning("[VectorDBUtil] No text files found or processed.");
            return;
        }

        await GenerateEmbeddings(documents);

        internalMemoryDb = documents;

        await Document.SaveDocumentsToFile(documents, SavePath);

        Debug.Log($"[VectorDBUtil] Generated & Saved {documents.Count} chunks to {SavePath}");
    }

    public static async Task Load()
    {
        Initialize();
        
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning($"[VectorDBUtil] DB file not found at: {SavePath}");
            return;
        }

        var loadedDocs = await Document.LoadDocumentsFromFile(SavePath);
        
        if (loadedDocs != null)
        {
            internalMemoryDb = loadedDocs;
            Debug.Log($"[VectorDBUtil] Database loaded. Total chunks: {internalMemoryDb.Count}");
        }
    }

    public static async Task<List<string>> SearchOnVectorDB(string query, int topK = DEFAULT_TOP_K, float threshold = DEFAULT_THRESHOLD)
    {
        Initialize();

        if (internalMemoryDb.Count == 0)
        {
            await Load();
            if (internalMemoryDb.Count == 0)
            {
                Debug.LogError("[VectorDBUtil] Database is empty even after load attempt.");
                return new List<string>();
            }
        }

        var embeddingResult = await embeddingGenerator.GenerateAsync(query);
        var queryVector = embeddingResult.Vector;

        var searchResults = internalMemoryDb
            .Select(doc => new 
            { 
                Doc = doc, 
                Score = ComputeCosineSimilarity(queryVector, doc.ContentEmbedding) 
            })
            .Where(x => x.Score >= threshold)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        List<string> results = new List<string>();
        
        foreach (var item in searchResults)
        {
            results.Add(item.Doc.Content);
        }

        return results;
    }

    private static async Task GenerateEmbeddings(List<Document.DocumentChunk> documents)
    {
        const int batchSize = 10;
        int processed = 0;

        for (int i = 0; i < documents.Count; i += batchSize)
        {
            var batch = documents.Skip(i).Take(batchSize).ToList();
            
            foreach (var doc in batch)
            {
                try
                {
                    var result = await embeddingGenerator.GenerateAsync(doc.Content);
                    doc.ContentEmbedding = result.Vector;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VectorDBUtil] Embedding Error: {ex.Message}");
                }
            }
            
            processed += batch.Count;
            #if UNITY_EDITOR
            EditorUtility.DisplayProgressBar("Generating Embeddings", $"Processing {processed}/{documents.Count}", (float)processed / documents.Count);
            #endif
        }
        
        #if UNITY_EDITOR
        EditorUtility.ClearProgressBar();
        #endif
    }

    private static float ComputeCosineSimilarity(ReadOnlyMemory<float> vecA, ReadOnlyMemory<float> vecB)
    {
        var spanA = vecA.Span;
        var spanB = vecB.Span;

        if (spanA.Length != spanB.Length) return 0.0f;

        float dotProduct = 0.0f;
        float magnitudeA = 0.0f;
        float magnitudeB = 0.0f;

        for (int i = 0; i < spanA.Length; i++)
        {
            dotProduct += spanA[i] * spanB[i];
            magnitudeA += spanA[i] * spanA[i];
            magnitudeB += spanB[i] * spanB[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0.0f;

        return dotProduct / (float)(Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}