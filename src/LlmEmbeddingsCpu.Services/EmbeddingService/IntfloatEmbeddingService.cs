
using System.IO;
using LlmEmbeddingsCpu.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using LlmEmbeddingsCpu.Core.Models;


namespace LlmEmbeddingsCpu.Services.EmbeddingService
{
    /// <summary>
    /// Provides a service for generating text embeddings using the intfloat/multilingual-e5-small ONNX model.
    /// </summary>
    public class IntfloatEmbeddingService : IEmbeddingService
    {
        private readonly Tokenizers.DotNet.Tokenizer _tokenizer;
        private readonly SessionOptions _sessionOptions;
        private readonly string _modelPath;

        private readonly int    _embeddingSize;
        private readonly int    _maxSequenceLength;
        private readonly string _modelName;
        private readonly ILogger<IntfloatEmbeddingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntfloatEmbeddingService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="modelName">The name of the ONNX model.</param>
        /// <param name="embeddingSize">The size of the embedding vector.</param>
        /// <param name="maxSequenceLength">The maximum sequence length for the model.</param>
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
            
            string modelDir    = Path.Combine(GetRootDirectory(), "deps", "intfloat", modelName);
            _modelPath         = Path.Combine(modelDir, "model.onnx");
            string tokPath     = Path.Combine(modelDir, "tokenizer.json");

            // Ensure model files exist
            if (!File.Exists(_modelPath))
            {
                _logger.LogError("Model file not found at {ModelPath}", _modelPath);
                throw new FileNotFoundException($"Model file not found at {_modelPath}");
            }

            if (!File.Exists(tokPath))
            {
                _logger.LogError("Tokenizer file not found at {TokenizerPath}", tokPath);
                throw new FileNotFoundException($"Tokenizer file not found at {tokPath}");
            }

            // Initialize tokenizer
            _tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokPath);

            // Setup ONNX session options
            _sessionOptions = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };
        }

        /// <summary>
        /// Gets the root directory of the application.
        /// </summary>
        /// <returns>The root directory path.</returns>
        private static string GetRootDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Asynchronously generates an embedding for a single keyboard input log.
        /// </summary>
        /// <param name="keyboardInputLog">The keyboard input log.</param>
        /// <returns>A <see cref="Task{Embedding}"/> representing the asynchronous operation, containing the generated embedding.</returns>
        public async Task<Core.Models.Embedding> GenerateEmbeddingAsync(KeyboardInputLog keyboardInputLog)
        {
            _logger.LogDebug("Generating embedding for a single keyboard input log");
            var list = await GenerateEmbeddingsAsync(new[] { keyboardInputLog });
            return list.First();
        }

        /// <summary>
        /// Asynchronously generates embeddings for a collection of keyboard input logs.
        /// </summary>
        /// <param name="keyboardInputLogs">The collection of keyboard input logs.</param>
        /// <returns>A <see cref="Task{IEnumerable{Embedding}}"/> representing the asynchronous operation, containing the generated embeddings.</returns>
        public async Task<IEnumerable<Core.Models.Embedding>> GenerateEmbeddingsAsync(IEnumerable<KeyboardInputLog> keyboardInputLogs)
        {
            return await Task.Run(() =>
            {
                _logger.LogDebug("Generating embeddings for {KeyboardInputLogCount} keyboard input logs", keyboardInputLogs.Count());
                using var session = new InferenceSession(_modelPath, _sessionOptions);

                var results = new List<Core.Models.Embedding>();
                foreach (var inputLog in keyboardInputLogs)
                {
                    string pre  = PreprocessText(inputLog.Content);
                    var tokens  = _tokenizer.Encode(pre);
                    var vec     = GenerateEmbeddingVector(session, tokens);

                    results.Add(new Core.Models.Embedding
                    {
                        Vector     = vec,
                        ModelName  = _modelName,
                        Timestamp  = inputLog.Timestamp,
                        KeyboardInputType = inputLog.Type
                    });
                }

                session.Dispose();
                return results;
            });
        }

        /// <summary>
        /// Preprocesses the input text by removing excess whitespace and adding a prefix.
        /// </summary>
        /// <param name="text">The text to preprocess.</param>
        /// <returns>The preprocessed text.</returns>
        private static string PreprocessText(string text)
        {
            // Remove excess whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            // Add prefix
            text = "query: " + text;
            
            return text;
        }

        /// <summary>
        /// Generates an embedding vector from a sequence of tokens using the ONNX model.
        /// </summary>
        /// <param name="session">The ONNX inference session.</param>
        /// <param name="tokens">The input tokens.</param>
        /// <returns>A float array representing the embedding vector.</returns>
        private float[] GenerateEmbeddingVector(InferenceSession session,uint[] tokens)
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
                outputs = session.Run(inputs);
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
        
        /// <summary>
        /// Performs mean pooling on the model output to get a fixed-size embedding.
        /// </summary>
        /// <param name="modelOutput">The output tensor from the model.</param>
        /// <param name="attentionMask">The attention mask tensor.</param>
        /// <param name="seqLength">The sequence length.</param>
        /// <param name="embeddingSize">The embedding size.</param>
        /// <returns>A float array representing the pooled embedding.</returns>
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
        
        /// <summary>
        /// Normalizes a vector using L2 normalization.
        /// </summary>
        /// <param name="vector">The vector to normalize.</param>
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
    }
}