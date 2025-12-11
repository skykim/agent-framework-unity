using UnityEngine;
using System.Threading.Tasks;

[RequireComponent(typeof(RunWhisper))]
public class WhisperManager : MonoBehaviour
{
    [Header("References")]
    public RunWhisper whisperEngine;

    [Header("Settings")]
    private const int frequency = 16000; 
    private const int maxDuration = 30; 
    
    private string microphoneDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;

    void Start()
    {
        if (whisperEngine == null)
            whisperEngine = GetComponent<RunWhisper>();

        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("[WhisperManager] No Microphone detected!");
        }
    }

    public void StartRecording()
    {
        if (string.IsNullOrEmpty(microphoneDevice)) return;
        if (isRecording) return;

        isRecording = true;

        if (recordingClip != null) Destroy(recordingClip);
        recordingClip = Microphone.Start(microphoneDevice, false, maxDuration, frequency);
        
        Debug.Log("[WhisperManager] Recording Started...");
    }

    public async Task<string> StopAndTranscribe()
    {
        if (!isRecording) return null;
        if (string.IsNullOrEmpty(microphoneDevice)) return null;

        isRecording = false;

        int position = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);

        Debug.Log("[WhisperManager] Recording Stopped. Transcribing...");

        if (position <= 0 && recordingClip != null) position = recordingClip.samples;

        if (recordingClip == null || position < 1600) 
        {
            Debug.LogWarning("[WhisperManager] Recording too short.");
            return null;
        }

        float[] samples = new float[position];
        recordingClip.GetData(samples, 0);

        string transcribedText = await whisperEngine.Transcribe(samples);

        Debug.Log($"[WhisperManager] Result: {transcribedText}");
        return transcribedText;
    }
}