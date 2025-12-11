using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

public class ImageAgent : MonoBehaviour
{
    private static string OllamaUrl = "http://localhost:11434";
    private static string OllamaVisionModelName = "qwen3-vl:2b"; 

    [Header("UI Component")]
    public RawImage webcamDisplay;

    private WebCamTexture webCamTexture;

    void Start()
    {
        if (WebCamTexture.devices.Length > 0)
        {
            webCamTexture = new WebCamTexture(640, 480);
            if (webcamDisplay != null)
            {
                webcamDisplay.texture = webCamTexture;
                webcamDisplay.material.mainTexture = webCamTexture;
            }
            webCamTexture.Play();
        }
        else
        {
            Debug.LogWarning("[ImageAgent] No Webcam found.");
        }
    }

    public async Task<string> Process(string message)
    {
        byte[] imageBytes = await MainThreadDispatcher.EnqueueAsync(() =>
        {
            if (webCamTexture == null || !webCamTexture.isPlaying)
            {
                return null;
            }

            if (webCamTexture.width <= 16)
            {
                return null;
            }

            Texture2D snap = new Texture2D(webCamTexture.width, webCamTexture.height);
            snap.SetPixels(webCamTexture.GetPixels());
            snap.Apply();

            byte[] bytes = snap.EncodeToJPG();

            Destroy(snap);

            return bytes;
        });

        if (imageBytes == null)
        {
            return "Camera is not ready or available.";
        }

        return await RunAgentAsync(imageBytes, message);
    }

    private async Task<string> RunAgentAsync(byte[] imageBytes, string userMessage)
    {
        string instructions = "You are an intelligent Vision AI Assistant. Analyze the image and answer the user's question concisely.";
        
        IChatClient chatClient = new OllamaApiClient(new Uri(OllamaUrl), OllamaVisionModelName);

        var agentOptions = new ChatClientAgentOptions
        {
            Name = "VisionAgent",
            Instructions = instructions,
        };

        var agent = new ChatClientAgent(chatClient, agentOptions);

        List<AIContent> messageContents = new List<AIContent>
        {
            new TextContent(userMessage),
            new DataContent(imageBytes, "image/jpeg") 
        };

        ChatMessage message = new ChatMessage(ChatRole.User, messageContents);

        try
        {
            var response = await agent.RunAsync(new ChatMessage[] { message });
            
            string resultText = response.Messages[0].Text;
            Debug.Log("[ImageAgent] Response: " + resultText);
            
            return resultText;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ImageAgent] Error: {e.Message}");
            return $"Error analyzing image: {e.Message}";
        }
    }

    void OnDestroy()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }
}