using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;

public class AgentWorkflowManager : MonoBehaviour
{
    [Header("Agents")]
    [SerializeField] private ChatAgent chatAgent;
    [SerializeField] private ImageAgent imageAgent;
    [SerializeField] private PromptRewriteAgent promptRewriteAgent;

    [Header("External Systems")]
    [SerializeField] private WhisperManager whisperManager;
    [SerializeField] private AnimationManager animationManager;

    [Header("UI References")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text resultTextUI;
    [SerializeField] private GameObject recordingIndicator;

    [Header("Options")]
    [SerializeField] public Toggle retrieverToggle;
    [SerializeField] public Toggle webcamToggle;

    private Workflow _agentWorkflow;
    
    // Executors
    private PromptRewritingExecutor _rewriteExec;
    private PassThroughExecutor _passThroughExec;
    private VisionExecutor _visionExec;
    private RagExecutor _ragExec;
    private ContextAggregatorExecutor _aggExec;

    private bool isRecordingInput = false;

    private void Awake()
    {
        MainThreadDispatcher.EnsureInitialized();
    }

    private async void Start()
    {
        if (recordingIndicator != null) recordingIndicator.SetActive(false);
        if (inputField != null) inputField.onSubmit.AddListener(OnSubmitChat);

        await VectorDBUtil.Load();

        InitializeWorkflowGraph();
    }

    private void Update()
    {
        HandleVoiceInputKeys();
    }

    private void InitializeWorkflowGraph()
    {
        Debug.Log("[Manager] Initializing Agent Workflow Graph...");

        var startExec = new StartExecutor();
        _rewriteExec = new PromptRewritingExecutor(promptRewriteAgent);
        _passThroughExec = new PassThroughExecutor();
        _visionExec = new VisionExecutor(imageAgent);
        _ragExec = new RagExecutor();
        
        _aggExec = new ContextAggregatorExecutor(expectedCount: 3); 
        
        var chatExec = new ChatGenExecutor(chatAgent);

        chatExec.OnTokenReceived = (token) =>
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                if (resultTextUI != null)
                {
                    resultTextUI.text += token;
                }
            });
        };

        WorkflowBuilder builder = new WorkflowBuilder(startExec);

        builder.AddEdge(startExec, _rewriteExec);
        builder.AddFanOutEdge(_rewriteExec, targets: new ExecutorBinding[] { _visionExec, _ragExec, _passThroughExec });
        builder.AddFanInEdge(sources: new ExecutorBinding[] { _visionExec, _ragExec, _passThroughExec }, _aggExec);
        builder.AddEdge(_aggExec, chatExec).WithOutputFrom(chatExec);

        _agentWorkflow = builder.Build();
        Debug.Log("[Manager] Workflow Graph Initialized.");
    }

    public void OnSubmitChat(TMP_InputField input) => OnSubmitChat(input.text);

    public void OnSubmitChat(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return;

        if (animationManager != null) animationManager.StopAll();
        if (inputField != null) inputField.interactable = false;

        _ = RunAgentWorkflow(userMessage);
    }

    private async Task RunAgentWorkflow(string message)
    {
        try
        {
            if (_agentWorkflow == null)
            {
                Debug.LogError("[Manager] Workflow is not initialized. Call InitializeWorkflowGraph first.");
                return;
            }

            if (resultTextUI != null) resultTextUI.text = "";

            bool isVisionOn = webcamToggle != null && webcamToggle.isOn;
            bool isRagOn = retrieverToggle != null && retrieverToggle.isOn;

            _rewriteExec.IsEnabled = true;
            _visionExec.IsEnabled = isVisionOn;
            _ragExec.IsEnabled = isRagOn;

            Debug.Log($"[Manager] Running Workflow... (Vision: {isVisionOn}, RAG: {isRagOn})");

            await using Run run = await InProcessExecution.RunAsync(_agentWorkflow, message);

            string finalResponse = null;

            foreach (WorkflowEvent evt in run.NewEvents)
            {
                if (evt is ExecutorCompletedEvent executorComplete)
                {
                    if (executorComplete.ExecutorId == "ChatGenExecutor")
                    {
                        string dataStr = executorComplete.Data?.ToString();
                        if (!string.IsNullOrWhiteSpace(dataStr))
                        {
                            finalResponse = dataStr;
                            break;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(finalResponse))
            {
                await ProcessOutput(finalResponse);
            }
            else
            {
                Debug.LogWarning("[Manager] Workflow completed but returned empty response.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Manager] Execution Error: {ex.Message}");
            if (resultTextUI != null) resultTextUI.text = "Error occurred during processing.";
        }
        finally
        {
            if (inputField != null)
            {
                inputField.text = ""; 
                inputField.interactable = true;
                inputField.ActivateInputField();
            }
        }
    }

    private async Task ProcessOutput(string rawSentence)
    {
        string cleanSentence = CleanText(rawSentence);

        if (resultTextUI != null) resultTextUI.text = cleanSentence;
        
        if (animationManager != null)
        {
            await animationManager.ProcessAndPlayAsync(cleanSentence);
        }
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("*", "").Replace("_", "").Replace("#", "").Trim();
    }
    private void HandleVoiceInputKeys()
    {
        if (whisperManager == null) return;
        if (Input.GetKeyDown(KeyCode.RightBracket) && !isRecordingInput) StartVoiceInput();
        if (Input.GetKeyUp(KeyCode.RightBracket) && isRecordingInput) FinishVoiceInput();
    }

    private void StartVoiceInput()
    {
        if (animationManager != null) animationManager.StopAll();
        if (inputField != null) inputField.interactable = false;
        
        isRecordingInput = true;
        if (recordingIndicator != null) recordingIndicator.SetActive(true);
        
        whisperManager.StartRecording();
    }

    private async void FinishVoiceInput()
    {
        isRecordingInput = false;
        if (recordingIndicator != null) recordingIndicator.SetActive(false);
        
        string transcribedText = await whisperManager.StopAndTranscribe();
        
        if (inputField != null)
        {
            inputField.interactable = true;
            if (!string.IsNullOrWhiteSpace(transcribedText))
            {
                inputField.text = transcribedText;
                OnSubmitChat(transcribedText);
            }
            else
            {
                inputField.ActivateInputField();
            }
        }
    }
}