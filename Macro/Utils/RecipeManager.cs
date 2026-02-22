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
        private static readonly JsonSerializerOptions _readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        private RecipeItem? _currentRecipe;
        private List<SequenceItem> _loadedSequences = new List<SequenceItem>();
        private readonly string _recipeDir;
        private readonly object _varFileLock = new object();
        private readonly object _recipeFileLock = new object();

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeManager] LoadLastRecipe failed: {ex.Message}");
            }
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

            // [New] Copy sidecar .vars.json if exists
            var sourceVarsPath = Path.ChangeExtension(sourceFilePath, ".vars.json");
            if (File.Exists(sourceVarsPath))
            {
                var destVarsPath = Path.ChangeExtension(destFilePath, ".vars.json");
                try
                {
                    File.Copy(sourceVarsPath, destVarsPath, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeManager] Vars copy failed: {ex.Message}");
                }
            }
        }

        private void LoadSequenceData(string filePath)
        {
            lock (_recipeFileLock)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            _loadedSequences = JsonSerializer.Deserialize<List<SequenceItem>>(json, _readOptions) ?? new List<SequenceItem>();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeManager] LoadSequenceData failed: {ex.Message}");
                    _loadedSequences = new List<SequenceItem>();
                }
            }
        }

        public void UpdateVariableValue(string name, string value)
        {
            if (CurrentRecipe == null || string.IsNullOrEmpty(CurrentRecipe.FilePath)) return;

            var varsPath = Path.ChangeExtension(CurrentRecipe.FilePath, ".vars.json");
            
            lock (_varFileLock)
            {
                try
                {
                    List<VariableDefinition> variables = new List<VariableDefinition>();
                    var options = _writeOptions;

                    // 1. Read existing
                    if (File.Exists(varsPath))
                    {
                        var json = File.ReadAllText(varsPath);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            variables = JsonSerializer.Deserialize<List<VariableDefinition>>(json, options) ?? new List<VariableDefinition>();
                        }
                    }

                    // 2. Update
                    var target = variables.FirstOrDefault(v => v.Name == name);
                    if (target != null)
                    {
                        target.DefaultValue = value;
                    }
                    else
                    {
                        // If not exists, add new (Optional, but safer)
                        variables.Add(new VariableDefinition { Name = name, DefaultValue = value, Description = "Auto-saved runtime variable" });
                    }

                    // 3. Save
                    var newJson = JsonSerializer.Serialize(variables, options);
                    File.WriteAllText(varsPath, newJson);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to persist variable '{name}': {ex.Message}");
                }
            }
        }
    }
}
