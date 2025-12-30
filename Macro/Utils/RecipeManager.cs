using Macro.Models;
using ReactiveUI;

namespace Macro.Utils
{
    public class RecipeManager : ReactiveObject
    {
        private static readonly RecipeManager _instance = new RecipeManager();
        private RecipeItem? _currentRecipe;

        public static RecipeManager Instance => _instance;

        public RecipeItem? CurrentRecipe
        {
            get => _currentRecipe;
            set => this.RaiseAndSetIfChanged(ref _currentRecipe, value);
        }

        private RecipeManager() { }
    }
}
