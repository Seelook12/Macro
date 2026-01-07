using Macro.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Macro.Services;

namespace Macro.Utils
{
    public class RecipeManager : ReactiveObject
    {
        private static readonly RecipeManager _instance = new RecipeManager();
        private RecipeItem? _currentRecipe;
        private List<SequenceItem> _loadedSequences = new List<SequenceItem>();
        private readonly string _recipeDir;

        public static RecipeManager Instance => _instance;

        public string RecipeDir => _recipeDir;

        public RecipeItem? CurrentRecipe
        {
            get => _currentRecipe;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentRecipe, value);
                if (value != null)
                {
                    // 레시피 변경 시 이미지 캐시 비우기 (새 레시피 이미지 로드 준비)
                    ImageSearchService.ClearCache();

                    // 레시피가 변경될 때마다 최근 사용 기록 저장 (자동실행 옵션은 기존꺼 유지)
                    var settings = SettingsManager.LoadSettings();
                    settings.LastRecipeName = value.FileName;
                    SettingsManager.SaveSettings(settings);
                    
                    // 데이터 로드
                    LoadSequenceData(value.FilePath);
                }
            }
        }

        public List<SequenceItem> LoadedSequences => _loadedSequences;

        private RecipeManager() 
        {
            _recipeDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Recipe"));
            InitializeLastRecipe();
        }

        private void InitializeLastRecipe()
        {
            try
            {
                var settings = SettingsManager.LoadSettings();
                if (string.IsNullOrEmpty(settings.LastRecipeName)) return;

                if (!Directory.Exists(_recipeDir)) return;

                var lastFile = Path.Combine(_recipeDir, settings.LastRecipeName);
                if (File.Exists(lastFile))
                {
                    // 속성을 통해 설정하여 UI 알림(NotifyPropertyChanged) 발생 유도
                    CurrentRecipe = new RecipeItem
                    {
                        FileName = settings.LastRecipeName,
                        FilePath = lastFile
                    };
                }
            }
            catch { }
        }

        public void DuplicateRecipe(string sourceFilePath, string newName)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source recipe file not found.", sourceFilePath);

            var destFileName = $"{newName}.json";
            var destFilePath = Path.Combine(_recipeDir, destFileName);

            if (File.Exists(destFilePath))
                throw new IOException($"A recipe with the name '{newName}' already exists.");

            File.Copy(sourceFilePath, destFilePath);
        }

        private void LoadSequenceData(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        _loadedSequences = JsonSerializer.Deserialize<List<SequenceItem>>(json, options) ?? new List<SequenceItem>();
                    }
                }
            }
            catch 
            {
                _loadedSequences = new List<SequenceItem>();
            }
        }
    }
}
