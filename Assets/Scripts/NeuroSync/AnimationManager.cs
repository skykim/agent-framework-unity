using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.InferenceEngine.Samples.TTS;

public class AnimationManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private KokoroEspeakManager kokoroManager;
    [SerializeField] private BlendshapeManager blendshapeManager;

    private CancellationTokenSource _cts;

    public async Task ProcessAndPlayAsync(string fullText)
    {
        if (kokoroManager == null || blendshapeManager == null) return;
        if (string.IsNullOrWhiteSpace(fullText)) return;

        StopAll(); 
        
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            string sentencePattern = @"([.?!]+)";

            string[] parts = Regex.Split(fullText, sentencePattern);

            var sentencesToPlay = new List<string>();

            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                if (string.IsNullOrEmpty(trimmedPart)) continue;

                bool isPunctuation = Regex.IsMatch(trimmedPart, "^" + sentencePattern + "$");

                if (isPunctuation)
                {
                    if (sentencesToPlay.Count > 0)
                    {
                        sentencesToPlay[sentencesToPlay.Count - 1] += trimmedPart;
                    }
                    else
                    {
                        sentencesToPlay.Add(trimmedPart);
                    }
                }
                else
                {
                    sentencesToPlay.Add(trimmedPart);
                }
            }

            foreach (string sentence in sentencesToPlay)
            {
                if (token.IsCancellationRequested) break;

                await PlayChunkAsync(sentence, token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Animation Error: {e.Message}");
        }
    }

    private async Task PlayChunkAsync(string text, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;

        AudioClip clip = await kokoroManager.GenerateAudioClip(text);
        if (token.IsCancellationRequested || clip == null) return;

        List<float[]> shapes = await blendshapeManager.FetchBlendshapesAsync(clip);
        if (token.IsCancellationRequested) return;

        if (shapes != null && shapes.Count > 0)
        {
            Task playTask = blendshapeManager.PlaySequenceAsync(clip, shapes);
            try
            {
                await playTask;
            }
            catch (System.Exception)
            {
            }
        }
    }

    public void StopAll()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (blendshapeManager != null)
        {
            blendshapeManager.Stop();
        }
    }
}