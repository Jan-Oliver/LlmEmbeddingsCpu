
using System.IO;
using LlmEmbeddingsCpu.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;


namespace LlmEmbeddingsCpu.Services.EmbeddingService
{
    public class IntfloatEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly Tokenizers.DotNet.Tokenizer _tokenizer;
        private readonly int _embeddingSize;
        private readonly int _maxSequenceLength;
        private readonly string _modelName;
        private readonly string _modelDirectory;
        private readonly ILogger<IntfloatEmbeddingService> _logger;

        public IntfloatEmbeddingService(
            ILogger<IntfloatEmbeddingService> logger,
            string modelName = "multilingual-e5-small", 
            int embeddingSize = 384,
            int maxSequenceLength = 512)
        {
            _logger = logger;
            _modelName = modelName;
            _embeddingSize = embeddingSize;
            _maxSequenceLength = maxSequenceLength;
            // Look for models in the deps directory
            _modelDirectory = Path.Combine(GetRootDirectory(), "deps", "intfloat", modelName);
        
            string modelPath = Path.Combine(_modelDirectory, "model.onnx");
            string tokenizerPath = Path.Combine(_modelDirectory, "tokenizer.json");

            // Ensure model files exist
            if (!File.Exists(modelPath))
            {
                _logger.LogError("Model file not found at {ModelPath}", modelPath);
                throw new FileNotFoundException($"Model file not found at {modelPath}");
            }

            if (!File.Exists(tokenizerPath))
            {
                _logger.LogError("Tokenizer file not found at {TokenizerPath}", tokenizerPath);
                throw new FileNotFoundException($"Tokenizer file not found at {tokenizerPath}");
            }

            // Initialize tokenizer
            _tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokenizerPath);

            // Setup ONNX session options
            var sessionOptions = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            // Initialize ONNX session
            _session = new InferenceSession(modelPath, sessionOptions);
            
            // Store model name for embedding metadata
            _modelName = Path.GetFileName(_modelDirectory);
        }

        private static string GetRootDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task<Core.Models.Embedding> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Core.Models.Embedding 
                { 
                    SourceText = string.Empty,
                    Vector = new float[_embeddingSize],
                    ModelName = _modelName
                };
            }

            return await Task.Run(() =>
            {
                // Preprocess text - normalize, remove extra whitespace, add prefix
                text = PreprocessText(text);

                // Tokenize
                var tokens = _tokenizer.Encode(text);

                // Generate embedding vector
                var vector = GenerateEmbeddingVector(tokens);

                // Create embedding object
                return new Core.Models.Embedding
                {
                    SourceText = text,
                    Vector = vector,
                    ModelName = _modelName
                };
            });
        }

        public async Task<IEnumerable<Core.Models.Embedding>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            var embeddings = new List<Core.Models.Embedding>();
            
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text);
                embeddings.Add(embedding);
            }
            
            return embeddings;
        }

        private static string PreprocessText(string text)
        {
            // Remove excess whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            // Add prefix
            text = "query: " + text;
            
            return text;
        }

        private float[] GenerateEmbeddingVector(uint[] tokens)
        {
            // Note: If we want to implement batching, we need to 
            // - add a batch dimension to the input tensors
            // - pad the tokens to the actualSequenceLength of the longest entry
            
            // 1. Apply Truncation: Limit the tokens to maxSequenceLength
            uint[] truncatedTokens = tokens.Take(_maxSequenceLength).ToArray();

            // 2. Determine the actual length of the sequence after truncation
            int actualSequenceLength = truncatedTokens.Length;

            // 3. Create input tensors with the fixed maxSequenceLength
            // Note: 1 because we have only a single batch
            var inputIdsTensor = new DenseTensor<long>(new[] { 1, actualSequenceLength });
            var attentionMaskTensor = new DenseTensor<long>(new[] { 1, actualSequenceLength });
            var tokenTypeIdsTensor = new DenseTensor<long>(new[] { 1, actualSequenceLength });


            // 4. Fill the tensors based on the paddedTokens
            for (int i = 0; i < actualSequenceLength; i++)
            {
                // Input IDs are the padded token IDs
                inputIdsTensor[0, i] = truncatedTokens[i];

                // Attention mask is 1 for actual tokens, 0 for padding tokens
                attentionMaskTensor[0, i] = 1;

                // Token type IDs are 0 for single-sentence embeddings
                tokenTypeIdsTensor[0, i] = 0;
            }

            // 5. Create input tensor dictionary
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };

            // 6. Run inference
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs;
            try
            {
                outputs = _session.Run(inputs);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during inference: {ErrorMessage}", ex.Message);
                // Return a zero vector as fallback
                return new float[_embeddingSize];
            }
            
            try
            {
                // 7. Get the model output tensor
                DisposableNamedOnnxValue outputValue = outputs.First();
                var tensor = outputValue.AsTensor<float>();
                
                var dims = tensor.Dimensions.ToArray();
                if (dims.Length == 3)
                {
                    // Apply mean pooling over sequence dimension
                    float[] embedding = MeanPooling(tensor, attentionMaskTensor, actualSequenceLength, _embeddingSize);
                    
                    // Apply L2 normalization
                    NormalizeL2(embedding);
                    
                    return embedding;
                }
                else
                {
                    _logger.LogError("Unexpected output shape: [{OutputShape}]", string.Join(", ", dims));
                    return new float[_embeddingSize];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during inference: {ErrorMessage}", ex.Message);
                // Return a zero vector as fallback
                return new float[_embeddingSize];
            }
            finally
            {
                outputs.Dispose();
            }
        }
        
        // Mean pooling implementation
        private static float[] MeanPooling(Tensor<float> modelOutput, Tensor<long> attentionMask, int seqLength, int embeddingSize)
        {
            float[] result = new float[embeddingSize];
            float[] sumMask = new float[embeddingSize];
            
            // Sum token embeddings weighted by attention mask
            for (int i = 0; i < seqLength; i++)
            {
                float maskValue = attentionMask[0, i];
                
                for (int j = 0; j < embeddingSize; j++)
                {
                    result[j] += modelOutput[0, i, j] * maskValue;
                    sumMask[j] += maskValue;
                }
            }
            
            // Average by dividing by the sum of the mask
            for (int j = 0; j < embeddingSize; j++)
            {
                // Prevent division by zero
                if (sumMask[j] > 1e-9)
                {
                    result[j] /= sumMask[j];
                }
            }
            
            return result;
        }
        
        // L2 normalization function
        private static void NormalizeL2(float[] vector)
        {
            float squaredSum = 0;
            
            // Calculate squared sum
            for (int i = 0; i < vector.Length; i++)
            {
                squaredSum += vector[i] * vector[i];
            }
            
            // Normalize only if squared sum is positive
            if (squaredSum > 1e-9)
            {
                float norm = (float)Math.Sqrt(squaredSum);
                
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= norm;
                }
            }
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}