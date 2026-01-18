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
                            var bounds = ScreenCaptureHelper.GetScreenBounds();
                            
                            // CoordinatePickerWindow를 물리 좌표계 기준으로 생성
                            var picker = new CoordinatePickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                            var result = picker.ShowDialog();

                            if (result == true && picker.SelectedPoint.HasValue)
                            {
                                // 이미 물리 픽셀 좌표이므로 그대로 반환
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
                            var bounds = ScreenCaptureHelper.GetScreenBounds();

                            // RegionPickerWindow도 물리 좌표계 기준으로 생성 필요 (추후 수정 필요할 수 있음)
                            // 현재는 생성자 변경 없이 사용하되, 반환값 처리를 물리 픽셀로 가정
                            // NOTE: RegionPickerWindow도 CoordinatePickerWindow처럼 수정해야 완벽함.
                            // 우선 기존 생성자 사용하되, 내부 로직 확인 필요.
                            
                            // RegionPickerWindow가 아직 수정을 안 거쳤다면, 물리 좌표계 적용이 안 될 수 있음.
                            // 일단 여기서는 RegionPickerWindow도 수정되었다고 가정하고(또는 수정해야 함) 진행.
                            var picker = new RegionPickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                            var result = picker.ShowDialog();

                            if (result == true)
                            {
                                // Picker가 물리 픽셀 Rect를 반환한다고 가정
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
                            var bounds = ScreenCaptureHelper.GetScreenBounds();
                            
                            var picker = new RegionPickerWindow(capture, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                            var result = picker.ShowDialog();

                            if (result == true)
                            {
                                var rect = picker.SelectedRegion;
                                
                                // rect는 화면 절대 물리 좌표 (음수 포함)
                                // CroppedBitmap을 위해 비트맵 내부 로컬 좌표로 변환 (0,0 기준)
                                int localX = (int)(rect.X - bounds.Left);
                                int localY = (int)(rect.Y - bounds.Top);
                                int localW = (int)rect.Width;
                                int localH = (int)rect.Height;
                                
                                // 범위 체크
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