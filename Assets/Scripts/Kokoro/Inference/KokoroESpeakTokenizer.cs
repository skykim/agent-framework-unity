using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Unity.InferenceEngine.Samples.TTS.Inference
{
    public class KokoroESpeakTokenizer
    {
        private static readonly Dictionary<string, int> Vocab = new Dictionary<string, int>
        {
            { "$", 0 }, { ";", 1 }, { ":", 2 }, { ",", 3 }, { ".", 4 }, { "!", 5 }, { "?", 6 }, 
            { "—", 9 }, { "…", 10 }, { "\"", 11 }, { "(", 12 }, { ")", 13 }, { "“", 14 }, { "”", 15 }, { " ", 16 },
            { "\u0303", 17 }, { "ʣ", 18 }, { "ʥ", 19 }, { "ʦ", 20 }, { "ʨ", 21 }, { "ᵝ", 22 }, { "\uAB67", 23 },
            { "A", 24 }, { "I", 25 }, { "O", 31 }, { "Q", 33 }, { "S", 35 }, { "T", 36 }, { "W", 39 }, { "Y", 41 },
            { "ᵊ", 42 }, { "a", 43 }, { "b", 44 }, { "c", 45 }, { "d", 46 }, { "e", 47 }, { "f", 48 }, { "h", 50 },
            { "i", 51 }, { "j", 52 }, { "k", 53 }, { "l", 54 }, { "m", 55 }, { "n", 56 }, { "o", 57 }, { "p", 58 },
            { "q", 59 }, { "r", 60 }, { "s", 61 }, { "t", 62 }, { "u", 63 }, { "v", 64 }, { "w", 65 }, { "x", 66 },
            { "y", 67 }, { "z", 68 }, { "ɑ", 69 }, { "ɐ", 70 }, { "ɒ", 71 }, { "æ", 72 }, { "β", 75 }, { "ɔ", 76 },
            { "ɕ", 77 }, { "ç", 78 }, { "ɖ", 80 }, { "ð", 81 }, { "ʤ", 82 }, { "ə", 83 }, { "ɚ", 85 }, { "ɛ", 86 },
            { "ɜ", 87 }, { "ɟ", 90 }, { "ɡ", 92 }, { "ɥ", 99 }, { "ɨ", 101 }, { "ɪ", 102 }, { "ʝ", 103 }, { "ɯ", 110 },
            { "ɰ", 111 }, { "ŋ", 112 }, { "ɳ", 113 }, { "ɲ", 114 }, { "ɴ", 115 }, { "ø", 116 }, { "ɸ", 118 }, { "θ", 119 },
            { "œ", 120 }, { "ɹ", 123 }, { "ɾ", 125 }, { "ɻ", 126 }, { "ʁ", 128 }, { "ɽ", 129 }, { "ʂ", 130 }, { "ʃ", 131 },
            { "ʈ", 132 }, { "ʧ", 133 }, { "ʊ", 135 }, { "ʋ", 136 }, { "ʌ", 138 }, { "ɣ", 139 }, { "ɤ", 140 }, { "χ", 142 },
            { "ʎ", 143 }, { "ʒ", 147 }, { "ʔ", 148 }, { "ˈ", 156 }, { "ˌ", 157 }, { "ː", 158 }, { "ʰ", 162 }, { "ʲ", 164 },
            { "↓", 169 }, { "→", 171 }, { "↗", 172 }, { "↘", 173 }, { "ᵻ", 177 }
        };

        private const int MAX_PHONEME_LENGTH = 510;

        public KokoroESpeakTokenizer(string espeakDataPath, string voiceName = "en-us")
        {
            int initResult = ESpeakNG.espeak_Initialize(0, 0, espeakDataPath, 0);
            if (initResult < 0)
            {
                Debug.LogError($"[KokoroESpeak] Espeak initialization failed: {initResult}");
                return;
            }
            ESpeakNG.espeak_SetVoiceByName(voiceName);
        }

        public int[] TokenizeGraphemes(string text)
        {
            return Tokenize(text);
        }

        public int[] Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();

            string phonemeString = GetPhonemesFromEspeak(text);
            
            if (phonemeString.Length > MAX_PHONEME_LENGTH)
            {
                Debug.LogWarning($"Text too long. Truncating to {MAX_PHONEME_LENGTH} phonemes.");
                phonemeString = phonemeString.Substring(0, MAX_PHONEME_LENGTH);
            }

            List<int> tokenIds = new List<int>();
            foreach (char p in phonemeString)
            {
                if (Vocab.TryGetValue(p.ToString(), out int id))
                {
                    tokenIds.Add(id);
                }
            }

            return tokenIds.ToArray();
        }

        private string GetPhonemesFromEspeak(string text)
        {
            IntPtr textPtr = IntPtr.Zero;
            try
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text + "\0");
                textPtr = Marshal.AllocHGlobal(textBytes.Length);
                Marshal.Copy(textBytes, 0, textPtr, textBytes.Length);

                IntPtr pointerToText = textPtr;
                int textMode = 0; 
                int phonemeMode = 2; 

                StringBuilder sb = new StringBuilder();
                
                int safetyLoopCount = 0;
                long lastPointerAddress = pointerToText.ToInt64();
                const int MAX_LOOPS = 2000;

                while (true)
                {
                    if (safetyLoopCount++ > MAX_LOOPS) break;

                    IntPtr resultPtr = ESpeakNG.espeak_TextToPhonemes(ref pointerToText, textMode, phonemeMode);

                    if (resultPtr == IntPtr.Zero) break;

                    long currentPointerAddress = pointerToText.ToInt64();
                    if (currentPointerAddress == lastPointerAddress) break;
                    lastPointerAddress = currentPointerAddress;

                    string chunk = PtrToUtf8String(resultPtr).Trim();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append(chunk);
                    }
                }

                return sb.ToString().Trim();
            }
            finally
            {
                if (textPtr != IntPtr.Zero) Marshal.FreeHGlobal(textPtr);
            }
        }

        private static string PtrToUtf8String(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "";
            var byteList = new List<byte>();
            for (int offset = 0; ; offset++)
            {
                byte b = Marshal.ReadByte(ptr, offset);
                if (b == 0) break;
                byteList.Add(b);
            }
            return Encoding.UTF8.GetString(byteList.ToArray());
        }
    }
}