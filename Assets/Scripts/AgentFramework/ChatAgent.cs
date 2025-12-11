#pragma warning disable MEAI001

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Agents.AI; 
using Microsoft.Extensions.AI;
using OllamaSharp;

public class ChatAgent : MonoBehaviour
{
    private static string OllamaUrl = "http://localhost:11434";
    private static string OllamaModelName = "qwen2.5:3b"; 

    private string agentName = "Lupin";
    private string instructions = "You are helpful assistant. Your answer must be a single full sentence under 100 characters.";
    const int SHORT_TERM_MEMORY_MESSAGE_COUNT = 2;

    private AIAgent agent;
    private AgentThread conversationThread;

    public bool IsInitialized { get; private set; } = false;

    void Start()
    {
        InitializeAgent();
    }

    void InitializeAgent()
    {
        try
        {
            IChatClient chatClient = new OllamaApiClient(new Uri(OllamaUrl), OllamaModelName);

            var agentOptions = new ChatClientAgentOptions
            {
                Name = agentName,
                Instructions = instructions,
                ChatMessageStoreFactory = ctx => new InMemoryChatMessageStore(
                    new MessageCountingChatReducer(SHORT_TERM_MEMORY_MESSAGE_COUNT),
                    ctx.SerializedState,
                    ctx.JsonSerializerOptions,
                    InMemoryChatMessageStore.ChatReducerTriggerEvent.AfterMessageAdded)
            };

            agent = new ChatClientAgent(chatClient, agentOptions);            
            conversationThread = agent.GetNewThread();

            IsInitialized = true;
            Debug.Log($"[{agentName}] Chat Agent Initialized.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatAgent] ChatAgent Initialization Error: {ex.Message}");
        }
    }

    public async Task<string> GenerateResponseAsync(string compositeMessage, Action<string> onStreamUpdate = null)
    {
        if (!IsInitialized || agent == null || conversationThread == null)
        {
            Debug.LogError("[ChatAgent] ChatAgent is not ready.");
            return string.Empty;
        }

        try
        {
            List<AIContent> messageContents = new List<AIContent>
            {
                new TextContent(compositeMessage)
            };

            ChatMessage chatMessage = new ChatMessage(ChatRole.User, messageContents);

            StringBuilder fullResponseBuilder = new StringBuilder();

            await foreach (var update in agent.RunStreamingAsync(new ChatMessage[] { chatMessage }, conversationThread))
            {
                string token = update.ToString();

                if (!string.IsNullOrEmpty(token))
                {
                    fullResponseBuilder.Append(token);

                    onStreamUpdate?.Invoke(token);
                }
            }

            string finalFullText = fullResponseBuilder.ToString();
            Debug.Log("[ChatAgent] Final Response: " + finalFullText);
            
            return finalFullText;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatAgent] ChatAgent Generation Error: {ex.Message}");
            return "Error generating response.";
        }
    }
}