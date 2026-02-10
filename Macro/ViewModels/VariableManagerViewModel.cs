using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Macro.Models;
using Macro.Services;
using Macro.Utils;
using ReactiveUI;

namespace Macro.ViewModels
{
    public class VariableManagerViewModel : ReactiveObject, IRoutableViewModel, IActivatableViewModel
    {
        public string UrlPathSegment => "VariableManager";
        public IScreen HostScreen { get; }
        public ViewModelActivator Activator { get; } = new ViewModelActivator();

        private ObservableCollection<VariableDefinition> _definedVariables = new ObservableCollection<VariableDefinition>();
        public ObservableCollection<VariableDefinition> DefinedVariables
        {
            get => _definedVariables;
            set => this.RaiseAndSetIfChanged(ref _definedVariables, value);
        }

        public ReactiveCommand<Unit, Unit> AddVariableCommand { get; }
        public ReactiveCommand<VariableDefinition, Unit> RemoveVariableCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }

        public VariableManagerViewModel(IScreen screen, ObservableCollection<VariableDefinition> sharedVariables)
        {
            HostScreen = screen;
            DefinedVariables = sharedVariables;

            AddVariableCommand = ReactiveCommand.Create(AddVariable);
            RemoveVariableCommand = ReactiveCommand.Create<VariableDefinition>(RemoveVariable);
            SaveCommand = ReactiveCommand.Create(SaveVariables);

            this.WhenActivated(disposables =>
            {
                // 데이터는 TeachingViewModel과 공유되므로 별도 로드 불필요
                disposables.Add(Disposable.Create(() => { }));
            });
        }

        private void AddVariable()
        {
            DefinedVariables.Add(new VariableDefinition 
            { 
                Name = $"NewVar_{DefinedVariables.Count + 1}", 
                DefaultValue = "0", 
                Description = "New Variable" 
            });
        }

        private void RemoveVariable(VariableDefinition variable)
        {
            if (variable != null && DefinedVariables.Contains(variable))
            {
                DefinedVariables.Remove(variable);
            }
        }

        private void LoadVariables()
        {
            DefinedVariables.Clear();
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath)) return;

            var varsPath = Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
            if (File.Exists(varsPath))
            {
                try
                {
                    var json = File.ReadAllText(varsPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    var vars = JsonSerializer.Deserialize<List<VariableDefinition>>(json, options);
                    if (vars != null)
                    {
                        foreach (var v in vars) DefinedVariables.Add(v);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VariableManager] Failed to load variables: {ex.Message}");
                }
            }
        }

        private void SaveVariables()
        {
            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
            if (currentRecipe == null || string.IsNullOrEmpty(currentRecipe.FilePath)) return;

            var varsPath = Path.ChangeExtension(currentRecipe.FilePath, ".vars.json");
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(DefinedVariables, options);
                File.WriteAllText(varsPath, json);
                
                // Show simple feedback (optional, or use a notification service)
                // System.Windows.MessageBox.Show("Variables Saved."); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VariableManager] Failed to save variables: {ex.Message}");
            }
        }
    }
}