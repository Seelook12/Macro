using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Macro.Models;
using Macro.Utils;
using Macro.ViewModels;
using ReactiveUI;
using UserControl = System.Windows.Controls.UserControl;

namespace Macro.Views
{
    public partial class TeachingView : UserControl, IViewFor<TeachingViewModel>
    {
        #region Dependency Properties

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(TeachingViewModel), typeof(TeachingView), new PropertyMetadata(null));

        #endregion

        public TeachingView()
        {
            InitializeComponent();
            
            this.WhenActivated(disposables =>
            {
                // ViewModel 바인딩
                this.WhenAnyValue(x => x.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => DataContext = vm)
                    .DisposeWith(disposables);

                // Interaction 핸들러 등록: ViewModel이 설정될 때까지 대기
                var interactionSub = new CompositeDisposable().DisposeWith(disposables);

                this.WhenAnyValue(x => x.ViewModel)
                    .WhereNotNull()
                    .Take(1)
                    .Subscribe(vm =>
                    {
                        vm.GetCoordinateInteraction.RegisterHandler(async ctx =>
                        {
                            var mainWindow = System.Windows.Application.Current.MainWindow;
                            if (mainWindow == null) return;
                            try
                            {
                                mainWindow.WindowState = WindowState.Minimized;
                                await Task.Delay(300);

                                var capture = ScreenCaptureHelper.GetScreenCapture();
                                var bounds = ScreenCaptureHelper.GetScreenBounds();

                                if (capture == null) return;

                                var picker = new CoordinatePickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                                var result = picker.ShowDialog();

                                if (result == true && picker.SelectedPoint.HasValue)
                                {
                                    ctx.SetOutput(picker.SelectedPoint.Value);
                                }
                                else
                                {
                                    ctx.SetOutput(null);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Coordinate Pick Failed: {ex}");
                                ctx.SetOutput(null);
                            }
                            finally
                            {
                                mainWindow.WindowState = WindowState.Normal;
                                mainWindow.Activate();
                            }
                        }).DisposeWith(interactionSub);

                        vm.GetRegionInteraction.RegisterHandler(async ctx =>
                        {
                            var mainWindow = System.Windows.Application.Current.MainWindow;
                            if (mainWindow == null) return;
                            try
                            {
                                mainWindow.WindowState = WindowState.Minimized;
                                await Task.Delay(300);

                                var capture = ScreenCaptureHelper.GetScreenCapture();
                                var bounds = ScreenCaptureHelper.GetScreenBounds();

                                if (capture == null) return;

                                var picker = new RegionPickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                                var result = picker.ShowDialog();

                                if (result == true)
                                {
                                    ctx.SetOutput(picker.SelectedRegion);
                                }
                                else
                                {
                                    ctx.SetOutput(null);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Region Pick Failed: {ex}");
                                ctx.SetOutput(null);
                            }
                            finally
                            {
                                mainWindow.WindowState = WindowState.Normal;
                                mainWindow.Activate();
                            }
                        }).DisposeWith(interactionSub);

                        vm.CaptureImageInteraction.RegisterHandler(async ctx =>
                        {
                            var mainWindow = System.Windows.Application.Current.MainWindow;
                            if (mainWindow == null) return;
                            try
                            {
                                mainWindow.WindowState = WindowState.Minimized;
                                await Task.Delay(300);

                                var capture = ScreenCaptureHelper.GetScreenCapture();
                                var bounds = ScreenCaptureHelper.GetScreenBounds();

                                if (capture == null) return;

                                var picker = new RegionPickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                                var result = picker.ShowDialog();

                                if (result == true)
                                {
                                    var rect = picker.SelectedRegion;

                                    int localX = (int)(rect.X - bounds.Left);
                                    int localY = (int)(rect.Y - bounds.Top);
                                    int localW = (int)rect.Width;
                                    int localH = (int)rect.Height;

                                    if (localX < 0) localX = 0;
                                    if (localY < 0) localY = 0;
                                    if (localX + localW > capture.PixelWidth) localW = capture.PixelWidth - localX;
                                    if (localY + localH > capture.PixelHeight) localH = capture.PixelHeight - localY;

                                    if (localW > 0 && localH > 0)
                                    {
                                        var cropped = new CroppedBitmap(capture,
                                            new Int32Rect(localX, localY, localW, localH));

                                        var tempPath = Path.GetTempFileName().Replace(".tmp", ".png");
                                        using (var fileStream = new FileStream(tempPath, FileMode.Create))
                                        {
                                            var encoder = new PngBitmapEncoder();
                                            encoder.Frames.Add(BitmapFrame.Create(cropped));
                                            encoder.Save(fileStream);
                                        }
                                        ctx.SetOutput(tempPath);
                                    }
                                    else
                                    {
                                        ctx.SetOutput(null);
                                    }
                                }
                                else
                                {
                                    ctx.SetOutput(null);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Capture Image Failed: {ex}");
                                ctx.SetOutput(null);
                            }
                            finally
                            {
                                mainWindow.WindowState = WindowState.Normal;
                                mainWindow.Activate();
                            }
                        }).DisposeWith(interactionSub);
                    })
                    .DisposeWith(disposables);
            });
        }

        #region IViewFor Implementation

        public TeachingViewModel? ViewModel
        {
            get => GetValue(ViewModelProperty) as TeachingViewModel;
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = value as TeachingViewModel;
        }

        #endregion

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ViewModel == null) return;

            if (e.NewValue is SequenceGroup group)
            {
                ViewModel.SelectedGroup = group;
                ViewModel.SelectedSequence = null;
            }
            else if (e.NewValue is SequenceItem item)
            {
                ViewModel.SelectedSequence = item;
                // Find parent group using recursive helper
                ViewModel.SelectedGroup = ViewModel.FindParentGroup(item);
            }
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Check if click was on a TreeViewItem
            var source = e.OriginalSource as DependencyObject;
            var treeViewItem = FindVisualParent<TreeViewItem>(source);

            if (treeViewItem == null)
            {
                // Clicked on empty space within TreeView
                if (ViewModel != null)
                {
                    ViewModel.SelectedGroup = null;
                    ViewModel.SelectedSequence = null;
                    
                    // Clear TreeView selection visual (Optional, as ViewModel update might not clear it automatically if Mode=OneWay)
                    // But standard TreeView doesn't support 'Unselect' easily via binding without behavior.
                    // This logic mainly resets VM state so 'Add Group' works at Root level.
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
                        }
                        return null;
                    }
                }
            }
            