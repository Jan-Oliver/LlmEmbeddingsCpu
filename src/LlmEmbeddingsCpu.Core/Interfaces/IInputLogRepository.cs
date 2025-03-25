using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmEmbeddingsCpu.Core.Models;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IInputLogRepository
    {
        Task SaveLogAsync(InputLog log);
        Task<IEnumerable<InputLog>> GetPreviousLogsAsync(DateTime date);
        IEnumerable<DateTime> GetDatesToProcess();
        Task MarkFileAsDeleted(DateTime date);
    }
}