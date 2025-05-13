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

dotnet publish src/LlmEmbeddingsCpu.App/LlmEmbeddingsCpu.App.csproj -c Release -r win-arm64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -p:DebugType=None -p:DebugSymbols=false

-> files are then logged to "$Env:TEMP\.net". open with  explorer "$Env:TEMP\.net"
```


## Removing a service 

```
C:\Program Files (x86)\LLM Embeddings CPU> .\nssm.exe remove LlmEmbeddingsCpuService confirm
```


## How to make the final output smaller 
- remove nssm
- remove the other models


## See the task 
```
schtasks /Query /TN "LLMEmbeddingsCpuHooks" /V /FO LIST
```

Delete the task 
```
schtasks /Delete /TN "LLMEmbeddingsCpuHooks" /F
```

Run it once
```
schtasks /Run   /TN "LLMEmbeddingsCpuHooks"
``` 

```
# run in an elevated PowerShell window
$app = 'C:\Program Files\LLM Embeddings CPU'

schtasks /Create /F /RL HIGHEST /SC ONLOGON /DELAY 0000:10 `
        /TN "LLMEmbeddingsCpuHooks" `
        /TR "`"$app\LlmEmbeddingsCpu.App.exe`""

Write-Host "Exit code $LASTEXITCODE"
```