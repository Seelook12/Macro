using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Macro.ViewModels;
using ReactiveUI;

namespace Macro.Views
{
    public partial class RecipeView : UserControl, IViewFor<RecipeViewModel>
    {
        #region ViewModel Property

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel), typeof(RecipeViewModel), typeof(RecipeView), new PropertyMetadata(null, OnViewModelChanged));

        public RecipeViewModel? ViewModel
        {
            get => (RecipeViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (RecipeViewModel?)value;
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RecipeView)d).DataContext = e.NewValue;
        }

        #endregion

        public RecipeView()
        {
            InitializeComponent();

            // Loaded 이벤트에서 Interaction 등록 (참조 프로젝트 방식)
            this.Loaded += RecipeView_Loaded;
        }

        private void RecipeView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            // 목록 로드
            ViewModel.LoadRecipes();

            // Interaction 핸들러 등록
            ViewModel.ShowInputName.RegisterHandler(interaction =>
            {
                var inputWindow = new InputWindow();
                var window = Window.GetWindow(this);
                if (window != null) inputWindow.Owner = window;

                if (inputWindow.ShowDialog() == true)
                {
                    interaction.SetOutput(inputWindow.InputText);
                }
                else
                {
                    interaction.SetOutput(null);
                }
            });
        }
    }
}
