using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;

namespace LlmEmbeddingsCpu.Services.EmbeddingService
{
    public class IntfloatEmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly Tokenizers.DotNet.Tokenizer _tokenizer;
        private readonly int _embeddingSize;
        private readonly string _modelName;
        private readonly string _modelDirectory;

        public IntfloatEmbeddingService(string modelName = "multilingual-e5-small", int embeddingSize = 384)
        {
            _modelName = modelName;
            _embeddingSize = embeddingSize;

            // Look for models in the deps directory
            _modelDirectory = Path.Combine(GetRootDirectory(), "deps", "intfloat", modelName);
        
            string modelPath = Path.Combine(_modelDirectory, "model.onnx");
            string tokenizerPath = Path.Combine(_modelDirectory, "tokenizer.json");

            // Ensure model files exist
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found at {modelPath}");
            }

            if (!File.Exists(tokenizerPath))
            {
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
                // Preprocess text - normalize, remove extra whitespace
                text = NormalizeText(text);

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

        private static string NormalizeText(string text)
        {
            // Remove excess whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text;
        }

        private float[] GenerateEmbeddingVector(uint[] tokens)
        {
            // Remove padding zeros and limit sequence length
            var inputIds = tokens.TakeWhile(t => t != 0).ToArray();
            if (inputIds.Length == 0)
            {
                // If all tokens are 0, take the actual tokens
                inputIds = tokens.Where(t => t != 0).ToArray();
                if (inputIds.Length == 0)
                {
                    // If still empty, use the original tokens
                    inputIds = tokens;
                }
            }
            
            int seqLength = inputIds.Length;
            
            // Create input tensors
            var inputIdsTensor = new DenseTensor<long>(new[] { 1, seqLength });
            var attentionMaskTensor = new DenseTensor<long>(new[] { 1, seqLength });
            var tokenTypeIdsTensor = new DenseTensor<long>(new[] { 1, seqLength });
            
            // Fill the tensors
            for (int i = 0; i < seqLength; i++)
            {
                inputIdsTensor[0, i] = inputIds[i];
                attentionMaskTensor[0, i] = 1; // Set attention mask to 1 for non-padding tokens
                tokenTypeIdsTensor[0, i] = 0;  // Token type IDs (all zeros for sentence embeddings)
            }
            
            // Create input tensor dictionary
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };
            
            try
            {
                // Run inference
                using var outputs = _session.Run(inputs);
                
                // Get the model output tensor
                DisposableNamedOnnxValue outputValue = outputs.First();
                var tensor = outputValue.AsTensor<float>();
                
                var dims = tensor.Dimensions.ToArray();
                if (dims.Length == 3)
                {
                    // Apply mean pooling over sequence dimension
                    float[] embedding = MeanPooling(tensor, attentionMaskTensor, seqLength, _embeddingSize);
                    
                    // Apply L2 normalization
                    NormalizeL2(embedding);
                    
                    return embedding;
                }
                else
                {
                    Console.WriteLine($"Unexpected output shape: [{string.Join(", ", dims)}]");
                    return new float[_embeddingSize];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during inference: {ex.Message}");
                // Return a zero vector as fallback
                return new float[_embeddingSize];
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