using Gma.System.MouseKeyHook;
using System.Text;
using System.Windows.Forms;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Common.Extensions;
using LlmEmbeddingsCpu.Data.KeyboardInputStorage;
namespace LlmEmbeddingsCpu.Services.KeyboardMonitor
{
    public class KeyboardMonitorService
    {
        private IKeyboardEvents? _globalHook;
        private readonly KeyboardInputStorageService _repository;
        private readonly StringBuilder _currentBufferedInputSequence = new();
        public event EventHandler<string>? TextCaptured;
        public KeyboardMonitorService(KeyboardInputStorageService repository)
        {
            _repository = repository;
        }
        public void StartTracking()
        {
            // Subscribe to global events
            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyPress += GlobalHook_KeyPress;
            Console.WriteLine("Keyboard tracking started...");
        }
        
        public void StopTracking()
        {
            // Save any pending word
            if (_currentBufferedInputSequence.Length > 0)
            {
                SaveWord(_currentBufferedInputSequence.ToString());
                _currentBufferedInputSequence.Clear();
            }
            
            if (_globalHook != null)
            {
                _globalHook.KeyPress -= GlobalHook_KeyPress;
            }
            Console.WriteLine("Keyboard tracking stopped.");
        }

        private void GlobalHook_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Always save on enter/return
            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                if (_currentBufferedInputSequence.Length > 0)
                {
                    string sentence = _currentBufferedInputSequence.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        SaveWord(sentence);
                        TextCaptured?.Invoke(this, sentence);
                    }
                    _currentBufferedInputSequence.Clear();
                }
                return;
            }

            // Check if the key is a sentence terminator
            bool isTerminator = e.KeyChar == '.' || e.KeyChar == '?' || e.KeyChar == '!';
            
            if (isTerminator)
            {
                _currentBufferedInputSequence.Append(e.KeyChar);
            }
            else if (e.KeyChar == ' ' && _currentBufferedInputSequence.Length > 0)
            {
                // If the last character was a terminator and now we see a space, save the sentence
                char lastChar = _currentBufferedInputSequence[^1];
                if (lastChar == '.' || lastChar == '?' || lastChar == '!')
                {
                    string sentence = _currentBufferedInputSequence.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        SaveWord(sentence);
                        TextCaptured?.Invoke(this, sentence);
                    }
                    _currentBufferedInputSequence.Clear();
                }
                _currentBufferedInputSequence.Append(e.KeyChar);
            }
            else
            {
                // Collect all printable characters for the sentence
                if (!char.IsControl(e.KeyChar))
                {
                    _currentBufferedInputSequence.Append(e.KeyChar);
                }
            }
        }
        
        private async void SaveWord(string word)
        {
            string encodedWord = word.ToRot13();
            
            // Create log entry
            var log = new KeyboardInputLog
            {
                Content = encodedWord,
                Timestamp = DateTime.Now
            };
            
            // Save asynchronously
            await _repository.SaveLogAsync(log);
            
            // Debug output
            Console.WriteLine($"Saved word: {word} (encoded: {encodedWord})");
        }
    }
}