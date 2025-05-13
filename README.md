# LlmEmbeddingsCpu

## Add .onnx file
-> TODO: Decide which one. 

For now just used this one: https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2/blob/main/onnx/model.onnx

Then, put it into the `/src/LlmEmbeddingsCpu.App/deps/sentence-transformers/all-MiniLM-L6-v2` directory. Together with the `tokenizer.json`.

## Executing

```
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj
```

```
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-x64
dotnet run --project src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -r win-arm64

```

## Build
```
dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -c Release -r win-win-arm64 --self-contained true /p:PublishSingleFile=false

dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -c Release -r win-win-arm64 --self-contained true /p:PublishSingleFile=false
```
