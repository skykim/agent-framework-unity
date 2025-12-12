# agent-framework-unity

**Project Lupin** is a Local, Multi-Modal Agent Workflow System in Unity powered by Microsoft Agent Framework and Ollama.

[![Agent Framework Unity](https://img.youtube.com/vi/NMB4n3kJn70/0.jpg)](https://www.youtube.com/watch?v=NMB4n3kJn70)

## Overview

**Project Lupin** is an AI Avatar project that implements the **Microsoft Agent Framework** within Unity. It orchestrates a complex, multi-agent workflow entirely on the local device, ensuring privacy and zero-latency inference.

This project demonstrates a "Fan-Out/Fan-In" architecture where multiple specialized agents (Vision, RAG, Prompt Engineering) work in parallel to synthesize context-aware responses. The system creates an immersive conversational experience with the persona of *Arsène Lupin*, powered by local LLMs (Qwen), Vector Databases, and Neural Speech synthesis.

## Features

- **Microsoft Agent Framework**: Implements graph-based multi-agent workflows.
- **Local LLM Inference**: Fully offline capability using Ollama (LLM/VLM/Embedding).
- **Multi-Modal Interaction**:
  - **Vision**: Webcam image based analysis.
  - **Audio**: Speech-to-Text (Whisper) and Text-to-Speech (Kokoro) using Unity Sentis.
- **RAG (Retrieval-Augmented Generation)**: Custom local VectorDB implementation for context retrieval.
- **Modular Architecture**: Decoupled executors for easy extension of agent capabilities.

## Requirements

- **Unity**: `6000.2.10f1`
- **Unity Sentis**: `2.4.1`
- **Microsoft Agent Framework**: `1.0.0-preview`
- **Ollama**: `qwen2.5:3b`, `qwen3-vl:2b`, `qwen3-embedding:4b`
- **External Tools**: `espeak-ng` (for phoneme generation)

## Model Configuration

This project relies on specific local models managed via Ollama and Sentis.

| Role | Model Name | Description |
|------|------------|-------------|
| **LLM (Chat)** | `qwen2.5:3b` | Main conversational agents |
| **VLM (Vision)** | `qwen3-vl:2b` | Visual context analysis |
| **Embedding** | `qwen3-embedding:4b` | Text embedding for RAG |
| **STT** | `Whisper (Small)` | Speech recognition |
| **TTS** | `Kokoro 82M` | High-quality neural speech synthesis |

## Workflow Architecture

The core of this project is the `AgentWorkflowManager`, which constructs a directed graph for processing user inputs.

### 1. Workflow Graph

The system processes user input through the following stages:

1. **Prompt Rewriting Agent**: Refines raw user input into clear, structured queries.
2. **Fan-Out Execution (Parallel)**:
   - **Vision Executor**: Captures a snapshot from the webcam and analyzes it using the VLM.
   - **RAG Executor**: Searches the local VectorDB for relevant context.
   - **Pass-Through**: Retains the original query intent.
3. **Context Aggregator**: Waits for all parallel tasks to complete and combines their outputs into a single prompt.
4. **Chat Generation Agent**: Synthesizes the final response using the aggregated context under the persona of *Arsène Lupin*.

<img width="512" alt="Workflow Graph" src="https://github.com/user-attachments/assets/b4100b54-972f-4926-bb13-e1a95fe441ce" />

### 2. RAG System (Retrieval-Augmented Generation)
- **Source**: *Arsène Lupin* (Project Gutenberg Text) [Link](https://www.gutenberg.org/files/6133/6133-h/6133-h.htm)
- **Vector Database**: Custom local implementation (`ddd_db.bin` in `StreamingAssets`).
  - **Generation**: Accessible via `Tools > Generate VectorDB` in the Unity Editor.
- **Chunking Strategy**: 
  - **Chunk Size**: 500 characters
  - **Overlap**: 100 characters

### 3. Audio Processing
- **Input**: Whisper Small model handles voice command transcription.
- **Output**: Kokoro 82M (ONNX) combined with `espeak-ng` generates phonemes and audio for the agent's voice.

## Getting Started

### 1. Prerequisite: Ollama Setup
Ensure Ollama is installed and the required models are pulled:
```bash
ollama pull qwen2.5:3b
ollama pull qwen3-vl:2b
ollama pull qwen3-embedding:4b
```

### 2. Project Setup
- Clone or download this repository.
- Download the required assets: [Assets.zip](https://drive.google.com/file/d/1j7QWtJ86qNRA9KDl0PApXuDsy3ERWzaV/view?usp=sharing)
- Unzip the `Assets.zip` file and place the contents into the following directories within your Unity project:
  - `/Assets/Models`
  - `/Assets/StreamingAssets`
  
  *(Ensure the folder structure matches exactly to avoid missing reference errors.)*

### 3. Run the Scene
- Open `/Assets/Scenes/LupinScene.unity` in the Unity Editor.
- Press **Play**.
- Toggle **Text Retriever** or **Visual Cue** checkboxes to dynamically enable or disable specific agents.
- Interact by typing directly into the input field, or hold the `]` (Right Bracket) key to issue commands via STT.

## Links

- [Microsoft Agent Framework GitHub](https://github.com/microsoft/agent-framework)
- [Ollama](https://ollama.com/)
- [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@latest/)
- [eSpeak NG](https://github.com/espeak-ng/espeak-ng)
- [Text: Project Gutenberg - Arsène Lupin](https://www.gutenberg.org/files/6133/6133-h/6133-h.htm)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Usage of third-party dependencies is subject to their respective licensing terms.
