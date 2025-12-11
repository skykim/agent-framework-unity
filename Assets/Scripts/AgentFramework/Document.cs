using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

public class Document
{
    public sealed class DocumentChunk
    {
        [VectorStoreKey]
        public int Key { get; set; }        
        
        [VectorStoreData]
        public string Content { get; set; }
        
        [VectorStoreVector(2560)]
        public ReadOnlyMemory<float> ContentEmbedding { get; set; }
    }

    public static async Task<List<DocumentChunk>> ProcessTextFilesToDocumentChunks(string directoryPath)
    {
        var documents = new List<DocumentChunk>();
        int currentKey = 0;
        const int chunkSize = 500;
        const int overlapSize = 100;

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        string[] textFiles = Directory.GetFiles(directoryPath, "*.txt");

        foreach (string filePath in textFiles)
        {
            string fileContent = await File.ReadAllTextAsync(filePath);
            
            if (!string.IsNullOrWhiteSpace(fileContent))
            {
                fileContent = fileContent
                    .Replace("\t", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ")
                    .Replace("  ", " ")
                    .Trim();
                
                if (fileContent.Length <= chunkSize)
                {
                    var documentChunk = new DocumentChunk
                    {
                        Key = currentKey++,
                        Content = fileContent,
                        ContentEmbedding = new ReadOnlyMemory<float>(new float[2560])
                    };
                    documents.Add(documentChunk);
                }
                else
                {
                    for (int i = 0; i < fileContent.Length; i += (chunkSize - overlapSize))
                    {
                        int length = Math.Min(chunkSize, fileContent.Length - i);
                        string chunk = fileContent.Substring(i, length);

                        if (i > 0)
                        {
                            int firstSpace = chunk.IndexOf(' ');
                            if (firstSpace > 0)
                            {
                                i += firstSpace;
                                chunk = chunk.Substring(firstSpace).TrimStart();
                            }
                        }

                        if (i + length < fileContent.Length && length == chunkSize)
                        {
                            int lastSpace = chunk.LastIndexOf(' ');
                            if (lastSpace > 0)
                            {
                                chunk = chunk.Substring(0, lastSpace).TrimEnd();
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(chunk))
                        {
                            var documentChunk = new DocumentChunk
                            {
                                Key = currentKey++,
                                Content = chunk,
                                ContentEmbedding = new ReadOnlyMemory<float>(new float[2560])
                            };
                            documents.Add(documentChunk);
                        }
                    }
                }
            }
        }

        return documents;
    }

    public static async Task SaveDocumentsToFile(List<DocumentChunk> documents, string savePath)
    {
        await using (FileStream fs = new FileStream(savePath, FileMode.Create))
        await using (BinaryWriter writer = new BinaryWriter(fs))
        {
            writer.Write(documents.Count);

            foreach (var doc in documents)
            {
                writer.Write(doc.Key);
                writer.Write(doc.Content);
                
                var embeddingArray = doc.ContentEmbedding.ToArray();
                writer.Write(embeddingArray.Length);
                foreach (var value in embeddingArray)
                {
                    writer.Write(value);
                }
            }
        }
    }

    public static async Task<List<DocumentChunk>> LoadDocumentsFromFile(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return null;
        }

        var documents = new List<DocumentChunk>();

        await using (FileStream fs = new FileStream(savePath, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            int documentCount = reader.ReadInt32();

            for (int i = 0; i < documentCount; i++)
            {
                int key = reader.ReadInt32();
                string content = reader.ReadString();
                
                int embeddingLength = reader.ReadInt32();
                float[] embedding = new float[embeddingLength];
                for (int j = 0; j < embeddingLength; j++)
                {
                    embedding[j] = reader.ReadSingle();
                }

                documents.Add(new DocumentChunk
                {
                    Key = key,
                    Content = content,
                    ContentEmbedding = new ReadOnlyMemory<float>(embedding)
                });
            }
        }

        return documents;
    }
}