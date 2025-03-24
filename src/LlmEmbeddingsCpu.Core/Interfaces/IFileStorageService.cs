using System.Threading.Tasks;
using System.Collections.Generic;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IFileStorageService
    {
        Task WriteTextAsync(string filename, string content, bool append = false);
        Task<string> ReadTextAsync(string filename);
        bool FileExists(string filename);
        IEnumerable<string> ListFiles(string pattern);
        void RenameFile(string oldName, string newName);
        Task EnsureDirectoryExistsAsync(string path);
    }
}