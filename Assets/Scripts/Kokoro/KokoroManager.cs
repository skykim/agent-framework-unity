using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Unity.InferenceEngine;
using Unity.InferenceEngine.Samples.TTS.Inference;
using Unity.InferenceEngine.Samples.TTS.Utils;

namespace Unity.InferenceEngine.Samples.TTS
{
    [RequireComponent(typeof(AudioSource))]
    public class KokoroManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float speed = 1.0f;
        [SerializeField] private string langCode = "en-us";

        [Header("Resources")]
        public ModelAsset kokoroModelAsset;
        public TextAsset kokoroVoiceAsset;

        private KokoroHandler m_KokoroHandler;
        private KokoroESpeakTokenizer m_Tokenizer;
        private bool m_IsInitialized = false;

        private const int k_SampleRate = 24000;

        private void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        private IEnumerator InitializeRoutine()
        {
            string espeakDataPath;

#if UNITY_ANDROID && !UNITY_EDITOR
            espeakDataPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data");

            if (!Directory.Exists(espeakDataPath))
            {
                Debug.Log("[KokoroManager] Extracting espeak-ng-data...");
                string zipSourcePath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data.zip");
                string zipDestPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data.zip");

                using (UnityWebRequest www = UnityWebRequest.Get(zipSourcePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[KokoroManager] Failed to load zip: {www.error}");
                        yield break;
                    }
                    File.WriteAllBytes(zipDestPath, www.downloadHandler.data);
                    
                    try {
                        ZipFile.ExtractToDirectory(zipDestPath, Application.persistentDataPath);
                    } catch (Exception e) {
                        Debug.LogError($"[KokoroManager] Zip Error: {e.Message}");
                        yield break;
                    } finally {
                        if (File.Exists(zipDestPath)) File.Delete(zipDestPath);
                    }
                }
            }
#else
            espeakDataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            if (!Directory.Exists(espeakDataPath))
            {
                Debug.LogError($"[KokoroManager] 'espeak-ng-data' folder not found at {espeakDataPath}");
                yield break;
            }
#endif

            try
            {
                m_Tokenizer = new KokoroESpeakTokenizer(espeakDataPath, langCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroManager] Tokenizer Init Failed: {e.Message}");
                yield break;
            }

            try
            {
                m_KokoroHandler = new KokoroHandler(kokoroModelAsset, backendType: BackendType.GPUCompute);
                m_IsInitialized = true;
                Debug.Log("[KokoroManager] Initialized Successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroManager] Handler Init Failed: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            m_KokoroHandler?.Dispose();
            m_KokoroHandler = null;
        }

        public async Task<AudioClip> GenerateAudioClip(string text)
        {
            if (!m_IsInitialized || m_KokoroHandler == null)
            {
                Debug.LogError("[KokoroManager] Manager is not initialized.");
                return null;
            }

            if (string.IsNullOrEmpty(text)) return null;

            KokoroHandler.Voice voice = null;

            try
            {
                int[] tokens = m_Tokenizer.Tokenize(text);

                if (tokens == null || tokens.Length == 0)
                {
                    Debug.LogWarning("[KokoroManager] Tokenization result is empty.");
                    return null;
                }

                voice = KokoroHandler.GetVoice(kokoroVoiceAsset);

                using var output = await m_KokoroHandler.Execute(tokens, speed: speed, voice);
                
                if (output == null) return null;

                float[] audioData = output.DownloadToArray();

                if (audioData == null || audioData.Length == 0) return null;

                AudioClip clip = AudioClip.Create("KokoroSpeech", audioData.Length, 1, k_SampleRate, false);
                clip.SetData(audioData, 0);

                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroManager] Generation Error: {e.Message}");
                return null;
            }
            finally
            {
                voice?.Dispose();
            }
        }
    }
}