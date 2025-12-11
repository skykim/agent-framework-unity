using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Agents.AI.Workflows;

public class AgentResult
{
    public string Source { get; set; }
    public string Content { get; set; }
}

public class StartExecutor : Executor<string, string>
{
    public StartExecutor() : base("StartExecutor") { }

    public override ValueTask<string> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        Debug.Log($"[Start] Workflow Initiated. Input: '{input}'");
        return new ValueTask<string>(input);
    }
}

public class PromptRewritingExecutor : Executor<string, string>
{
    private readonly PromptRewriteAgent _agent;
    public bool IsEnabled { get; set; } = true;

    public PromptRewritingExecutor(PromptRewriteAgent agent) : base("PromptRewritingExecutor")
    {
        _agent = agent;
    }

    public override async ValueTask<string> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _agent == null)
        {
            Debug.Log("[Rewriter] Skipped (Disabled or Null). Using original input.");
            return input;
        }

        string rewritten = await _agent.RewriteAsync(input);
        return rewritten;
    }
}

public class PassThroughExecutor : Executor<string, AgentResult>
{
    public PassThroughExecutor() : base("PassThroughExecutor") { }

    public override ValueTask<AgentResult> HandleAsync(string input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return new ValueTask<AgentResult>(new AgentResult { Source = "RefinedQuery", Content = input });
    }
}

public class VisionExecutor : Executor<string, AgentResult>
{
    private readonly ImageAgent _imageAgent;
    public bool IsEnabled { get; set; } = true;

    public VisionExecutor(ImageAgent agent) : base("VisionExecutor")
    {
        _imageAgent = agent;
    }

    public override async ValueTask<AgentResult> HandleAsync(string refinedMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _imageAgent == null)
        {
            Debug.Log("[Vision] Skipped (Disabled via Toggle).");
            return new AgentResult { Source = "Vision", Content = string.Empty };
        }

        Debug.Log($"[Vision] Analyzing visual context with query: '{refinedMessage}'");
        string result = string.Empty;
        
        try
        {
            result = await _imageAgent.Process(refinedMessage);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Vision] Error: {ex.Message}");
        }

        return new AgentResult { Source = "Vision", Content = result };
    }
}

public class RagExecutor : Executor<string, AgentResult>
{
    public bool IsEnabled { get; set; } = true;

    public RagExecutor() : base("RagExecutor")
    {
    }

    public override async ValueTask<AgentResult> HandleAsync(string refinedMessage, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            Debug.Log("[RAG] Skipped (Disabled via Toggle).");
            return new AgentResult { Source = "RAG", Content = string.Empty };
        }

        Debug.Log($"[RAG] Processing Retrieval with query: '{refinedMessage}'");
        string contextResult = string.Empty;

        try
        {
            List<string> searchResults = await VectorDBUtil.SearchOnVectorDB(refinedMessage);

            if (searchResults != null && searchResults.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var res in searchResults)
                {
                    sb.AppendLine($"- {res}");
                }
                contextResult = sb.ToString();
                Debug.Log($"[RAG] Found {searchResults.Count} documents. \n{contextResult}");
            }
            else
            {
                Debug.Log("[RAG] No relevant documents found.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RAG] Error: {ex.Message}");
        }

        return new AgentResult { Source = "RAG", Content = contextResult };
    }
}

public class ContextAggregatorExecutor : Executor<AgentResult, string>
{
    private readonly List<AgentResult> _results = new List<AgentResult>();
    private readonly int _expectedCount;

    public ContextAggregatorExecutor(int expectedCount) : base("ContextAggregator")
    {
        _expectedCount = expectedCount;
    }

    public override async ValueTask<string> HandleAsync(AgentResult input, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        _results.Add(input);
                
        if (_results.Count < _expectedCount)
        {
            return string.Empty; 
        }

        Debug.Log("[Aggregator] All inputs received. Constructing prompt...");

        StringBuilder finalPrompt = new StringBuilder();

        var visionContent = _results.FirstOrDefault(r => r.Source == "Vision")?.Content;
        if (!string.IsNullOrWhiteSpace(visionContent))
        {
            finalPrompt.AppendLine("### Visual Context (Webcam) ###");
            finalPrompt.AppendLine(visionContent);
            finalPrompt.AppendLine();
        }

        var ragContent = _results.FirstOrDefault(r => r.Source == "RAG")?.Content;
        if (!string.IsNullOrWhiteSpace(ragContent))
        {
            finalPrompt.AppendLine("### Relevant Knowledge (RAG) ###");
            finalPrompt.AppendLine(ragContent);
            finalPrompt.AppendLine();
        }

        var refinedQuery = _results.FirstOrDefault(r => r.Source == "RefinedQuery")?.Content;
        
        finalPrompt.AppendLine("### User Question ###");
        finalPrompt.AppendLine(refinedQuery);
        
        finalPrompt.AppendLine();
        finalPrompt.AppendLine("Based on the context above, please answer the user's question naturally in a full sentence.");
        
        string promptString = finalPrompt.ToString();
        _results.Clear();
        
        await context.YieldOutputAsync(promptString, cancellationToken);
        return promptString;
    }
}

public class ChatGenExecutor : Executor<string, string>
{
    private readonly ChatAgent _chatAgent;
    public System.Action<string> OnTokenReceived { get; set; }

    public ChatGenExecutor(ChatAgent agent) : base("ChatGenExecutor")
    {
        _chatAgent = agent;
    }

    public override async ValueTask<string> HandleAsync(string prompt, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;
        if (_chatAgent == null) return "Error: ChatAgent missing.";
        
        return await _chatAgent.GenerateResponseAsync(prompt, OnTokenReceived);
    }
}