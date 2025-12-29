using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Macro.Models;
using ReactiveUI;

namespace Macro.ViewModels
{
    public class RecipeViewModel : ReactiveObject, IRoutableViewModel
    {
        #region Fields & Properties

        private string _recipeDirectory;
        private RecipeItem? _selectedRecipe;

        // IRoutableViewModel 구현
        public string UrlPathSegment => "Recipe";
        public IScreen HostScreen { get; }

        public ObservableCollection<RecipeItem> Recipes { get; } = new();

        public RecipeItem? SelectedRecipe
        {
            get => _selectedRecipe;
            set => this.RaiseAndSetIfChanged(ref _selectedRecipe, value);
        }

        #endregion

        #region Commands & Interactions

        public ReactiveCommand<Unit, Unit> CreateCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

        // View에서 입력을 받기 위한 Interaction
        public Interaction<Unit, string?> ShowInputName { get; } = new();

        #endregion

        #region Constructor

        public RecipeViewModel(IScreen hostScreen = null)
        {
            HostScreen = hostScreen;

            // 커맨드 구성 (가장 먼저 초기화하여 바인딩 오류 방지)
            CreateCommand = ReactiveCommand.CreateFromTask(CreateRecipeAsync);
            
            // 삭제는 선택된 항목이 있을 때만 가능
            var canDelete = this.WhenAnyValue(x => x.SelectedRecipe).Select(x => x != null);
            DeleteCommand = ReactiveCommand.Create(DeleteRecipe, canDelete);

            // 경로 설정: 실행 파일 상위 폴더의 Recipe
            _recipeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Recipe");
            _recipeDirectory = Path.GetFullPath(_recipeDirectory); // 정규 경로로 변환

            try
            {
                // 초기화
                InitializeDirectory();
                LoadRecipes();
            }
            catch (Exception ex)
            {
                // 로드 실패 시 로그를 남기거나 처리 (현재는 무시하되 생성자는 완료됨)
                System.Diagnostics.Debug.WriteLine($"Error loading recipes: {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private void InitializeDirectory()
        {
            if (!Directory.Exists(_recipeDirectory))
            {
                Directory.CreateDirectory(_recipeDirectory);
            }
        }

        public void LoadRecipes()
        {
            Recipes.Clear();
            var files = Directory.GetFiles(_recipeDirectory, "*.json");

            foreach (var file in files)
            {
                Recipes.Add(new RecipeItem
                {
                    FileName = Path.GetFileNameWithoutExtension(file),
                    FilePath = file
                });
            }
        }

        private async System.Threading.Tasks.Task CreateRecipeAsync()
        {
            // View에게 이름 입력 요청
            var name = await ShowInputName.Handle(Unit.Default);

            if (string.IsNullOrWhiteSpace(name))
                return;

            var fileName = $"{name}.json";
            var filePath = Path.Combine(_recipeDirectory, fileName);

            // 중복 체크 (간단히 덮어쓰기 방지)
            if (File.Exists(filePath))
            {
                // 실제로는 사용자 알림이 필요할 수 있음
                return;
            }

            try
            {
                // 빈 JSON 파일 생성
                var emptyContent = "{}";
                await File.WriteAllTextAsync(filePath, emptyContent);

                // 리스트에 추가
                var newItem = new RecipeItem
                {
                    FileName = name,
                    FilePath = filePath
                };
                Recipes.Add(newItem);
                SelectedRecipe = newItem;
            }
            catch (Exception)
            {
                // 에러 처리 로직 (로그 등)
            }
        }

        private void DeleteRecipe()
        {
            if (SelectedRecipe == null) return;

            try
            {
                if (File.Exists(SelectedRecipe.FilePath))
                {
                    File.Delete(SelectedRecipe.FilePath);
                }

                Recipes.Remove(SelectedRecipe);
                SelectedRecipe = null;
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        #endregion
    }
}