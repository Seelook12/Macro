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
    [JsonDerivedType(typeof(GrayChangeCondition), typeDiscriminator: nameof(GrayChangeCondition))]
    public interface IMacroCondition
    {
        Task<bool> CheckAsync();
        System.Windows.Point? FoundPoint { get; }
        string FailJumpName { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(MouseClickAction), typeDiscriminator: nameof(MouseClickAction))]
    [JsonDerivedType(typeof(KeyPressAction), typeDiscriminator: nameof(KeyPressAction))]
    public interface IMacroAction
    {
        Task ExecuteAsync(System.Windows.Point? conditionPoint = null);
        string FailJumpName { get; set; }
    }

    #endregion

    #region Condition Implementations

    public class DelayCondition : ReactiveObject, IMacroCondition
    {
        private int _delayTimeMs;
        private string _failJumpName = string.Empty;

        public System.Windows.Point? FoundPoint => null;

        public int DelayTimeMs
        {
            get => _delayTimeMs;
            set => this.RaiseAndSetIfChanged(ref _delayTimeMs, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
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
        private string _failJumpName = string.Empty;
        
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

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public bool UseRegion
        {
            get => _useRegion;
            set => this.RaiseAndSetIfChanged(ref _useRegion, value);
        }
// ... (Region properties 생략, 유지됨)

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

        private System.Windows.Point? _foundPoint;

        [JsonIgnore]
        public System.Windows.Point? FoundPoint
        {
            get => _foundPoint;
            private set => this.RaiseAndSetIfChanged(ref _foundPoint, value);
        }

        public async Task<bool> CheckAsync()
        {
            _foundPoint = null;
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

                    if (result.HasValue)
                    {
                        _foundPoint = result.Value;
                        return true;
                    }

                    // 3. 결과 반환
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }

    public class GrayChangeCondition : ReactiveObject, IMacroCondition
    {
        private int _x;
        private int _y;
        private int _width = 50;
        private int _height = 50;
        private double _threshold = 10.0;
        private int _delayMs = 100;
        private string _failJumpName = string.Empty;

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

        public int Width
        {
            get => _width;
            set => this.RaiseAndSetIfChanged(ref _width, value);
        }

        public int Height
        {
            get => _height;
            set => this.RaiseAndSetIfChanged(ref _height, value);
        }

        public double Threshold
        {
            get => _threshold;
            set => this.RaiseAndSetIfChanged(ref _threshold, value);
        }

        public int DelayMs
        {
            get => _delayMs;
            set => this.RaiseAndSetIfChanged(ref _delayMs, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        [JsonIgnore]
        public double? ReferenceValue { get; set; }

        public System.Windows.Point? FoundPoint => null;

        public async Task<bool> CheckAsync()
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs);
            }

            return await Task.Run(() =>
            {
                try
                {
                    var capture = System.Windows.Application.Current.Dispatcher.Invoke(() => ScreenCaptureHelper.GetScreenCapture());
                    if (capture == null) return false;

                    double currentValue = ImageSearchService.GetGrayAverage(capture, X, Y, Width, Height);


                    if (ReferenceValue == null)
                    {
                        return true;
                    }

                    double diff = Math.Abs(currentValue - ReferenceValue.Value);
                    return diff >= Threshold;
                }
                catch
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
        private bool _useConditionAddress;
        private string _failJumpName = string.Empty;

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

        public bool UseConditionAddress
        {
            get => _useConditionAddress;
            set => this.RaiseAndSetIfChanged(ref _useConditionAddress, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public async Task ExecuteAsync(System.Windows.Point? conditionPoint = null)
        {
            // 백그라운드 스레드에서 UI 차단 없이 실행
            await Task.Run(() =>
            {
                int finalX = X;
                int finalY = Y;

                if (UseConditionAddress && conditionPoint.HasValue)
                {
                    finalX = (int)conditionPoint.Value.X;
                    finalY = (int)conditionPoint.Value.Y;
                }

                InputHelper.MoveAndClick(finalX, finalY, ClickType);
            });
        }
    }

    public class KeyPressAction : ReactiveObject, IMacroAction
    {
        private string _keyCode = string.Empty;
        private string _failJumpName = string.Empty;

        public string KeyCode
        {
            get => _keyCode;
            set => this.RaiseAndSetIfChanged(ref _keyCode, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public async Task ExecuteAsync(System.Windows.Point? conditionPoint = null)
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

        private int _retryCount = 0;
        private int _retryDelayMs = 500;
        private int _repeatCount = 1;
        private string _successJumpName = string.Empty;

        public SequenceItem(IMacroAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        // JSON Deserialization을 위한 기본 생성자 (필요 시)
        [JsonConstructor]
        public SequenceItem(string name, bool isEnabled, IMacroCondition? preCondition, IMacroAction action, IMacroCondition? postCondition, int retryCount = 0, int retryDelayMs = 500, int repeatCount = 1, string successJumpName = "")
        {
             _name = name;
             _isEnabled = isEnabled;
             _preCondition = preCondition;
             _action = action ?? throw new ArgumentNullException(nameof(action));
             _postCondition = postCondition;
             _retryCount = retryCount;
             _retryDelayMs = retryDelayMs;
             _repeatCount = repeatCount;
             _successJumpName = successJumpName;
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

        public int RetryCount
        {
            get => _retryCount;
            set => this.RaiseAndSetIfChanged(ref _retryCount, value);
        }

        public int RetryDelayMs
        {
            get => _retryDelayMs;
            set => this.RaiseAndSetIfChanged(ref _retryDelayMs, value);
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set => this.RaiseAndSetIfChanged(ref _repeatCount, value);
        }

        public string SuccessJumpName
        {
            get => _successJumpName;
            set => this.RaiseAndSetIfChanged(ref _successJumpName, value);
        }
    }

    #endregion
}