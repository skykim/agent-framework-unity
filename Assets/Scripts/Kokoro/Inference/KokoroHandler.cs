using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.InferenceEngine.Samples.TTS.Utils;
using UnityEngine;

namespace Unity.InferenceEngine.Samples.TTS.Inference
{
    public class KokoroHandler: IDisposable
    {
        Model m_Model;
        Worker m_Worker;
        readonly BackendType m_BackendType;

        public KokoroHandler(ModelAsset modelAsset, BackendType backendType = BackendType.GPUCompute)
        {
            m_BackendType = backendType;
            
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);
        }
        public async Task<Tensor<float>> Execute(int[] inputIds, float speed, Voice voice)
        {
            // Add the pad ids
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            using var inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            using var speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });
            using var voiceTensor = await GetVoiceVector(inputIdsTensor, voice.Tensor);

            return await Execute(inputIdsTensor, voiceTensor, speedTensor);
        }

        public async Task<Tensor<float>> Execute(Tensor<int> inputIdsTensor, Tensor<float> voiceTensor, Tensor<float> speedTensor)
        {
            m_Worker.Schedule(inputIdsTensor, voiceTensor, speedTensor);
            using var result = m_Worker.PeekOutput() as Tensor<float>;
            using var output = await result.ReadbackAndCloneAsync();

            var processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(output);
            return processedOutput;
        }

        public static Voice GetVoice(TextAsset voiceAsset)
        {
            var voiceData = voiceAsset.bytes;

            var voiceArray = new float[voiceData.Length / sizeof(float)];
            Buffer.BlockCopy(voiceData, 0, voiceArray, 0, voiceData.Length);

            var styleShape = new TensorShape(voiceArray.Length / 256, 1, 256);
            var tensor = new Tensor<float>(styleShape, voiceArray);
            var voice = new Voice(voiceAsset.name, tensor);
            return voice;
        }

        async Task<Tensor<float>> GetVoiceVector(Tensor<int> inputIds, Tensor<float> voice)
        {
            var graph = new FunctionalGraph();
            var tokenInput = graph.AddInput<float>(voice.shape, "voice");
            var output = tokenInput[inputIds.count];
            graph.AddOutput(output, "output");
            var model = graph.Compile();

            using var worker = new Worker(model, m_BackendType);
            worker.Schedule(voice);
            using var result = worker.PeekOutput() as Tensor<float>;
            return await result.ReadbackAndCloneAsync();
        }

        public void Dispose()
        {
            m_Worker?.Dispose();
            m_Worker = null;
        }

        public class Voice: IDisposable
        {
            public string Name;
            public Tensor<float> Tensor;

            public Voice(string name, Tensor<float> data)
            {
                Name = name;
                Tensor = data;
            }
            public void Dispose()
            {
                Tensor?.Dispose();
            }
        }
    }
}
