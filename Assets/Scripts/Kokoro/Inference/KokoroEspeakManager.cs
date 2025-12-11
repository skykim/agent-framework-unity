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
    public class KokoroEspeakManager : MonoBehaviour
    {
        [Header("Model & Voice Settings")]
        public ModelAsset modelAsset;
        public TextAsset voiceAsset;

        [Header("Speech Settings")]
        [Range(0.5f, 2.0f)]
        public float speed = 1.0f;
        public string langCode = "en-us";

        private KokoroHandler _handler;
        private KokoroESpeakTokenizer _tokenizer;
        private bool _isInitialized = false;

        private const int k_SampleRate = 24000;

        void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        IEnumerator InitializeRoutine()
        {
            string espeakDataPath;

#if UNITY_ANDROID && !UNITY_EDITOR
            espeakDataPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data");

            if (!Directory.Exists(espeakDataPath))
            {
                Debug.Log("[KokoroEspeak] Extracting espeak-ng-data...");
                string zipSourcePath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data.zip");
                string zipDestPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data.zip");

                using (UnityWebRequest www = UnityWebRequest.Get(zipSourcePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[KokoroEspeak] Failed to load zip: {www.error}");
                        yield break;
                    }
                    File.WriteAllBytes(zipDestPath, www.downloadHandler.data);
                    
                    try {
                        ZipFile.ExtractToDirectory(zipDestPath, Application.persistentDataPath);
                    } catch (Exception e) {
                        Debug.LogError($"[KokoroEspeak] Zip Error: {e.Message}");
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
                Debug.LogError($"[KokoroEspeak] 'espeak-ng-data' not found at {espeakDataPath}");
                yield break;
            }
#endif

            try
            {
                _tokenizer = new KokoroESpeakTokenizer(espeakDataPath, langCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroEspeak] Tokenizer Init Failed: {e.Message}");
                yield break;
            }

            try
            {
                _handler = new KokoroHandler(modelAsset, BackendType.GPUCompute);
                Debug.Log("[KokoroEspeak] Initialized Successfully.");
                _isInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroEspeak] Handler Init Failed: {e.Message}");
            }
        }

        public async Task<AudioClip> GenerateAudioClip(string text)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[KokoroEspeak] Manager is not initialized.");
                return null;
            }

            if (string.IsNullOrEmpty(text)) return null;

            KokoroHandler.Voice voice = null;

            try
            {
                int[] inputIds = _tokenizer.Tokenize(text);
                
                if (inputIds == null || inputIds.Length == 0)
                {
                    Debug.LogWarning("[KokoroEspeak] Tokenization result is empty.");
                    return null;
                }

                voice = KokoroHandler.GetVoice(voiceAsset);

                using var outputTensor = await _handler.Execute(inputIds, speed, voice);
                
                if (outputTensor == null) return null;

                return AudioClipUtils.ToAudioClip(outputTensor, sampleRate: k_SampleRate, name: "KokoroSpeech");
            }
            catch (Exception e)
            {
                Debug.LogError($"[KokoroEspeak] Generation Error: {e.Message}");
                return null;
            }
            finally
            {
                voice?.Dispose();
            }
        }

        private void OnDestroy()
        {
            _handler?.Dispose();
        }
    }
}