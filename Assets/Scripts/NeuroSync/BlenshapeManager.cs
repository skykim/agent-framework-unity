using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[RequireComponent(typeof(AudioSource))]
public class BlendshapeManager : MonoBehaviour
{
    private static string AudioToBlendshapeUrl = "http://localhost:5005/audio_to_blendshapes";

    [Header("Components")]
    [SerializeField] private BlendshapeAdapter blendshapeAdapter;
    
    private AudioSource audioSource;
    
    public bool IsPlaying { get; private set; } = false;

    private float playbackTime = 0f;
    private List<BlendshapeFrame> currentFrames = new List<BlendshapeFrame>();
    private const int TARGET_FRAMERATE = 60;

    public class BlendshapeFrame { public Dictionary<string, float> values = new Dictionary<string, float>(); }
    private class BlendshapesResponse { public List<float[]> blendshapes { get; set; } }

    void Awake()
    {
        Application.targetFrameRate = TARGET_FRAMERATE;
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;

        if (blendshapeAdapter != null)
        {
            blendshapeAdapter.Initialize();
        }
        else
        {
            Debug.LogError("[BlendshapeManager] BlendshapeAdapter is missing!");
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        playbackTime = 0f;
        
        if (blendshapeAdapter != null) 
            blendshapeAdapter.ResetAll();
            
        if (audioSource != null && audioSource.isPlaying) 
            audioSource.Stop();
    }

    public async Task<List<float[]>> FetchBlendshapesAsync(AudioClip audioClip)
    {
        if (audioClip == null) return null;

        byte[] wavData = AudioClipToWav(audioClip);

        using (UnityWebRequest request = new UnityWebRequest(AudioToBlendshapeUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(wavData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "audio/wav");

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try 
                {
                    var response = JsonConvert.DeserializeObject<BlendshapesResponse>(request.downloadHandler.text);
                    return response.blendshapes;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BlendshapeManager] JSON Parse Error: {ex.Message}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"[BlendshapeManager] API Error: {request.error}");
                return null;
            }
        }
    }

    public async Task PlaySequenceAsync(AudioClip clip, List<float[]> rawBlendshapes)
    {
        Stop();

        currentFrames = ConvertToBlendshapeFrames(rawBlendshapes);
        audioSource.clip = clip;
        
        audioSource.Play();
        IsPlaying = true;
        playbackTime = 0f;

        while (IsPlaying)
        {
            await Task.Yield();
        }
    }

    void Update()
    {
        if (!IsPlaying) return;

        playbackTime += Time.deltaTime;
        
        int targetFrame = Mathf.FloorToInt(playbackTime * TARGET_FRAMERATE);
        bool isAnimationFinished = (currentFrames != null) && (targetFrame >= currentFrames.Count);

        if (currentFrames.Count > 0 && isAnimationFinished)
        {
            Stop();
            return;
        }

        if (currentFrames != null && targetFrame < currentFrames.Count)
        {
            ApplyBlendshapes(currentFrames[targetFrame]);
        }
    }

    private void ApplyBlendshapes(BlendshapeFrame frame)
    {
        if (blendshapeAdapter == null) return;

        foreach (var kvp in frame.values)
        {
            blendshapeAdapter.SetBlendshapeWeight(kvp.Key, kvp.Value);
        }
    }

    private List<BlendshapeFrame> ConvertToBlendshapeFrames(List<float[]> blendshapes)
    {
        if (blendshapes == null) return new List<BlendshapeFrame>();

        string[] blendshapeNames = new string[]
        {
            "eyeBlinkLeft", "eyeLookDownLeft", "eyeLookInLeft", "eyeLookOutLeft", "eyeLookUpLeft", 
            "eyeSquintLeft", "eyeWideLeft", "eyeBlinkRight", "eyeLookDownRight", "eyeLookInRight",
            "eyeLookOutRight", "eyeLookUpRight", "eyeSquintRight", "eyeWideRight", "jawForward",
            "jawRight", "jawLeft", "jawOpen", "mouthClose", "mouthFunnel", "mouthPucker",
            "mouthRight", "mouthLeft", "mouthSmileLeft", "mouthSmileRight", "mouthFrownLeft",
            "mouthFrownRight", "mouthDimpleLeft", "mouthDimpleRight", "mouthStretchLeft",
            "mouthStretchRight", "mouthRollLower", "mouthRollUpper", "mouthShrugLower",
            "mouthShrugUpper", "mouthPressLeft", "mouthPressRight", "mouthLowerDownLeft",
            "mouthLowerDownRight", "mouthUpperUpLeft", "mouthUpperUpRight", "browDownLeft",
            "browDownRight", "browInnerUp", "browOuterUpLeft", "browOuterUpRight", "cheekPuff",
            "cheekSquintLeft", "cheekSquintRight", "noseSneerLeft", "noseSneerRight", "tongueOut"
        };

        List<BlendshapeFrame> frames = new List<BlendshapeFrame>();
        
        foreach (var blendshapeArray in blendshapes)
        {
            BlendshapeFrame frame = new BlendshapeFrame();
            for (int i = 0; i < blendshapeArray.Length; i++)
            {
                if(i < blendshapeNames.Length)
                {
                    float val = blendshapeArray[i];
                    if (val > 0.001f) frame.values[blendshapeNames[i]] = val;
                }
            }
            frames.Add(frame);
        }
        
        if (frames.Count > 0)
        {
            var lastFrame = frames.Last();
            
            const int FADE_OUT_FRAMES = 45; 

            for (int i = 1; i <= FADE_OUT_FRAMES; i++) 
            {
                var fadeOutFrame = new BlendshapeFrame();
                float fadeFactor = 1.0f - ((float)i / FADE_OUT_FRAMES);

                foreach (var kvp in lastFrame.values)
                {
                    float fadedValue = kvp.Value * fadeFactor;
                    if (fadedValue > 0.001f)
                    {
                        fadeOutFrame.values[kvp.Key] = fadedValue;
                    }
                }
                frames.Add(fadeOutFrame);
            }
        }
        
        return frames;
    }
    
    private byte[] AudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(0);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2);
                writer.Write((short)(clip.channels * 2));
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);

                foreach (float sample in samples) 
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                    writer.Write(intSample);
                }

                long streamLength = stream.Length;
                stream.Position = 4;
                writer.Write((int)(streamLength - 8));
            }
            return stream.ToArray();
        }
    }
}