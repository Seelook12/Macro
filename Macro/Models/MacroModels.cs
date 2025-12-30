using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReactiveUI;
using Macro.Utils;
using Macro.Services;
using System.Windows.Input;

namespace Macro.Models
{
    #region Interfaces

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(DelayCondition), typeDiscriminator: nameof(DelayCondition))]
    [JsonDerivedType(typeof(ImageMatchCondition), typeDiscriminator: nameof(ImageMatchCondition))]
    public interface IMacroCondition
    {
        Task<bool> CheckAsync();
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(MouseClickAction), typeDiscriminator: nameof(MouseClickAction))]
    [JsonDerivedType(typeof(KeyPressAction), typeDiscriminator: nameof(KeyPressAction))]
    public interface IMacroAction
    {
        Task ExecuteAsync();
    }

    #endregion

    #region Condition Implementations

    public class DelayCondition : ReactiveObject, IMacroCondition
    {
        private int _delayTimeMs;

        public int DelayTimeMs
        {
            get => _delayTimeMs;
            set => this.RaiseAndSetIfChanged(ref _delayTimeMs, value);
        }

        public async Task<bool> CheckAsync()
        {
            await Task.Delay(DelayTimeMs);
            return true;
        }
    }

    public class ImageMatchCondition : ReactiveObject, IMacroCondition
    {
        private string _imagePath = string.Empty;
        private double _threshold = 0.9;
        
        // ROI Properties
        private bool _useRegion;
        private int _regionX;
        private int _regionY;
        private int _regionW;
        private int _regionH;

        public string ImagePath
        {
            get => _imagePath;
            set => this.RaiseAndSetIfChanged(ref _imagePath, value);
        }

        public double Threshold
        {
            get => _threshold;
            set => this.RaiseAndSetIfChanged(ref _threshold, value);
        }

        public bool UseRegion
        {
            get => _useRegion;
            set => this.RaiseAndSetIfChanged(ref _useRegion, value);
        }

        public int RegionX
        {
            get => _regionX;
            set => this.RaiseAndSetIfChanged(ref _regionX, value);
        }

        public int RegionY
        {
            get => _regionY;
            set => this.RaiseAndSetIfChanged(ref _regionY, value);
        }

        public int RegionW
        {
            get => _regionW;
            set => this.RaiseAndSetIfChanged(ref _regionW, value);
        }

        public int RegionH
        {
            get => _regionH;
            set => this.RaiseAndSetIfChanged(ref _regionH, value);
        }

        public async Task<bool> CheckAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 1. 현재 화면 캡처
                    var capture = System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        var bmp = ScreenCaptureHelper.GetScreenCapture();
                        bmp?.Freeze(); // 다른 스레드에서 사용 가능하게 얼림
                        return bmp;
                    });

                    if (capture == null) return false;

                    if (string.IsNullOrEmpty(ImagePath)) return false;

                    // ROI 영역 확인
                    System.Windows.Rect? roi = null;
                    if (UseRegion && RegionW > 0 && RegionH > 0)
                    {
                        roi = new System.Windows.Rect(RegionX, RegionY, RegionW, RegionH);
                    }

                    // 2. 이미지 서치
                    var result = ImageSearchService.FindImage(capture, ImagePath, Threshold, roi);

                    // 3. 결과 반환
                    return result.HasValue;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }

    #endregion

    #region Action Implementations

    public class MouseClickAction : ReactiveObject, IMacroAction
    {
        private int _x;
        private int _y;
        private string _clickType = "Left"; // Left, Right, Double

        public int X
        {
            get => _x;
            set => this.RaiseAndSetIfChanged(ref _x, value);
        }

        public int Y
        {
            get => _y;
            set => this.RaiseAndSetIfChanged(ref _y, value);
        }

        public string ClickType
        {
            get => _clickType;
            set => this.RaiseAndSetIfChanged(ref _clickType, value);
        }

        public async Task ExecuteAsync()
        {
            // 백그라운드 스레드에서 UI 차단 없이 실행
            await Task.Run(() =>
            {
                InputHelper.MoveAndClick(X, Y, ClickType);
            });
        }
    }

    public class KeyPressAction : ReactiveObject, IMacroAction
    {
        private string _keyCode = string.Empty;

        public string KeyCode
        {
            get => _keyCode;
            set => this.RaiseAndSetIfChanged(ref _keyCode, value);
        }

        public async Task ExecuteAsync()
        {
            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(KeyCode)) return;

                try
                {
                    // 문자열(예: "Enter", "A", "F1")을 WPF Key Enum으로 변환
                    if (Enum.TryParse(typeof(Key), KeyCode, true, out var result))
                    {
                        Key key = (Key)result;
                        // Key를 Windows Virtual Key Code로 변환
                        int vKey = KeyInterop.VirtualKeyFromKey(key);
                        
                        if (vKey > 0)
                        {
                            InputHelper.PressKey((byte)vKey);
                        }
                    }
                    else
                    {
                        // 파싱 실패 시 처리 (로그 등)
                    }
                }
                catch
                {
                    // 변환 실패 무시
                }
            });
        }
    }

    #endregion

    #region SequenceItem

    public class SequenceItem : ReactiveObject
    {
        private string _name = string.Empty;
        private bool _isEnabled = true;
        private IMacroCondition? _preCondition;
        private IMacroAction _action;
        private IMacroCondition? _postCondition;

        public SequenceItem(IMacroAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        // JSON Deserialization을 위한 기본 생성자 (필요 시)
        [JsonConstructor]
        public SequenceItem(string name, bool isEnabled, IMacroCondition? preCondition, IMacroAction action, IMacroCondition? postCondition)
        {
             _name = name;
             _isEnabled = isEnabled;
             _preCondition = preCondition;
             _action = action ?? throw new ArgumentNullException(nameof(action));
             _postCondition = postCondition;
        }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public IMacroCondition? PreCondition
        {
            get => _preCondition;
            set => this.RaiseAndSetIfChanged(ref _preCondition, value);
        }

        public IMacroAction Action
        {
            get => _action;
            set => this.RaiseAndSetIfChanged(ref _action, value);
        }

        public IMacroCondition? PostCondition
        {
            get => _postCondition;
            set => this.RaiseAndSetIfChanged(ref _postCondition, value);
        }
    }

    #endregion
}