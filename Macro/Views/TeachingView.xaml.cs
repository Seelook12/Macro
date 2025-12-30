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
                this.WhenAnyValue(x => x.ViewModel).BindTo(this, x => x.DataContext).DisposeWith(disposables);

                // [좌표 픽업 핸들러]
                if (ViewModel != null)
                {
                    ViewModel.GetCoordinateInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        var originalState = mainWindow.WindowState;

                        try
                        {
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);

                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            var picker = new CoordinatePickerWindow(capture);
                            var result = picker.ShowDialog();

                            ctx.SetOutput(result == true ? picker.SelectedPoint : null);
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

                    }).DisposeWith(disposables);

                    // [영역 픽업 핸들러]
                    ViewModel.GetRegionInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        
                        try
                        {
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);

                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            var picker = new RegionPickerWindow(capture);
                            var result = picker.ShowDialog();

                            ctx.SetOutput(result == true ? picker.SelectedRegion : null);
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

                    }).DisposeWith(disposables);

                    // [이미지 캡처 핸들러] (New)
                    ViewModel.CaptureImageInteraction.RegisterHandler(async ctx =>
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;

                        try
                        {
                            // 1. 최소화 및 캡처
                            mainWindow.WindowState = WindowState.Minimized;
                            await Task.Delay(300);
                            var capture = ScreenCaptureHelper.GetScreenCapture();
                            
                            // 2. 영역 선택 (재사용)
                            var picker = new RegionPickerWindow(capture);
                            var result = picker.ShowDialog();

                            if (result == true)
                            {
                                var rect = picker.SelectedRegion;
                                
                                // 3. 이미지 자르기 (Crop)
                                var cropped = new CroppedBitmap(capture, 
                                    new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height));

                                // 4. 임시 파일로 저장
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

                    }).DisposeWith(disposables);
                }
            });
        }

        #region IViewFor Implementation

        public TeachingViewModel? ViewModel
        {
            get => (TeachingViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (TeachingViewModel?)value;
        }

        #endregion
    }
}
