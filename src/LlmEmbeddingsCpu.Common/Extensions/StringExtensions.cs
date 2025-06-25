using System.ComponentModel.Design;

namespace LlmEmbeddingsCpu.Common.Extensions
{
    /// <summary>
    /// Provides extension methods for the <see cref="string"/> class.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Encrypts a string using the ROT13 cipher.
        /// </summary>
        /// <param name="input">The string to encrypt.</param>
        /// <returns>The ROT13 encrypted string.</returns>
        public static string ToRot13(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            char[] result = new char[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Only process letters
                if (char.IsLetter(c))
                {
                    char offset = char.IsUpper(c) ? 'A' : 'a';
                    // Rot13 formula: (c - offset + 13) % 26 + offset
                    result[i] = (char)((c - offset + 13) % 26 + offset);
                }
                else
                {
                    result[i] = c;
                }
            }

            return new string(result);
        }

        /// <summary>
        /// Decrypts a string using the ROT13 cipher.
        /// </summary>
        /// <param name="input">The ROT13 encrypted string.</param>
        /// <returns>The decrypted string.</returns>
        public static string FromRot13(this string input)
        {
            // ROT13 is symmetric, so encoding and decoding are the same operation
            return ToRot13(input);
        }
    }
}