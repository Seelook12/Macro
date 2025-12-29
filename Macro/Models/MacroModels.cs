using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReactiveUI;

namespace Macro.Models
{
    #region Interfaces

    /// <summary>
    /// 매크로 실행 전/후 조건을 정의하는 인터페이스
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
    [JsonDerivedType(typeof(DelayCondition), typeDiscriminator: nameof(DelayCondition))]
    [JsonDerivedType(typeof(ImageMatchCondition), typeDiscriminator: nameof(ImageMatchCondition))]
    public interface IMacroCondition
    {
        Task<bool> CheckAsync();
    }

    /// <summary>
    /// 매크로의 실제 동작을 정의하는 인터페이스
    /// </summary>
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

        public Task<bool> CheckAsync()
        {
            // TODO: 실제 이미지 매칭 로직 구현 필요
            return Task.FromResult(true);
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

        public Task ExecuteAsync()
        {
            // TODO: 실제 마우스 클릭 로직 구현 필요
            return Task.CompletedTask;
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

        public Task ExecuteAsync()
        {
            // TODO: 실제 키보드 입력 로직 구현 필요
            return Task.CompletedTask;
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
