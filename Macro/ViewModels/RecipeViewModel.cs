using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using Macro.Models;
using ReactiveUI;

using Macro.Utils;

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
        public ReactiveCommand<Unit, Unit> SelectCommand { get; }

        // View에서 입력을 받기 위한 Interaction
        public Interaction<Unit, string?> ShowInputName { get; } = new();
        
        // [New] 변경 확인 팝업을 위한 Interaction (Input: Message, Output: User Response(bool))
        public Interaction<string, bool> ConfirmChange { get; } = new();

        #endregion

        #region Constructor

        public RecipeViewModel(IScreen hostScreen = null)
        {
            HostScreen = hostScreen;

            // 커맨드 구성
            CreateCommand = ReactiveCommand.CreateFromTask(CreateRecipeAsync);
            
            var canExecute = this.WhenAnyValue(x => x.SelectedRecipe).Select(x => x != null);
            DeleteCommand = ReactiveCommand.Create(DeleteRecipe, canExecute);
            // [Modified] 비동기 명령어로 변경
            SelectCommand = ReactiveCommand.CreateFromTask(SelectRecipeAsync, canExecute);

            // 경로 설정
            _recipeDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Recipe");
            _recipeDirectory = Path.GetFullPath(_recipeDirectory);

            try
            {
                // 초기화 및 로드
                InitializeDirectory();
                LoadRecipes();

                // [Fix] 초기화 시 RecipeManager의 현재 레시피가 있다면 선택 상태 복원
                var current = RecipeManager.Instance.CurrentRecipe;
                if (current != null)
                {
                    SelectedRecipe = Recipes.FirstOrDefault(r => r.FilePath == current.FilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading recipes: {ex.Message}");
            }

            // [Fix] 초기화 완료 후 변경 사항 구독 (초기 null 할당 방지)
            this.WhenAnyValue(x => x.SelectedRecipe)
                .Skip(1) // 생성자 초기 세팅은 건너뜀
                .Subscribe(recipe => 
                {
                    // 단순 선택 변경 시에는 업데이트하지 않음 (더블 클릭으로 확정할 때만 업데이트 하거나, 
                    // 혹은 UI 상의 '선택됨' 표시를 위해 동기화는 하되 실제 로드/저장은 버튼 액션으로 분리 가능)
                    // 현재 정책: 리스트 선택만으로는 Global Manager를 바꾸지 않고, 'SelectCommand' 실행 시에만 바꾼다.
                    // 따라서 여기서는 아무것도 하지 않거나, 로컬 상태만 유지함.
                    
                    // *중요 변경*: 사용자의 요청("변경하시겠습니까 팝업")을 수용하려면 
                    // 리스트 클릭만으로 즉시 Manager를 업데이트하면 안 됩니다.
                    // 따라서 이 Subscribe 로직을 제거하거나 주석 처리해야 합니다.
                });
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

        private async System.Threading.Tasks.Task SelectRecipeAsync()
        {
            if (SelectedRecipe == null) return;

            var current = RecipeManager.Instance.CurrentRecipe;

            // 이미 같은 레시피가 선택되어 있다면 팝업 없이 이동
            if (current != null && current.FilePath == SelectedRecipe.FilePath)
            {
                NavigateToTeaching();
                return;
            }

            // 현재 선택된 레시피가 있다면 변경 확인
            if (current != null)
            {
                var confirm = await ConfirmChange.Handle($"현재 레시피 '{current.FileName}'에서\n'{SelectedRecipe.FileName}'(으)로 변경하시겠습니까?");
                if (!confirm) return;
            }

            // 레시피 변경 확정
            RecipeManager.Instance.CurrentRecipe = SelectedRecipe;
            
            // 화면 이동
            NavigateToTeaching();
        }

        private void NavigateToTeaching()
        {
            if (HostScreen is MainWindowViewModel mainVM)
            {
                HostScreen.Router.Navigate.Execute(mainVM.TeachingVM);
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