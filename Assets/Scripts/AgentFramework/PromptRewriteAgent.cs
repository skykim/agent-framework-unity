using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using UnityEngine;

public class PromptRewriteAgent : MonoBehaviour
{
    private static string OllamaUrl = "http://localhost:11434";
    private static string OllamaModelName = "qwen2.5:3b"; 
    private const string REWRITE_INSTRUCTION = "ou are an expert Prompt Engineer. Your task is to refine the user's input into clear, grammatically correct language matching the original input without any quotes or explanations.";

    private ChatClientAgent _agent;
    public bool IsInitialized { get; private set; } = false;

    void Start()
    {
        InitializeAsync();
    }

    void InitializeAsync()
    {
        try
        {
            IChatClient chatClient = new OllamaApiClient(new Uri(OllamaUrl), OllamaModelName);
            var agentOptions = new ChatClientAgentOptions
            {
                Name = "PromptRewriteAgent",
                Instructions = REWRITE_INSTRUCTION,
            };
            
            _agent = new ChatClientAgent(chatClient, agentOptions);
            IsInitialized = true;
            Debug.Log("[PromptRewriteAgent] Initialized.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PromptRewriteAgent] Init Error: {ex.Message}");
        }
    }

    public async Task<string> RewriteAsync(string text)
    {
        if (!IsInitialized || _agent == null) 
        {
            Debug.LogWarning("[PromptRewriteAgent] Not initialized yet. Returning original text.");
            return text;
        }

        try
        {
            List<AIContent> contents = new List<AIContent> { new TextContent(text) };
            ChatMessage message = new ChatMessage(ChatRole.User, contents);

            var response = await _agent.RunAsync(new ChatMessage[] { message });
            
            string rewrittenText = response.Messages[0].Text;
            Debug.Log($"[PromptRewriteAgent] '{rewrittenText}'");
            
            return rewrittenText;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PromptRewriteAgent] Rewrite Error: {ex.Message}");
            return text;
        }
    }
}