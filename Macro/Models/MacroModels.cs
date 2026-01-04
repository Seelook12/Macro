using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReactiveUI;
using Macro.Utils;
using Macro.Services;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Threading;

namespace Macro.Models
{
    #region Interfaces

    public enum CoordinateMode
    {
        Global,
        WindowRelative
    }

    public interface ISupportCoordinateTransform
    {
        void SetTransform(double scaleX, double scaleY, int offsetX, int offsetY);
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(DelayCondition), typeDiscriminator: nameof(DelayCondition))]
    [JsonDerivedType(typeof(ImageMatchCondition), typeDiscriminator: nameof(ImageMatchCondition))]
    [JsonDerivedType(typeof(GrayChangeCondition), typeDiscriminator: nameof(GrayChangeCondition))]
    [JsonDerivedType(typeof(VariableCompareCondition), typeDiscriminator: nameof(VariableCompareCondition))]
    public interface IMacroCondition : IReactiveObject
    {
        Task<bool> CheckAsync(CancellationToken token = default);
        System.Windows.Point? FoundPoint { get; }
        string FailJumpName { get; set; }
        string FailJumpId { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(MouseClickAction), typeDiscriminator: nameof(MouseClickAction))]
    [JsonDerivedType(typeof(KeyPressAction), typeDiscriminator: nameof(KeyPressAction))]
    [JsonDerivedType(typeof(VariableSetAction), typeDiscriminator: nameof(VariableSetAction))]
    [JsonDerivedType(typeof(IdleAction), typeDiscriminator: nameof(IdleAction))]
    [JsonDerivedType(typeof(WindowControlAction), typeDiscriminator: nameof(WindowControlAction))]
    public interface IMacroAction : IReactiveObject
    {
        Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null);
        string FailJumpName { get; set; }
        string FailJumpId { get; set; }
    }

    public enum WindowControlState
    {
        Maximize,
        Minimize,
        Restore
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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public async Task<bool> CheckAsync(CancellationToken token = default)
        {
            await Task.Delay(DelayTimeMs, token);
            return true;
        }
    }

    public class ImageMatchCondition : ReactiveObject, IMacroCondition, ISupportCoordinateTransform
    {
        private string _imagePath = string.Empty;
        private double _threshold = 0.9;
        private string _failJumpName = string.Empty;
        private string _resultVariableName = string.Empty;
        
        // Runtime Transform Properties
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        private int _offsetX = 0;
        private int _offsetY = 0;

        public void SetTransform(double scaleX, double scaleY, int offsetX, int offsetY)
        {
            _scaleX = scaleX;
            _scaleY = scaleY;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public string ResultVariableName
        {
            get => _resultVariableName;
            set => this.RaiseAndSetIfChanged(ref _resultVariableName, value);
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
        private double _testScore;
        private string _testResult = "Not Tested";

        [JsonIgnore]
        public System.Windows.Point? FoundPoint
        {
            get => _foundPoint;
            private set => this.RaiseAndSetIfChanged(ref _foundPoint, value);
        }

        [JsonIgnore]
        public double TestScore
        {
            get => _testScore;
            set => this.RaiseAndSetIfChanged(ref _testScore, value);
        }

        [JsonIgnore]
        public string TestResult
        {
            get => _testResult;
            set => this.RaiseAndSetIfChanged(ref _testResult, value);
        }

        public async Task<bool> CheckAsync(CancellationToken token = default)
        {
            _foundPoint = null;
            return await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return false;

                    // 1. 현재 화면 캡처
                    var capture = System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
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
                        double rx = RegionX * _scaleX + _offsetX;
                        double ry = RegionY * _scaleY + _offsetY;
                        double rw = RegionW * _scaleX;
                        double rh = RegionH * _scaleY;
                        roi = new System.Windows.Rect(rx, ry, rw, rh);
                    }
                    
                    if (token.IsCancellationRequested) return false;

                    // 2. 이미지 서치
                    var result = ImageSearchService.FindImage(capture, ImagePath, Threshold, roi);

                    if (!string.IsNullOrEmpty(ResultVariableName))
                    {
                        MacroEngineService.Instance.Variables[ResultVariableName] = result.HasValue ? "True" : "False";
                    }

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
            }, token);
        }
    }

    public class GrayChangeCondition : ReactiveObject, IMacroCondition, ISupportCoordinateTransform
    {
        private int _x;
        private int _y;
        private int _width = 50;
        private int _height = 50;
        private double _threshold = 10.0;
        private int _delayMs = 100;
        private string _failJumpName = string.Empty;

        // Runtime Transform Properties
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        private int _offsetX = 0;
        private int _offsetY = 0;

        public void SetTransform(double scaleX, double scaleY, int offsetX, int offsetY)
        {
            _scaleX = scaleX;
            _scaleY = scaleY;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        [JsonIgnore]
        public double? ReferenceValue { get; set; }

        public System.Windows.Point? FoundPoint => null;

        public async Task<bool> CheckAsync(CancellationToken token = default)
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, token);
            }

            return await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return false;

                    var capture = System.Windows.Application.Current?.Dispatcher?.Invoke(() => ScreenCaptureHelper.GetScreenCapture());
                    if (capture == null) return false;

                    int tx = (int)(X * _scaleX + _offsetX);
                    int ty = (int)(Y * _scaleY + _offsetY);
                    int tw = (int)(Width * _scaleX);
                    int th = (int)(Height * _scaleY);

                    double currentValue = ImageSearchService.GetGrayAverage(capture, tx, ty, tw, th);


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
            }, token);
        }
    }

    public class VariableCompareCondition : ReactiveObject, IMacroCondition
    {
        private string _variableName = string.Empty;
        private string _operator = "=="; // ==, !=, >, <, >=, <=, Contains
        private string _targetValue = string.Empty;
        private string _failJumpName = string.Empty;

        public string VariableName
        {
            get => _variableName;
            set => this.RaiseAndSetIfChanged(ref _variableName, value);
        }

        public string Operator
        {
            get => _operator;
            set => this.RaiseAndSetIfChanged(ref _operator, value);
        }

        public string TargetValue
        {
            get => _targetValue;
            set => this.RaiseAndSetIfChanged(ref _targetValue, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public System.Windows.Point? FoundPoint => null;

        public async Task<bool> CheckAsync(CancellationToken token = default)
        {
            return await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return false;

                var vars = MacroEngineService.Instance.Variables;
                string currentValue = vars.ContainsKey(VariableName) ? vars[VariableName] : string.Empty;

                switch (Operator)
                {
                    case "==": return currentValue == TargetValue;
                    case "!=": return currentValue != TargetValue;
                    case "Contains": return currentValue.Contains(TargetValue);
                    case ">":
                    case "<":
                    case ">=":
                    case "<=":
                        if (double.TryParse(currentValue, out double cur) && double.TryParse(TargetValue, out double tar))
                        {
                            if (Operator == ">") return cur > tar;
                            if (Operator == "<") return cur < tar;
                            if (Operator == ">=") return cur >= tar;
                            if (Operator == "<=") return cur <= tar;
                        }
                        return false;
                    default: return false;
                }
            }, token);
        }
    }

    #endregion

    #region Action Implementations

    public class MouseClickAction : ReactiveObject, IMacroAction, ISupportCoordinateTransform
    {
        private int _x;
        private int _y;
        private string _clickType = "Left"; // Left, Right, Double
        private bool _useConditionAddress;
        private string _failJumpName = string.Empty;

        // Runtime Transform Properties
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        private int _offsetX = 0;
        private int _offsetY = 0;

        public void SetTransform(double scaleX, double scaleY, int offsetX, int offsetY)
        {
            _scaleX = scaleX;
            _scaleY = scaleY;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public async Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null)
        {
            // 백그라운드 스레드에서 UI 차단 없이 실행
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                int finalX = 0;
                int finalY = 0;

                if (UseConditionAddress && conditionPoint.HasValue)
                {
                    finalX = (int)conditionPoint.Value.X;
                    finalY = (int)conditionPoint.Value.Y;
                }
                else
                {
                    finalX = (int)(X * _scaleX + _offsetX);
                    finalY = (int)(Y * _scaleY + _offsetY);
                }
                
                if (!token.IsCancellationRequested)
                    InputHelper.MoveAndClick(finalX, finalY, ClickType);
            }, token);
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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public async Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                
                if (string.IsNullOrEmpty(KeyCode)) return;

                try
                {
                    // 문자열(예: "Enter", "A", "F1")을 WPF Key Enum으로 변환
                    if (Enum.TryParse(typeof(Key), KeyCode, true, out var result))
                    {
                        Key key = (Key)result;
                        // Key를 Windows Virtual Key Code로 변환
                        int vKey = KeyInterop.VirtualKeyFromKey(key);
                        
                        if (vKey > 0 && !token.IsCancellationRequested)
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
            }, token);
        }
    }

    public class VariableSetAction : ReactiveObject, IMacroAction
    {
        private string _variableName = string.Empty;
        private string _value = string.Empty;
        private string _operation = "Set"; // Set, Add, Sub
        private string _failJumpName = string.Empty;

        public string VariableName
        {
            get => _variableName;
            set => this.RaiseAndSetIfChanged(ref _variableName, value);
        }

        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public string Operation
        {
            get => _operation;
            set => this.RaiseAndSetIfChanged(ref _operation, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public async Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                var vars = MacroEngineService.Instance.Variables;
                if (string.IsNullOrEmpty(VariableName)) return;

                if (Operation == "Set")
                {
                    vars[VariableName] = Value;
                }
                else if (Operation == "Add" || Operation == "Sub")
                {
                    double current = 0;
                    if (vars.ContainsKey(VariableName)) double.TryParse(vars[VariableName], out current);
                    double val = 0;
                    double.TryParse(Value, out val);

                    if (Operation == "Add") vars[VariableName] = (current + val).ToString();
                    else vars[VariableName] = (current - val).ToString();
                }
            }, token);
        }
    }

    public class IdleAction : ReactiveObject, IMacroAction
    {
        private int _delayTimeMs;
        private string _failJumpName = string.Empty;

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

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        private string _failJumpId = string.Empty;

        public async Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null)
        {
            if (DelayTimeMs > 0)
            {
                await Task.Delay(DelayTimeMs, token);
            }
        }
    }

    public enum WindowControlSearchMethod
    {
        ProcessName,
        WindowTitle
    }

    public class WindowControlAction : ReactiveObject, IMacroAction
    {
        private string _targetName = string.Empty; // ProcessName or WindowTitle
        private WindowControlSearchMethod _searchMethod = WindowControlSearchMethod.ProcessName;
        private WindowControlState _windowState = WindowControlState.Restore;
        private string _failJumpName = string.Empty;
        private string _failJumpId = string.Empty;

        // 하위 호환성을 위해 ProcessName 속성은 유지하되 TargetName과 연동
        public string ProcessName
        {
            get => _targetName;
            set => this.RaiseAndSetIfChanged(ref _targetName, value);
        }

        public string TargetName
        {
            get => _targetName;
            set => this.RaiseAndSetIfChanged(ref _targetName, value);
        }

        public WindowControlSearchMethod SearchMethod
        {
            get => _searchMethod;
            set => this.RaiseAndSetIfChanged(ref _searchMethod, value);
        }

        public WindowControlState WindowState
        {
            get => _windowState;
            set => this.RaiseAndSetIfChanged(ref _windowState, value);
        }

        public string FailJumpName
        {
            get => _failJumpName;
            set => this.RaiseAndSetIfChanged(ref _failJumpName, value);
        }

        public string FailJumpId
        {
            get => _failJumpId;
            set => this.RaiseAndSetIfChanged(ref _failJumpId, value);
        }

        public async Task ExecuteAsync(CancellationToken token, System.Windows.Point? conditionPoint = null)
        {
            await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(TargetName))
                    throw new Exception("Target name (Process or Title) is empty.");

                IntPtr hWnd = IntPtr.Zero;

                if (SearchMethod == WindowControlSearchMethod.ProcessName)
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(TargetName);
                    if (processes.Length == 0)
                        throw new Exception($"Process '{TargetName}' not found.");

                    // Main Window가 있는 첫 번째 프로세스 찾기
                    foreach (var p in processes)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            hWnd = p.MainWindowHandle;
                            break;
                        }
                    }
                    
                    if (hWnd == IntPtr.Zero)
                        throw new Exception($"Process '{TargetName}' found but has no main window.");
                }
                else // WindowTitle
                {
                    hWnd = InputHelper.FindWindowByTitle(TargetName);
                    if (hWnd == IntPtr.Zero)
                        throw new Exception($"Window with title containing '{TargetName}' not found.");
                }

                int nCmdShow = InputHelper.SW_RESTORE;
                switch (WindowState)
                {
                    case WindowControlState.Maximize:
                        nCmdShow = InputHelper.SW_SHOWMAXIMIZED;
                        break;
                    case WindowControlState.Minimize:
                        nCmdShow = InputHelper.SW_SHOWMINIMIZED;
                        break;
                    case WindowControlState.Restore:
                        nCmdShow = InputHelper.SW_RESTORE;
                        break;
                }

                if (!token.IsCancellationRequested)
                {
                    InputHelper.ShowWindow(hWnd, nCmdShow);
                    
                    if (WindowState != WindowControlState.Minimize)
                    {
                        InputHelper.SetForegroundWindow(hWnd);
                    }
                }
            }, token);
        }
    }

    #endregion

    #region SequenceItem

    public class SequenceItem : ReactiveObject
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private bool _isEnabled = true;
        private IMacroCondition? _preCondition;
        private IMacroAction _action;
        private IMacroCondition? _postCondition;

        private int _retryCount = 0;
        private int _retryDelayMs = 500;
        private int _repeatCount = 1;
        
        // Coordinate Mode Fields
        private CoordinateMode _coordinateMode = CoordinateMode.Global;
        private WindowControlSearchMethod _contextSearchMethod = WindowControlSearchMethod.ProcessName;
        private WindowControlState _contextWindowState = WindowControlState.Maximize;

        private string _targetProcessName = string.Empty;
        private string _processNotFoundJumpName = string.Empty;
        private string _processNotFoundJumpId = string.Empty;
        private int _refWindowWidth = 1920;
        private int _refWindowHeight = 1080;

        private string _successJumpName = string.Empty;
        private string _successJumpId = string.Empty; // Guid string or special tag

        public SequenceItem(IMacroAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        // JSON Deserialization을 위한 기본 생성자 (필요 시)
        [JsonConstructor]
        public SequenceItem(Guid id, string name, bool isEnabled, IMacroCondition? preCondition, IMacroAction? action, IMacroCondition? postCondition, int retryCount = 0, int retryDelayMs = 500, int repeatCount = 1, string successJumpName = "", string successJumpId = "")
        {
             _id = id == Guid.Empty ? Guid.NewGuid() : id;
             _name = name;
             _isEnabled = isEnabled;
             _preCondition = preCondition;
             _action = action ?? new IdleAction();
             _postCondition = postCondition;
             _retryCount = retryCount;
             _retryDelayMs = retryDelayMs;
             _repeatCount = repeatCount;
             _successJumpName = successJumpName;
             _successJumpId = successJumpId;
        }

        public Guid Id => _id;

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

        public CoordinateMode CoordinateMode
        {
            get => _coordinateMode;
            set => this.RaiseAndSetIfChanged(ref _coordinateMode, value);
        }

        public WindowControlSearchMethod ContextSearchMethod
        {
            get => _contextSearchMethod;
            set => this.RaiseAndSetIfChanged(ref _contextSearchMethod, value);
        }

        public WindowControlState ContextWindowState
        {
            get => _contextWindowState;
            set => this.RaiseAndSetIfChanged(ref _contextWindowState, value);
        }

        public string TargetProcessName
        {
            get => _targetProcessName;
            set => this.RaiseAndSetIfChanged(ref _targetProcessName, value);
        }

        public string ProcessNotFoundJumpName
        {
            get => _processNotFoundJumpName;
            set => this.RaiseAndSetIfChanged(ref _processNotFoundJumpName, value);
        }

        public string ProcessNotFoundJumpId
        {
            get => _processNotFoundJumpId;
            set => this.RaiseAndSetIfChanged(ref _processNotFoundJumpId, value);
        }

        public int RefWindowWidth
        {
            get => _refWindowWidth;
            set => this.RaiseAndSetIfChanged(ref _refWindowWidth, value);
        }

        public int RefWindowHeight
        {
            get => _refWindowHeight;
            set => this.RaiseAndSetIfChanged(ref _refWindowHeight, value);
        }

        public string SuccessJumpName
        {
            get => _successJumpName;
            set => this.RaiseAndSetIfChanged(ref _successJumpName, value);
        }

        public string SuccessJumpId
        {
            get => _successJumpId;
            set => this.RaiseAndSetIfChanged(ref _successJumpId, value);
        }

        public void ResetId()
        {
            _id = Guid.NewGuid();
        }
    }

    public class SequenceGroup : ReactiveObject
    {
        private string _name = "Group";
        private ObservableCollection<SequenceItem> _items = new ObservableCollection<SequenceItem>();

        // Shared Context Fields
        private CoordinateMode _coordinateMode = CoordinateMode.Global;
        private WindowControlSearchMethod _contextSearchMethod = WindowControlSearchMethod.ProcessName;
        private WindowControlState _contextWindowState = WindowControlState.Maximize;
        private string _targetProcessName = string.Empty;
        private string _processNotFoundJumpName = string.Empty;
        private string _processNotFoundJumpId = string.Empty;
        private int _refWindowWidth = 1920;
        private int _refWindowHeight = 1080;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public ObservableCollection<SequenceItem> Items
        {
            get => _items;
            set => this.RaiseAndSetIfChanged(ref _items, value);
        }

        public CoordinateMode CoordinateMode
        {
            get => _coordinateMode;
            set => this.RaiseAndSetIfChanged(ref _coordinateMode, value);
        }

        public WindowControlSearchMethod ContextSearchMethod
        {
            get => _contextSearchMethod;
            set => this.RaiseAndSetIfChanged(ref _contextSearchMethod, value);
        }

        public WindowControlState ContextWindowState
        {
            get => _contextWindowState;
            set => this.RaiseAndSetIfChanged(ref _contextWindowState, value);
        }

        public string TargetProcessName
        {
            get => _targetProcessName;
            set => this.RaiseAndSetIfChanged(ref _targetProcessName, value);
        }

        public string ProcessNotFoundJumpName
        {
            get => _processNotFoundJumpName;
            set => this.RaiseAndSetIfChanged(ref _processNotFoundJumpName, value);
        }

        public string ProcessNotFoundJumpId
        {
            get => _processNotFoundJumpId;
            set => this.RaiseAndSetIfChanged(ref _processNotFoundJumpId, value);
        }

        public int RefWindowWidth
        {
            get => _refWindowWidth;
            set => this.RaiseAndSetIfChanged(ref _refWindowWidth, value);
        }

        public int RefWindowHeight
        {
            get => _refWindowHeight;
            set => this.RaiseAndSetIfChanged(ref _refWindowHeight, value);
        }

        public SequenceGroup()
        {
        }
    }

    #endregion
}
