using System;

namespace LlmEmbeddingsCpu.Core.Interfaces
{
    public interface IInputTrackingService
    {
        void StartTracking();
        void StopTracking();
        event EventHandler<string> TextCaptured;
    }
}