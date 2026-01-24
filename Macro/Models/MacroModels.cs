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
        WindowRelative,
        ParentRelative
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
    [JsonDerivedType(typeof(SwitchCaseCondition), typeDiscriminator: nameof(SwitchCaseCondition))]
    public interface IMacroCondition : IReactiveObject
    {
        Task<bool> CheckAsync(CancellationToken token = default);
        System.Windows.Point? FoundPoint { get; }
        string FailJumpName { get; set; }
        string FailJumpId { get; set; }

        // Default implementation for Force Jump (Switch-Case)
        Guid? GetForceJumpId() => null;
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(MouseClickAction), typeDiscriminator: nameof(MouseClickAction))]
    [JsonDerivedType(typeof(KeyPressAction), typeDiscriminator: nameof(KeyPressAction))]
    [JsonDerivedType(typeof(VariableSetAction), typeDiscriminator: nameof(VariableSetAction))]
    [JsonDerivedType(typeof(IdleAction), typeDiscriminator: nameof(IdleAction))]
    [JsonDerivedType(typeof(WindowControlAction), typeDiscriminator: nameof(WindowControlAction))]
    [JsonDerivedType(typeof(MultiAction), typeDiscriminator: nameof(MultiAction))]
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

    public enum MouseCoordinateSource
    {
        Fixed,
        Found,
        Variable
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

        // Retry Properties
        private int _maxSearchCount = 1;
        private int _searchIntervalMs = 500;

        public int MaxSearchCount
        {
            get => _maxSearchCount;
            set => this.RaiseAndSetIfChanged(ref _maxSearchCount, value);
        }

        public int SearchIntervalMs
        {
            get => _searchIntervalMs;
            set => this.RaiseAndSetIfChanged(ref _searchIntervalMs, value);
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
            return await Task.Run(async () =>
            {
                int attempts = Math.Max(1, MaxSearchCount);
                int interval = Math.Max(0, SearchIntervalMs);

                for (int i = 0; i < attempts; i++)
                {
                    if (token.IsCancellationRequested) return false;

                    try
                    {
                        // 1. 현재 화면 캡처 및 영역 정보 획득
                        // (반복문 안에서 매번 캡처해야 최신 화면 반영)
                        var captureData = System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                        {
                            var bmp = ScreenCaptureHelper.GetScreenCapture();
                            var bounds = ScreenCaptureHelper.GetScreenBounds();
                            bmp?.Freeze();
                            return new { Image = bmp, Bounds = bounds };
                        });

                        if (captureData?.Image == null) return false;

                        if (string.IsNullOrEmpty(ImagePath)) return false;

                        // [Path Resolve] 상대 경로 처리
                        string targetPath = ImagePath;
                        if (!string.IsNullOrEmpty(targetPath) && !System.IO.Path.IsPathRooted(targetPath))
                        {
                            var currentRecipe = RecipeManager.Instance.CurrentRecipe;
                            if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
                            {
                                var dir = System.IO.Path.GetDirectoryName(currentRecipe.FilePath);
                                if (dir != null)
                                {
                                    targetPath = System.IO.Path.Combine(dir, targetPath);
                                }
                            }
                        }

                        // ROI 영역 확인 (절대 좌표 -> 이미지 로컬 좌표 변환)
                        System.Windows.Rect? roi = null;
                        if (UseRegion && RegionW > 0 && RegionH > 0)
                        {
                            double rx = (RegionX * _scaleX + _offsetX) - captureData.Bounds.Left;
                            double ry = (RegionY * _scaleY + _offsetY) - captureData.Bounds.Top;
                            double rw = RegionW * _scaleX;
                            double rh = RegionH * _scaleY;
                            roi = new System.Windows.Rect(rx, ry, rw, rh);
                        }
                        
                        if (token.IsCancellationRequested) return false;

                        // 2. 이미지 서치
                        // 스케일 정보 전달 (_scaleX, _scaleY는 ConfigureRelativeCoordinates에서 설정됨)
                        var result = ImageSearchService.FindImage(captureData.Image, targetPath, Threshold, roi, _scaleX, _scaleY);

                        if (result.HasValue)
                        {
                            // 이미지 로컬 좌표 -> 스크린 절대 좌표 변환
                            _foundPoint = new System.Windows.Point(
                                result.Value.X + captureData.Bounds.Left, 
                                result.Value.Y + captureData.Bounds.Top);
                            
                            if (!string.IsNullOrEmpty(ResultVariableName))
                            {
                                MacroEngineService.Instance.Variables[ResultVariableName] = "True";
                            }
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // 개별 시도 실패는 무시하고 재시도
                    }

                    // 마지막 시도가 아니면 대기
                    if (i < attempts - 1)
                    {
                        await Task.Delay(interval, token);
                    }
                }

                // 모든 시도 실패
                if (!string.IsNullOrEmpty(ResultVariableName))
                {
                    MacroEngineService.Instance.Variables[ResultVariableName] = "False";
                }
                return false;
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

        public void UpdateReferenceValue(System.Windows.Media.Imaging.BitmapSource capture, (int Left, int Top, int Width, int Height) bounds)
        {
            if (capture == null) return;

            // 절대 좌표 계산
            double absX = X * _scaleX + _offsetX;
            double absY = Y * _scaleY + _offsetY;

            // 이미지 로컬 좌표로 변환
            int tx = (int)(absX - bounds.Left);
            int ty = (int)(absY - bounds.Top);
            
            int tw = (int)(Width * _scaleX);
            int th = (int)(Height * _scaleY);

            ReferenceValue = ImageSearchService.GetGrayAverage(capture, tx, ty, tw, th);
        }

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

                    var captureData = System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                    {
                        var bmp = ScreenCaptureHelper.GetScreenCapture();
                        var bounds = ScreenCaptureHelper.GetScreenBounds();
                        return new { Image = bmp, Bounds = bounds };
                    });

                    if (captureData?.Image == null) return false;

                    // 절대 좌표 계산
                    double absX = X * _scaleX + _offsetX;
                    double absY = Y * _scaleY + _offsetY;

                    // 이미지 로컬 좌표로 변환
                    int tx = (int)(absX - captureData.Bounds.Left);
                    int ty = (int)(absY - captureData.Bounds.Top);
                    
                    int tw = (int)(Width * _scaleX);
                    int th = (int)(Height * _scaleY);

                    double currentValue = ImageSearchService.GetGrayAverage(captureData.Image, tx, ty, tw, th);


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

    public class VariableDefinition : ReactiveObject
    {
        private string _name = string.Empty;
        private string _defaultValue = string.Empty;
        private string _description = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string DefaultValue
        {
            get => _defaultValue;
            set => this.RaiseAndSetIfChanged(ref _defaultValue, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }
    }

    public class SwitchCaseItem : ReactiveObject
    {
        private int _caseValue;
        private string _jumpId = string.Empty;

        public int CaseValue
        {
            get => _caseValue;
            set => this.RaiseAndSetIfChanged(ref _caseValue, value);
        }

        public string JumpId
        {
            get => _jumpId;
            set => this.RaiseAndSetIfChanged(ref _jumpId, value ?? string.Empty);
        }
    }

    public class SwitchCaseCondition : ReactiveObject, IMacroCondition
    {
        private string _targetVariableName = string.Empty;
        private ObservableCollection<SwitchCaseItem> _cases = new ObservableCollection<SwitchCaseItem>();
        private string _failJumpName = string.Empty;
        private string _failJumpId = string.Empty;

        public string TargetVariableName
        {
            get => _targetVariableName;
            set => this.RaiseAndSetIfChanged(ref _targetVariableName, value);
        }

        public ObservableCollection<SwitchCaseItem> Cases
        {
            get => _cases;
            set => this.RaiseAndSetIfChanged(ref _cases, value);
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

        public System.Windows.Point? FoundPoint => null;

        public async Task<bool> CheckAsync(CancellationToken token = default)
        {
            // SwitchCase는 조건 검사 자체는 항상 통과한 것으로 처리하고, 
            // 실제 분기는 GetForceJumpId()에서 처리함.
            return await Task.FromResult(true);
        }

        public Guid? GetForceJumpId()
        {
            if (string.IsNullOrEmpty(TargetVariableName)) return null;

            var vars = MacroEngineService.Instance.Variables;
            if (vars.TryGetValue(TargetVariableName, out var valStr))
            {
                if (int.TryParse(valStr, out int currentVal))
                {
                    var match = Cases.FirstOrDefault(c => c.CaseValue == currentVal);
                    if (match != null && Guid.TryParse(match.JumpId, out var guid))
                    {
                        return guid;
                    }
                }
            }
            return null;
        }
    }

    #endregion

    #region Action Implementations

    public class MouseClickAction : ReactiveObject, IMacroAction, ISupportCoordinateTransform
    {
        private int _x;
        private int _y;
        private string _clickType = "Left"; // Left, Right, Double
        private int _clickCount = 1;
        private int _clickIntervalMs = 100;
        
        private MouseCoordinateSource _sourceType = MouseCoordinateSource.Fixed;
        private string _xVariableName = string.Empty;
        private string _yVariableName = string.Empty;

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

        public int ClickCount
        {
            get => _clickCount;
            set => this.RaiseAndSetIfChanged(ref _clickCount, value);
        }

        public int ClickIntervalMs
        {
            get => _clickIntervalMs;
            set => this.RaiseAndSetIfChanged(ref _clickIntervalMs, value);
        }

        public MouseCoordinateSource SourceType
        {
            get => _sourceType;
            set
            {
                this.RaiseAndSetIfChanged(ref _sourceType, value);
                this.RaisePropertyChanged(nameof(UseConditionAddress));
            }
        }

        public string XVariableName
        {
            get => _xVariableName;
            set => this.RaiseAndSetIfChanged(ref _xVariableName, value);
        }

        public string YVariableName
        {
            get => _yVariableName;
            set => this.RaiseAndSetIfChanged(ref _yVariableName, value);
        }

        // Backward Compatibility for JSON
        public bool UseConditionAddress
        {
            get => SourceType == MouseCoordinateSource.Found;
            set
            {
                if (value) SourceType = MouseCoordinateSource.Found;
                else if (SourceType == MouseCoordinateSource.Found) SourceType = MouseCoordinateSource.Fixed;
            }
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
            await Task.Run(async () =>
            {
                if (token.IsCancellationRequested) return;

                int finalX = 0;
                int finalY = 0;

                if (SourceType == MouseCoordinateSource.Found && conditionPoint.HasValue)
                {
                    finalX = (int)conditionPoint.Value.X;
                    finalY = (int)conditionPoint.Value.Y;
                }
                else if (SourceType == MouseCoordinateSource.Variable)
                {
                    // 변수 모드: 전역 변수에서 값을 읽어옴 (그룹 범위 변수는 엔진 실행 시점에 전역으로 Flattening된다고 가정)
                    var vars = MacroEngineService.Instance.Variables;
                    int vx = 0, vy = 0;

                    if (vars.TryGetValue(XVariableName, out var xStr)) int.TryParse(xStr, out vx);
                    if (vars.TryGetValue(YVariableName, out var yStr)) int.TryParse(yStr, out vy);

                    // 변수 좌표도 상대 좌표 계산 적용 (변수에 저장된 값이 '창 기준 상대 좌표'라고 가정)
                    finalX = (int)(vx * _scaleX + _offsetX);
                    finalY = (int)(vy * _scaleY + _offsetY);
                }
                else // Fixed
                {
                    finalX = (int)(X * _scaleX + _offsetX);
                    finalY = (int)(Y * _scaleY + _offsetY);
                }
                
                if (!token.IsCancellationRequested)
                {
                    int count = Math.Max(1, ClickCount);
                    int interval = Math.Max(10, ClickIntervalMs);

                    for (int i = 0; i < count; i++)
                    {
                        if (token.IsCancellationRequested) break;
                        
                        InputHelper.MoveAndClick(finalX, finalY, ClickType);
                        
                        if (i < count - 1)
                        {
                            await Task.Delay(interval, token);
                        }
                    }
                }
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

    public class MultiAction : ReactiveObject, IMacroAction, ISupportCoordinateTransform
    {
        private ObservableCollection<IMacroAction> _actions = new ObservableCollection<IMacroAction>();
        private string _failJumpName = string.Empty;
        private string _failJumpId = string.Empty;

        public ObservableCollection<IMacroAction> Actions
        {
            get => _actions;
            set => this.RaiseAndSetIfChanged(ref _actions, value);
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
            foreach (var action in Actions)
            {
                if (token.IsCancellationRequested) break;
                
                // 하위 액션 실행
                // MultiAction 자체는 FailJump를 처리하지 않고, 하위 액션에서 예외가 발생하면 
                // Engine이 MultiAction 레벨의 예외로 처리하거나, 하위 액션이 던진 예외를 그대로 상위로 전파함.
                await action.ExecuteAsync(token, conditionPoint);
            }
        }

        public void SetTransform(double scaleX, double scaleY, int offsetX, int offsetY)
        {
            // 하위 액션들에게 좌표 변환 정보 전파
            foreach (var action in Actions)
            {
                if (action is ISupportCoordinateTransform t)
                {
                    t.SetTransform(scaleX, scaleY, offsetX, offsetY);
                }
            }
        }
    }

    #endregion

    #region SequenceItem

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "NodeType")]
    [JsonDerivedType(typeof(SequenceGroup), typeDiscriminator: "Group")]
    [JsonDerivedType(typeof(SequenceItem), typeDiscriminator: "Item")]
    public interface ISequenceTreeNode
    {
        Guid Id { get; }
        string Name { get; set; }
    }

    public class SequenceItem : ReactiveObject, ISequenceTreeNode
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
        
        // Group Boundary Flags
        private bool _isGroupStart = false;
        private bool _isGroupEnd = false;

        public SequenceItem(IMacroAction action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        // JSON Deserialization을 위한 기본 생성자 (필요 시)
        [JsonConstructor]
        public SequenceItem(Guid id, string name, bool isEnabled, IMacroCondition? preCondition, IMacroAction? action, IMacroCondition? postCondition, int retryCount = 0, int retryDelayMs = 500, int repeatCount = 1, string successJumpName = "", string successJumpId = "", bool isGroupStart = false, bool isGroupEnd = false)
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
             _isGroupStart = isGroupStart;
             _isGroupEnd = isGroupEnd;
        }

        public Guid Id => _id;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public bool IsGroupStart
        {
            get => _isGroupStart;
            set => this.RaiseAndSetIfChanged(ref _isGroupStart, value);
        }

        public bool IsGroupEnd
        {
            get => _isGroupEnd;
            set => this.RaiseAndSetIfChanged(ref _isGroupEnd, value);
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

    public class SequenceGroup : ReactiveObject, ISequenceTreeNode
    {
        private string _name = "Group";
        private ObservableCollection<ISequenceTreeNode> _nodes = new ObservableCollection<ISequenceTreeNode>();
        private ObservableCollection<SequenceItem>? _legacyItems;

        // Shared Context Fields
        private CoordinateMode _coordinateMode = CoordinateMode.Global;
        private WindowControlSearchMethod _contextSearchMethod = WindowControlSearchMethod.ProcessName;
        private WindowControlState _contextWindowState = WindowControlState.Maximize;
        private string _targetProcessName = string.Empty;
        private string _processNotFoundJumpName = string.Empty;
        private string _processNotFoundJumpId = string.Empty;
        private int _refWindowWidth = 1920;
        private int _refWindowHeight = 1080;
        private bool _isStartGroup = false;
        private string _startJumpId = string.Empty;

        public Guid Id { get; } = Guid.NewGuid(); // ISequenceTreeNode requirement

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public bool IsStartGroup
        {
            get => _isStartGroup;
            set => this.RaiseAndSetIfChanged(ref _isStartGroup, value);
        }

        public string StartJumpId
        {
            get => _startJumpId;
            set => this.RaiseAndSetIfChanged(ref _startJumpId, value);
        }

        public ObservableCollection<ISequenceTreeNode> Nodes
        {
            get => _nodes;
            set => this.RaiseAndSetIfChanged(ref _nodes, value);
        }

        // Legacy Property for JSON Compatibility
        public ObservableCollection<SequenceItem>? Items
        {
            get => _legacyItems;
            set
            {
                _legacyItems = value;
                if (_legacyItems != null && _legacyItems.Count > 0)
                {
                    // Migrate legacy items to Nodes
                    foreach (var item in _legacyItems)
                    {
                        Nodes.Add(item);
                    }
                    _legacyItems.Clear(); 
                }
            }
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
