using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using System.Text;
using Unity.Collections;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class RunWhisper : MonoBehaviour
{
    Worker decoder1, decoder2, encoder, spectrogram;
    Worker argmax;

    const int maxTokens = 100;

    const int END_OF_TEXT = 50257;
    const int START_OF_TRANSCRIPT = 50258;
    const int ENGLISH = 50259;
    const int KOREAN = 50264;
    const int TRANSCRIBE = 50359; 
    const int NO_TIME_STAMPS = 50363;

    int numSamples;
    string[] tokens;

    int tokenCount = 0;
    NativeArray<int> outputTokens;

    int[] whiteSpaceCharacters = new int[256];

    Tensor<float> encodedAudio;
    string outputString = "";

    const int maxSamples = 30 * 16000;

    public ModelAsset audioDecoder1, audioDecoder2;
    public ModelAsset audioEncoder;
    public ModelAsset logMelSpectro;
    public TextAsset vocabAsset;

    NativeArray<int> lastToken;
    Tensor<int> lastTokenTensor;
    Tensor<int> tokensTensor;
    Tensor<float> audioInput;
    
    private bool isInitialized = false;

    public async void Start()
    {
        SetupWhiteSpaceShifts();
        GetTokens();

        decoder1 = new Worker(ModelLoader.Load(audioDecoder1), BackendType.GPUCompute);
        decoder2 = new Worker(ModelLoader.Load(audioDecoder2), BackendType.GPUCompute);
        encoder = new Worker(ModelLoader.Load(audioEncoder), BackendType.GPUCompute);
        spectrogram = new Worker(ModelLoader.Load(logMelSpectro), BackendType.GPUCompute);

        FunctionalGraph graph = new FunctionalGraph();
        var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
        var amax = Functional.ArgMax(input, -1, false);
        var selectTokenModel = graph.Compile(amax);
        argmax = new Worker(selectTokenModel, BackendType.GPUCompute);

        outputTokens = new NativeArray<int>(maxTokens, Allocator.Persistent);
        lastToken = new NativeArray<int>(1, Allocator.Persistent);
        
        tokensTensor = new Tensor<int>(new TensorShape(1, maxTokens));
        ComputeTensorData.Pin(tokensTensor);
        
        lastTokenTensor = new Tensor<int>(new TensorShape(1, 1));

        Debug.Log("[Whisper] Models Loaded. Warming up...");
        await WarmStart();
        isInitialized = true;
        Debug.Log("[Whisper] Ready.");
    }

    private async Awaitable WarmStart()
    {
        float[] dummyAudio = new float[maxSamples];
        
        SetupAudioTensor(dummyAudio);
        EncodeAudio();
        
        ResetTokens();
        
        decoder1.SetInput("input_ids", tokensTensor);
        decoder1.SetInput("encoder_hidden_states", encodedAudio);
        decoder1.Schedule();
        
        try 
        {
            await InferenceStep(); 
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Whisper] Warmup warning (ignorable): {e.Message}");
        }
    }

    public async Awaitable<string> Transcribe(float[] audioData)
    {
        if (!isInitialized)
        {
            Debug.LogError("[Whisper] Whisper models are not initialized yet.");
            return "";
        }

        outputString = "";
        ResetTokens();

        SetupAudioTensor(audioData);
        EncodeAudio();

        bool running = true;
        while (running)
        {
            if (tokenCount >= maxTokens - 1)
            {
                break;
            }

            await InferenceStep();

            int currentToken = lastToken[0];
            if (currentToken == END_OF_TEXT)
            {
                running = false;
            }
        }

        string finalString = GetUnicodeText(outputString);
        Debug.Log($"[Whisper] Transcription: {finalString}");
        
        return finalString;
    }

    void ResetTokens()
    {
        tokenCount = 0;
        
        outputTokens[0] = START_OF_TRANSCRIPT;
        outputTokens[1] = ENGLISH; 
        outputTokens[2] = TRANSCRIBE;
        
        tokenCount = 3;

        tokensTensor.Reshape(new TensorShape(1, tokenCount));
        tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);

        lastToken[0] = NO_TIME_STAMPS;
        lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);
    }

    void SetupAudioTensor(float[] rawData)
    {
        if (audioInput != null) audioInput.Dispose();

        float[] paddedData = new float[maxSamples];
        int lengthToCopy = Mathf.Min(rawData.Length, maxSamples);
        System.Array.Copy(rawData, paddedData, lengthToCopy);

        audioInput = new Tensor<float>(new TensorShape(1, maxSamples), paddedData);
    }

    void EncodeAudio()
    {
        spectrogram.Schedule(audioInput);
        var logmel = spectrogram.PeekOutput() as Tensor<float>;
        encoder.Schedule(logmel);
        encodedAudio = encoder.PeekOutput() as Tensor<float>;
    }

    async Awaitable InferenceStep()
    {
        decoder1.SetInput("input_ids", tokensTensor);
        decoder1.SetInput("encoder_hidden_states", encodedAudio);
        decoder1.Schedule();

        var past_key_values_0_decoder_key = decoder1.PeekOutput("present.0.decoder.key") as Tensor<float>;
        var past_key_values_0_decoder_value = decoder1.PeekOutput("present.0.decoder.value") as Tensor<float>;
        var past_key_values_1_decoder_key = decoder1.PeekOutput("present.1.decoder.key") as Tensor<float>;
        var past_key_values_1_decoder_value = decoder1.PeekOutput("present.1.decoder.value") as Tensor<float>;
        var past_key_values_2_decoder_key = decoder1.PeekOutput("present.2.decoder.key") as Tensor<float>;
        var past_key_values_2_decoder_value = decoder1.PeekOutput("present.2.decoder.value") as Tensor<float>;
        var past_key_values_3_decoder_key = decoder1.PeekOutput("present.3.decoder.key") as Tensor<float>;
        var past_key_values_3_decoder_value = decoder1.PeekOutput("present.3.decoder.value") as Tensor<float>;
        var past_key_values_4_decoder_key = decoder1.PeekOutput("present.4.decoder.key") as Tensor<float>;
        var past_key_values_4_decoder_value = decoder1.PeekOutput("present.4.decoder.value") as Tensor<float>;
        var past_key_values_5_decoder_key = decoder1.PeekOutput("present.5.decoder.key") as Tensor<float>;
        var past_key_values_5_decoder_value = decoder1.PeekOutput("present.5.decoder.value") as Tensor<float>;
        var past_key_values_6_decoder_key = decoder1.PeekOutput("present.6.decoder.key") as Tensor<float>;
        var past_key_values_6_decoder_value = decoder1.PeekOutput("present.6.decoder.value") as Tensor<float>;
        var past_key_values_7_decoder_key = decoder1.PeekOutput("present.7.decoder.key") as Tensor<float>;
        var past_key_values_7_decoder_value = decoder1.PeekOutput("present.7.decoder.value") as Tensor<float>;
        var past_key_values_8_decoder_key = decoder1.PeekOutput("present.8.decoder.key") as Tensor<float>;
        var past_key_values_8_decoder_value = decoder1.PeekOutput("present.8.decoder.value") as Tensor<float>;
        var past_key_values_9_decoder_key = decoder1.PeekOutput("present.9.decoder.key") as Tensor<float>;
        var past_key_values_9_decoder_value = decoder1.PeekOutput("present.9.decoder.value") as Tensor<float>;
        var past_key_values_10_decoder_key = decoder1.PeekOutput("present.10.decoder.key") as Tensor<float>;
        var past_key_values_10_decoder_value = decoder1.PeekOutput("present.10.decoder.value") as Tensor<float>;
        var past_key_values_11_decoder_key = decoder1.PeekOutput("present.11.decoder.key") as Tensor<float>;
        var past_key_values_11_decoder_value = decoder1.PeekOutput("present.11.decoder.value") as Tensor<float>;

        var past_key_values_0_encoder_key = decoder1.PeekOutput("present.0.encoder.key") as Tensor<float>;
        var past_key_values_0_encoder_value = decoder1.PeekOutput("present.0.encoder.value") as Tensor<float>;
        var past_key_values_1_encoder_key = decoder1.PeekOutput("present.1.encoder.key") as Tensor<float>;
        var past_key_values_1_encoder_value = decoder1.PeekOutput("present.1.encoder.value") as Tensor<float>;
        var past_key_values_2_encoder_key = decoder1.PeekOutput("present.2.encoder.key") as Tensor<float>;
        var past_key_values_2_encoder_value = decoder1.PeekOutput("present.2.encoder.value") as Tensor<float>;
        var past_key_values_3_encoder_key = decoder1.PeekOutput("present.3.encoder.key") as Tensor<float>;
        var past_key_values_3_encoder_value = decoder1.PeekOutput("present.3.encoder.value") as Tensor<float>;
        var past_key_values_4_encoder_key = decoder1.PeekOutput("present.4.encoder.key") as Tensor<float>;
        var past_key_values_4_encoder_value = decoder1.PeekOutput("present.4.encoder.value") as Tensor<float>;
        var past_key_values_5_encoder_key = decoder1.PeekOutput("present.5.encoder.key") as Tensor<float>;
        var past_key_values_5_encoder_value = decoder1.PeekOutput("present.5.encoder.value") as Tensor<float>;
        var past_key_values_6_encoder_key = decoder1.PeekOutput("present.6.encoder.key") as Tensor<float>;
        var past_key_values_6_encoder_value = decoder1.PeekOutput("present.6.encoder.value") as Tensor<float>;
        var past_key_values_7_encoder_key = decoder1.PeekOutput("present.7.encoder.key") as Tensor<float>;
        var past_key_values_7_encoder_value = decoder1.PeekOutput("present.7.encoder.value") as Tensor<float>;
        var past_key_values_8_encoder_key = decoder1.PeekOutput("present.8.encoder.key") as Tensor<float>;
        var past_key_values_8_encoder_value = decoder1.PeekOutput("present.8.encoder.value") as Tensor<float>;
        var past_key_values_9_encoder_key = decoder1.PeekOutput("present.9.encoder.key") as Tensor<float>;
        var past_key_values_9_encoder_value = decoder1.PeekOutput("present.9.encoder.value") as Tensor<float>;
        var past_key_values_10_encoder_key = decoder1.PeekOutput("present.10.encoder.key") as Tensor<float>;
        var past_key_values_10_encoder_value = decoder1.PeekOutput("present.10.encoder.value") as Tensor<float>;
        var past_key_values_11_encoder_key = decoder1.PeekOutput("present.11.encoder.key") as Tensor<float>;
        var past_key_values_11_encoder_value = decoder1.PeekOutput("present.11.encoder.value") as Tensor<float>;

        decoder2.SetInput("input_ids", lastTokenTensor);

        decoder2.SetInput("past_key_values.0.decoder.key", past_key_values_0_decoder_key);
        decoder2.SetInput("past_key_values.0.decoder.value", past_key_values_0_decoder_value);
        decoder2.SetInput("past_key_values.1.decoder.key", past_key_values_1_decoder_key);
        decoder2.SetInput("past_key_values.1.decoder.value", past_key_values_1_decoder_value);
        decoder2.SetInput("past_key_values.2.decoder.key", past_key_values_2_decoder_key);
        decoder2.SetInput("past_key_values.2.decoder.value", past_key_values_2_decoder_value);
        decoder2.SetInput("past_key_values.3.decoder.key", past_key_values_3_decoder_key);
        decoder2.SetInput("past_key_values.3.decoder.value", past_key_values_3_decoder_value);
        decoder2.SetInput("past_key_values.4.decoder.key", past_key_values_4_decoder_key);
        decoder2.SetInput("past_key_values.4.decoder.value", past_key_values_4_decoder_value);
        decoder2.SetInput("past_key_values.5.decoder.key", past_key_values_5_decoder_key);
        decoder2.SetInput("past_key_values.5.decoder.value", past_key_values_5_decoder_value);
        decoder2.SetInput("past_key_values.6.decoder.key", past_key_values_6_decoder_key);
        decoder2.SetInput("past_key_values.6.decoder.value", past_key_values_6_decoder_value);
        decoder2.SetInput("past_key_values.7.decoder.key", past_key_values_7_decoder_key);
        decoder2.SetInput("past_key_values.7.decoder.value", past_key_values_7_decoder_value);
        decoder2.SetInput("past_key_values.8.decoder.key", past_key_values_8_decoder_key);
        decoder2.SetInput("past_key_values.8.decoder.value", past_key_values_8_decoder_value);
        decoder2.SetInput("past_key_values.9.decoder.key", past_key_values_9_decoder_key);
        decoder2.SetInput("past_key_values.9.decoder.value", past_key_values_9_decoder_value);
        decoder2.SetInput("past_key_values.10.decoder.key", past_key_values_10_decoder_key);
        decoder2.SetInput("past_key_values.10.decoder.value", past_key_values_10_decoder_value);
        decoder2.SetInput("past_key_values.11.decoder.key", past_key_values_11_decoder_key);
        decoder2.SetInput("past_key_values.11.decoder.value", past_key_values_11_decoder_value);

        decoder2.SetInput("past_key_values.0.encoder.key", past_key_values_0_encoder_key);
        decoder2.SetInput("past_key_values.0.encoder.value", past_key_values_0_encoder_value);
        decoder2.SetInput("past_key_values.1.encoder.key", past_key_values_1_encoder_key);
        decoder2.SetInput("past_key_values.1.encoder.value", past_key_values_1_encoder_value);
        decoder2.SetInput("past_key_values.2.encoder.key", past_key_values_2_encoder_key);
        decoder2.SetInput("past_key_values.2.encoder.value", past_key_values_2_encoder_value);
        decoder2.SetInput("past_key_values.3.encoder.key", past_key_values_3_encoder_key);
        decoder2.SetInput("past_key_values.3.encoder.value", past_key_values_3_encoder_value);
        decoder2.SetInput("past_key_values.4.encoder.key", past_key_values_4_encoder_key);
        decoder2.SetInput("past_key_values.4.encoder.value", past_key_values_4_encoder_value);
        decoder2.SetInput("past_key_values.5.encoder.key", past_key_values_5_encoder_key);
        decoder2.SetInput("past_key_values.5.encoder.value", past_key_values_5_encoder_value);
        decoder2.SetInput("past_key_values.6.encoder.key", past_key_values_6_encoder_key);
        decoder2.SetInput("past_key_values.6.encoder.value", past_key_values_6_encoder_value);
        decoder2.SetInput("past_key_values.7.encoder.key", past_key_values_7_encoder_key);
        decoder2.SetInput("past_key_values.7.encoder.value", past_key_values_7_encoder_value);
        decoder2.SetInput("past_key_values.8.encoder.key", past_key_values_8_encoder_key);
        decoder2.SetInput("past_key_values.8.encoder.value", past_key_values_8_encoder_value);
        decoder2.SetInput("past_key_values.9.encoder.key", past_key_values_9_encoder_key);
        decoder2.SetInput("past_key_values.9.encoder.value", past_key_values_9_encoder_value);
        decoder2.SetInput("past_key_values.10.encoder.key", past_key_values_10_encoder_key);
        decoder2.SetInput("past_key_values.10.encoder.value", past_key_values_10_encoder_value);
        decoder2.SetInput("past_key_values.11.encoder.key", past_key_values_11_encoder_key);
        decoder2.SetInput("past_key_values.11.encoder.value", past_key_values_11_encoder_value);

        decoder2.Schedule();

        var logits = decoder2.PeekOutput("logits") as Tensor<float>;
        argmax.Schedule(logits);
        
        using var t_Token = await argmax.PeekOutput().ReadbackAndCloneAsync() as Tensor<int>;
        int index = t_Token[0];

        outputTokens[tokenCount] = lastToken[0];
        lastToken[0] = index;
        tokenCount++;
        
        tokensTensor.Reshape(new TensorShape(1, tokenCount));
        tokensTensor.dataOnBackend.Upload<int>(outputTokens, tokenCount);
        lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

        if (index != END_OF_TEXT && index < tokens.Length)
        {
            outputString += tokens[index];
        }
    }

    void GetTokens()
    {
        var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabAsset.text);
        tokens = new string[vocab.Count];
        foreach (var item in vocab)
        {
            tokens[item.Value] = item.Key;
        }
    }

    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter : (char)whiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('�' <= c && c <= '�') || ('�' <= c && c <= '�'));
    }

    private void OnDestroy()
    {
        decoder1?.Dispose();
        decoder2?.Dispose();
        encoder?.Dispose();
        spectrogram?.Dispose();
        argmax?.Dispose();
        audioInput?.Dispose();
        lastTokenTensor?.Dispose();
        tokensTensor?.Dispose();
        
        if (outputTokens.IsCreated) outputTokens.Dispose();
        if (lastToken.IsCreated) lastToken.Dispose();
    }
}