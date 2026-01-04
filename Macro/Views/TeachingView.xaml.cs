using System;
using System.IO;
using System.Reactive.Disposables;
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
                var d1 = this.WhenAnyValue(x => x.ViewModel)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(vm => DataContext = vm);
                disposables.Add(d1);

                // Interaction 핸들러 등록
                if (ViewModel != null)
                {
                    var d2 = ViewModel.GetCoordinateInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow == null) return;
                        try
                        {
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);

                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            var picker = new CoordinatePickerWindow(capture);
                            var result = picker.ShowDialog();

                            if (result == true && picker.SelectedPoint.HasValue)
                            {
                                var point = picker.SelectedPoint.Value;
                                var source = System.Windows.PresentationSource.FromVisual(mainWindow);
                                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                                double pixelX = (SystemParameters.VirtualScreenLeft + point.X) * scaleX;
                                double pixelY = (SystemParameters.VirtualScreenTop + point.Y) * scaleY;

                                ctx.SetOutput(new System.Windows.Point(pixelX, pixelY));
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
                    });
                    disposables.Add(d2);

                    var d3 = ViewModel.GetRegionInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow == null) return;
                        try
                        {
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);

                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            var picker = new RegionPickerWindow(capture);
                            var result = picker.ShowDialog();

                            if (result == true)
                            {
                                var rect = picker.SelectedRegion;
                                var source = System.Windows.PresentationSource.FromVisual(mainWindow);
                                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                                double pixelX = rect.X * scaleX;
                                double pixelY = rect.Y * scaleY;
                                double pixelW = rect.Width * scaleX;
                                double pixelH = rect.Height * scaleY;

                                ctx.SetOutput(new Rect(pixelX, pixelY, pixelW, pixelH));
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
                    });
                    disposables.Add(d3);

                    var d4 = ViewModel.CaptureImageInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow == null) return;
                        try
                        {
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);
                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            
                            var picker = new RegionPickerWindow(capture);
                            var result = picker.ShowDialog();

                            if (result == true)
                            {
                                var rect = picker.SelectedRegion;
                                var source = System.Windows.PresentationSource.FromVisual(mainWindow);
                                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                                int pixelX = (int)(rect.X * scaleX);
                                int pixelY = (int)(rect.Y * scaleY);
                                int pixelW = (int)(rect.Width * scaleX);
                                int pixelH = (int)(rect.Height * scaleY);

                                var cropped = new CroppedBitmap(capture, 
                                    new Int32Rect(pixelX, pixelY, pixelW, pixelH));

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
                    });
                    disposables.Add(d4);
                }
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
                // Find parent group
                foreach (var g in ViewModel.Groups)
                {
                    if (g.Items.Contains(item))
                    {
                        ViewModel.SelectedGroup = g;
                        break;
                    }
                }
            }
        }
    }
}