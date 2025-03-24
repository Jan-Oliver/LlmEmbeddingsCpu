using System;
using System.Threading.Tasks;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IScheduledProcessingService
    {
        Task ScheduleProcessingAsync(TimeSpan timeOfDay);
        Task ProcessNowAsync();
        Task StopScheduledProcessingAsync();
    }
}