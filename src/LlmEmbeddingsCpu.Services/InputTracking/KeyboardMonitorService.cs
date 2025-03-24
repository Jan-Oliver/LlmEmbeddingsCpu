// src/LlmEmbeddingsCpu.Services/InputTracking/KeyboardMonitorService.cs
using Gma.System.MouseKeyHook;
using System;
using System.Text;
using System.Windows.Forms;
using LlmEmbeddingsCpu.Core.Interfaces;
using LlmEmbeddingsCpu.Core.Models;
using LlmEmbeddingsCpu.Core.Enums;
using LlmEmbeddingsCpu.Common.Extensions;
using System.ComponentModel.Design;

namespace LlmEmbeddingsCpu.Services.InputTracking
{
    public class KeyboardMonitorService : IInputTrackingService
    {
        private IKeyboardEvents? _globalHook;
        private readonly IInputLogRepository _repository;
        private StringBuilder _currentWord = new StringBuilder();
        private char? _lastTerminator = null;

        public event EventHandler<string>? TextCaptured;

        public KeyboardMonitorService(IInputLogRepository repository)
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
            if (_currentWord.Length > 0)
            {
                SaveWord(_currentWord.ToString());
                _currentWord.Clear();
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
                if (_currentWord.Length > 0)
                {
                    string sentence = _currentWord.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        SaveWord(sentence);
                        TextCaptured?.Invoke(this, sentence);
                    }
                    _currentWord.Clear();
                }
                return;
            }

            // Check if the key is a sentence terminator
            bool isTerminator = e.KeyChar == '.' || e.KeyChar == '?' || e.KeyChar == '!';
            
            if (isTerminator)
            {
                _currentWord.Append(e.KeyChar);
            }
            else if (e.KeyChar == ' ' && _currentWord.Length > 0)
            {
                // If the last character was a terminator and now we see a space, save the sentence
                char lastChar = _currentWord[_currentWord.Length - 1];
                if (lastChar == '.' || lastChar == '?' || lastChar == '!')
                {
                    string sentence = _currentWord.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        SaveWord(sentence);
                        TextCaptured?.Invoke(this, sentence);
                    }
                    _currentWord.Clear();
                }
                _currentWord.Append(e.KeyChar);
            }
            else
            {
                // Collect all printable characters for the sentence
                if (!char.IsControl(e.KeyChar))
                {
                    _currentWord.Append(e.KeyChar);
                }
            }
        }
        
        private void SaveWord(string word)
        {
            // Apply ROT13 encoding to the word
            string encodedWord = word.ToRot13();
            
            // Create log entry
            var log = new InputLog
            {
                Content = encodedWord,
                Type = InputType.Keyboard,
                Timestamp = DateTime.Now
            };
            
            // Save asynchronously
            _ = _repository.SaveLogAsync(log);
            
            // Debug output
            Console.WriteLine($"Saved word: {word} (encoded: {encodedWord})");
        }
    }
}