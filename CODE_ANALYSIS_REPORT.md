# Macro 프로젝트 코드 분석 보고서

> 분석 일자: 2026-02-11
> 분석 대상: C:\code\Macro\Macro\ (.NET 9.0 WPF + ReactiveUI)

---

## 목차

1. [Critical 이슈](#1-critical-이슈)
2. [Models 계층](#2-models-계층-macromodelcs)
3. [Services 계층](#3-services-계층)
4. [ViewModels 계층](#4-viewmodels-계층)
5. [Views / Utils 계층](#5-views--utils-계층)
6. [전체 요약](#6-전체-요약)

---

## 1. Critical 이슈

프로젝트 전반에서 발견된 가장 시급한 문제들입니다.

| # | 위치 | 문제 | 영향 |
|---|------|------|------|
| C1 | `MacroModels.cs:2070` | `SequenceGroup.Id`가 `{ get; } = Guid.NewGuid()`로 선언되어 JSON 역직렬화 시 매번 새 ID 생성 | **점프 기능 완전 붕괴 가능** |
| C2 | `MacroModels.cs:651-652, 1447` | `ConcurrentDictionary`에서 `ContainsKey` + 인덱서 분리 접근 (race condition) | `KeyNotFoundException` 발생 가능 |
| C3 | `MacroModels.cs:933` | `TimeoutCheckCondition`이 `IMacroCondition`의 `[JsonDerivedType]` 목록에 미등록 | 직렬화 시 타입 정보 손실 |
| C4 | `MacroEngineService.cs:178-184` | `IsRunning` 검사-설정 사이 race condition | 엔진 이중 실행 가능 |
| C5 | `MacroEngineService.cs:673-679` | `Stop()`에서 `_cts` null 체크 후 접근 사이 경합 | `NullReferenceException` |
| C6 | `ImageSearchService.cs:25-33` | `ClearCache()` 중 다른 스레드가 `Mat` 사용 중이면 Dispose됨 | `ObjectDisposedException` / 접근 위반 |
| C7 | `MacroEngineService.cs:545-554` | 점프 대상이 자기 자신일 때 무한 루프 | 프로그램 행(hang) |
| C8 | 모든 ViewModel | `ReactiveCommand`에 `.ThrownExceptions.Subscribe()` 없음 | 비동기 예외 시 앱 크래시 |

---

## 2. Models 계층 (`MacroModels.cs`)

### 2.1 구조적 문제: God File

`MacroModels.cs` (2242줄) 하나에 **25개 이상의 타입**이 집중되어 있습니다.

- enum 7개, 인터페이스 4개, Condition 7개, Action 8개, 기타 6개
- 새 Condition/Action 추가 시 이 파일 + `[JsonDerivedType]` 어트리뷰트를 동시 수정해야 함
- merge conflict 빈번, 검색/탐색 어려움

### 2.2 MVVM 위반: Model이 Service에 직접 의존

Model 클래스들이 `MacroEngineService.Instance`를 **37회** 직접 호출합니다.

```
DelayCondition.CheckAsync()          → MacroEngineService.Instance.AddLog(), .Variables
ImageMatchCondition.CheckAsync()     → MacroEngineService.Instance.UpdateVariable()
VariableCompareCondition             → MacroEngineService.Instance.Variables
SwitchCaseCondition                  → MacroEngineService.Instance.Variables
VariableSetAction.ExecuteAsync()     → MacroEngineService.Instance.UpdateVariable()
```

또한 Model 내에서 직접 수행하는 것들:
- 화면 캡처 (`ScreenCaptureHelper.GetScreenCapture()`) — 줄 346
- 이미지 검색 (`ImageSearchService.FindImageDetailed()`) — 줄 384
- 마우스/키보드 입력 (`InputHelper.MoveAndClick()`) — 줄 1216, 1279
- 파일 경로 해석 (`RecipeManager.Instance`) — 줄 362

### 2.3 메모리 관리 문제

| 위치 | 문제 |
|------|------|
| 줄 906, 1590 | `Process.GetProcessesByName()` 반환 객체 미Dispose → 핸들 누수 |
| 줄 346-349 | `ImageMatchCondition` 재시도 루프에서 이전 캡처 BitmapSource 미해제 |
| 줄 296-300 | `DebugImage` (BitmapSource) 보유하는데 IDisposable 미구현 |

### 2.4 직렬화 문제

| 위치 | 문제 |
|------|------|
| 줄 933 | `TimeoutCheckCondition`이 `[JsonDerivedType]` 목록에 누락 **(Critical)** |
| 줄 935-936 | `TimeoutCheckCondition._inner`에 `[JsonIgnore]` 없음 → 순환 참조 가능 |
| 줄 1541-1551 | `WindowControlAction.ProcessName`과 `TargetName`이 동일 backing field 공유 → 역직렬화 순서 의존 |
| 줄 1145-1153 | `UseConditionAddress`가 `SourceType`과 동시 존재 시 역직렬화 순서에 따라 값 덮어쓰기 |
| 줄 2139-2155 | `SequenceGroup.Items` setter가 `Nodes`에 추가 → JSON에 둘 다 있으면 아이템 중복 |

### 2.5 타입 안전성

매직 스트링이 enum 대신 사용되고 있습니다:

| 위치 | 필드 | 값 |
|------|------|-----|
| 줄 605 | `VariableCompareCondition.Operator` | `"==", "!=", ">", "<"` 등 |
| 줄 1070 | `MouseClickAction.ClickType` | `"Left", "Right", "Double"` |
| 줄 1396 | `VariableSetAction.Operation` | `"Set", "Add", "Sub"` |

### 2.6 코드 중복

**변수 해석 패턴** — 동일한 패턴이 **6곳**에서 반복:
```csharp
if (SourceType == ValueSourceType.Variable && !string.IsNullOrEmpty(VariableName))
{
    var vars = MacroEngineService.Instance.Variables;
    if (vars.TryGetValue(varName, out var valStr) && int.TryParse(valStr, out int val))
        finalValue = val;
}
```
→ `VariableResolver.ResolveInt()` 같은 헬퍼로 추출 필요

**SetTransform 패턴** — `ImageMatchCondition`, `GrayChangeCondition`, `MouseClickAction` 3곳에서 동일한 4필드 + 동일 메서드 반복

**FailJumpName/FailJumpId** — 13개 클래스에서 동일 프로퍼티 쌍 반복 → 기본 클래스로 추출 가능

### 2.7 잠재적 버그

| 위치 | 문제 |
|------|------|
| 줄 328, 417 | `_foundPoint` backing field 직접 대입 → `PropertyChanged` 미발생 → UI 미갱신 |
| 줄 594, 1287 | 빈 `catch` 블록으로 예외 삼킴 → 디버깅 불가 |
| 줄 664-669 | `double` 비교에서 `==` 사용 → "1.0"과 "1.00"이 다르게 평가 |
| 줄 1977-1980 | `ResetId()` 호출 시 `RaisePropertyChanged` 미호출 → 참조 불일치 |

---

## 3. Services 계층

### 3.1 스레드 안전성

| 위치 | 문제 | 심각도 |
|------|------|--------|
| `MacroEngineService.cs:178-184` | `RunAsync()` 진입 체크의 race condition | **높음** |
| `MacroEngineService.cs:673-679` | `Stop()`에서 `_cts` null 경합 | **높음** |
| `MacroEngineService.cs:42-43` | `_isRunning`, `_isPaused`에 `volatile` 없음 | 중간 |
| `MacroEngineService.cs:131-132` | `_currentSequenceItem` 필드 동기화 부재 | 중간 |
| `MacroEngineService.cs:99` | Property setter의 `Schedule()` 클로저로 인한 backing field 경합 | 중간 |
| `ImageSearchService.cs:58-62` | 동시 캐시 미스 시 `Mat` 인스턴스 누수 | 중간 |

### 3.2 리소스 관리

| 위치 | 문제 |
|------|------|
| `ImageSearchService.cs:19-20` | 캐시에 `Mat` 덮어쓸 때 기존 `Mat` 미Dispose |
| `ImageSearchService.cs:25-33` | `ClearCache()` 중 다른 스레드가 사용 중인 `Mat` Dispose → 크래시 |
| `MacroEngineService.cs:779` | `Process.GetProcessesByName()` 결과 미Dispose |
| `MacroEngineService.cs:14` | `MacroEngineService`가 `IDisposable` 미구현 |
| `HotkeyService.cs:7-52` | `IDisposable` 미구현, `UnregisterAll()` 없음 |
| `MacroEngineService.cs:47` | `_logLock`이 `readonly` 아님 |

### 3.3 에러 처리

| 위치 | 문제 |
|------|------|
| `MacroEngineService.cs:712` | `catch { }` — 로그 파일 쓰기 실패 완전 무시 |
| `ImageSearchService.cs:188-191` | `catch { return 0; }` — 모든 예외 삼킴 |
| `ImageSearchService.cs:154-157` | `Debug.WriteLine`만 사용 → Release에서 정보 없음 |
| `RecipeCompiler.cs:240-253` | `CloneItem` 실패 시 null 반환 → 스텝 조용히 누락 |
| `MacroEngineService.cs:623` | `RunSingleStepAsync`에서 `CancellationToken.None` → 중단 불가 |

### 3.4 엔진 로직

| 위치 | 문제 |
|------|------|
| `MacroEngineService.cs:545-554` | 자기 자신으로 점프 시 무한 루프 **(Critical)** |
| `MacroEngineService.cs:487-518` | 성공/실패 점프 로직이 복잡하게 얽혀 유지보수 위험 |
| `MacroEngineService.cs:445, 460` | `ForceJumpException`과 `ComponentFailureException`이 모두 `allRepeatsSuccess = false` 설정 → 의미론적 혼동 |
| `MacroEngineService.cs:838` | `Thread.Sleep(500)` → ThreadPool 스레드 블로킹 |

### 3.5 컴파일러 로직

| 위치 | 문제 |
|------|------|
| `RecipeCompiler.cs:232-237` | 타임아웃 래핑 순서가 역순 → 내부 그룹 타임아웃이 먼저 체크됨 |
| `RecipeCompiler.cs:269-278` | `RegisterGlobalVariables`가 **컴파일 시점**에 런타임 변수 변경 (부작용) |
| `RecipeCompiler.cs:240-253` | JSON 직렬화 기반 DeepClone — `[JsonIgnore]` 필드 누락, `JsonSerializerOptions` 매번 재생성 |
| `RecipeCompiler.cs:316-326` | `CompileSingleStep`의 부모 탐색이 O(n*d) |

### 3.6 DI/결합도

모든 서비스가 정적 Singleton 패턴:
- `MacroEngineService.Instance`
- `RecipeCompiler.Instance`
- `HotkeyService.Instance`
- `RecipeManager.Instance`

**문제점:**
- 단위 테스트 불가능 (Mock 불가)
- Model → Service 역방향 의존 (37곳)
- RecipeCompiler ↔ MacroEngineService 순환 의존
- 상태 격리 불가

### 3.7 성능

| 위치 | 문제 |
|------|------|
| `MacroEngineService.cs:536, 542` | 매 스텝마다 `FindIndex` O(n) + `Guid.ToString()` |
| `MacroEngineService.cs:710-711` | 매 로그마다 `File.AppendAllText` (파일 open/close) |
| `RecipeManager.cs:124-168` | 변수 변경마다 `.vars.json` 전체 Read-Modify-Write |
| `RecipeCompiler.cs:240-253` | `JsonSerializerOptions` 매번 재생성 → 캐시 효과 없음 |

---

## 4. ViewModels 계층

### 4.1 메모리 누수 (구독 해제 누락)

| 위치 | 문제 |
|------|------|
| `TeachingViewModel.cs:395-398` | `RecipeManager.Instance` 구독이 `CompositeDisposable`에 미추가 → 영구 참조 |
| `TeachingViewModel.cs:384-385` | `CollectionChanged += 람다` → 해제(unsubscribe) 불가능 |
| `DashboardViewModel.cs:88-108` | `Observable.Interval(100ms)` 무한 타이머의 `IDisposable` 반환값 버림 |
| `MainWindowViewModel.cs:49-53` | 싱글톤 구독 해제 수단 없음 |
| `DashboardViewModel.cs:35-51` | 6개 `ObservableAsPropertyHelper` Dispose 안 됨 |

### 4.2 WhenActivated 미사용/오용

| 위치 | 문제 |
|------|------|
| `TeachingViewModel.cs:380-399` | `IActivatableViewModel` 선언했지만 `WhenActivated` 미사용 → 구독이 영구 활성 |
| `DashboardViewModel.cs:16` | `IActivatableViewModel` 미구현 → 100ms 타이머 영구 실행 |
| `VariableManagerViewModel.cs:43-47` | `WhenActivated` 내부가 사실상 비어있음 (`Disposable.Create(() => { })`) |

### 4.3 코드 크기 / SRP 위반

`TeachingViewModel` 4개 partial 파일 합산: **약 2,470줄**

담당 책임:
- 시퀀스/그룹 CRUD
- JSON 직렬화/역직렬화 (파일 I/O)
- 이미지 선택/캡처/매칭 테스트
- 좌표 관리 및 윈도우 정보 조회
- 변수 관리, 클립보드, Jump Target 관리, 프로세스 목록 조회

### 4.4 커맨드 패턴 문제

| 위치 | 문제 |
|------|------|
| `TeachingViewModel.Commands.cs:19-67` | 모든 커맨드 `= null!` 초기화 → `InitializeCommands()` 전 바인딩 시 NRE |
| `TeachingViewModel.Commands.cs:87-88` | `ReactiveCommand`에 `.ThrownExceptions.Subscribe()` 없음 → 앱 크래시 **(Critical)** |
| `MainWindowViewModel.cs:78-88` | 핫키에서 `Execute()` 호출 시 `CanExecute` false면 `InvalidOperationException` |

### 4.5 상태 관리

| 위치 | 문제 |
|------|------|
| `TeachingViewModel.cs:253-315` | `SelectedGroup` setter에서 4개 메서드 동기 호출 → 부작용 예측 어려움 |
| `TeachingViewModel.cs:22, 121, 138` | `_isUpdatingGroupTargets` 플래그 기반 재진입 방지 → 스레드 안전하지 않음 |
| `TeachingViewModel.Logic.cs:116-156` | backing field 직접 쓰기 + 수동 `RaisePropertyChanged` → 누락 위험 |

### 4.6 잠재적 버그

| 위치 | 문제 |
|------|------|
| `DashboardViewModel.cs:96` | 100ms마다 새 `ObservableCollection` 인스턴스 생성 → UI 깜빡임 + GC 압력 |
| `DashboardViewModel.cs:162, 169, 179, 218, 229` | ViewModel에서 직접 `MessageBox.Show()` → MVVM 위반 |
| `TeachingViewModel.Logic.cs:459` | `File.ReadAllText` 동기 I/O → UI 프리징 |
| `TeachingViewModel.Clipboard.cs:96` | `PasteGroup`에서 `SanitizeLoadedData()` 불필요하게 전체 호출 → 기존 그룹 ID 변경 위험 |
| `TeachingViewModel.Logic.cs:348-364` | `FindParentGroup` 순환 참조 시 무한 루프 (방문 노드 추적 없음) |
| `TeachingViewModel.Logic.cs:713-721` | `JsonSerializerOptions` 매번 재생성 → 성능 저하 |
| `TeachingViewModel.Logic.cs:884-889` | `DeleteImageIfOrphaned()` 구현 없음 → 고아 이미지 파일 누적 |
| `VariableManagerViewModel.cs:68-96` | `LoadVariables()` 데드 코드 |

---

## 5. Views / Utils 계층

### 5.1 Code-behind MVVM 위반

| 위치 | 문제 |
|------|------|
| `TeachingView.xaml.cs:133-204` | 이미지 크롭, 좌표 변환, 파일 저장 로직이 View에 직접 존재 |
| `TeachingView.xaml.cs:225-261` | TreeView 선택 변경에서 ViewModel 직접 조작 |
| `TeachingView.xaml.cs:174` | `Path.GetTempFileName()` → 원래 .tmp 파일 미삭제 + .png 파일도 미정리 → 임시 파일 무한 누적 |

### 5.2 TeachingView.xaml 크기 (55KB, 676줄)

하나의 XAML에 13개 이상의 DataTemplate + TreeView + Group/Step Editor 모두 포함.

**분리 권장:**
- DataTemplate → `TeachingView.DataTemplates.xaml` ResourceDictionary
- Group Editor → `GroupEditorView.xaml` UserControl
- Step Editor → `StepEditorView.xaml` UserControl

### 5.3 XAML 문제

| 위치 | 문제 |
|------|------|
| `TeachingView.xaml` 다수 | 하드코딩된 색상값 대량 (`#C62828`, `#EF6C00` 등) → ResourceDictionary 미사용 |
| `TeachingView.xaml` vs `DashboardView.xaml` | **테마 불일치** — TeachingView는 밝은 테마, DashboardView는 어두운 테마 |
| `TeachingView.xaml:349` | `KeyPressAction`, `TextTypeAction` 등의 DataTemplate 누락 |
| `TeachingView.xaml:600-604` | 그룹 PostCondition의 JumpTarget 목록이 `JumpTargets` 참조 → `AvailableGroupExitTargets`여야 함 |
| `TeachingView.xaml:388` | 컬럼 너비 350px 하드코딩 |

### 5.4 커스텀 컨트롤

| 위치 | 문제 |
|------|------|
| `JumpTargetSelector.xaml.cs:46-55` | 컨트롤 Unloaded 시 `CollectionChanged` 이벤트 미해제 |
| `JumpTargetSelector.xaml.cs:129-132` | 빈 `catch` 블록 — 모든 예외 삼킴 |
| `VariableSelector.xaml.cs:57-68` | `_isItemsSourceChanging` 플래그 — 빠른 연속 변경 시 꼬임 가능 |

### 5.5 IViewFor 패턴

| 위치 | 문제 |
|------|------|
| `TeachingView.xaml.cs:39` | `WhenActivated` 시점에 `ViewModel == null`이면 Interaction 핸들러 영구 미등록 |
| `DashboardView.xaml.cs:52-70` | ViewModel 여러 번 변경 시 내부 `FromEventPattern` 구독 누적 → `SerialDisposable` 필요 |

### 5.6 Win32 API (InputHelper)

| 위치 | 문제 |
|------|------|
| `InputHelper.cs:19, 22` | `dwExtraInfo`가 `int` (4바이트) → Win32는 `ULONG_PTR` (8바이트) → 64비트 정렬 문제 |
| `InputHelper.cs` 전체 | `mouse_event`, `keybd_event` 사용 → Microsoft deprecated, `SendInput` 권장 |
| `InputHelper.cs:226` | `static readonly Random _random` → 스레드 안전하지 않음 → `Random.Shared` 권장 |

### 5.7 파일 I/O (RecipeManager, SettingsManager)

| 위치 | 문제 |
|------|------|
| `RecipeManager.cs:104-122` | `LoadSequenceData`에 락 없음 → 동시 접근 시 race condition |
| `RecipeManager.cs:124-168` | `_varFileLock`은 프로세스 내부만 보호 → 외부 접근 미보호 |
| `RecipeManager.cs:26-44` | `CurrentRecipe` setter에서 파일 I/O 3회 수행 (UI 스레드 블로킹 가능) |
| `RecipeManager.cs:75, 100, 118` | 다수의 빈 `catch` 블록 |
| `SettingsManager.cs` | `LoadSettings`/`SaveSettings` 동시 호출 시 파일 충돌 (락 없음) |

### 5.8 기타

| 위치 | 문제 |
|------|------|
| `DebugLogger.cs:9` | 로그 파일 크기 제한/로테이션 없음 → 무한 성장 |
| `DebugLogger.cs` | 매 호출마다 `File.AppendAllText` (open/close) → 성능 저하 |
| `MainWindow.xaml.cs:37-39` | 핫키 ID `9001, 9002, 9003` 매직 넘버 하드코딩 |

---

## 6. 전체 요약

### 심각도별 이슈 수

| 심각도 | 개수 | 대표 이슈 |
|--------|------|-----------|
| **Critical** | 8 | SequenceGroup.Id 역직렬화, Dictionary race condition, 엔진 이중실행, 커맨드 예외 미처리 |
| **High** | 15 | Process 핸들 누수, 구독 해제 누락, ImageCache 동시성, PropertyChanged 미발생 |
| **Medium** | 25 | MVVM 위반, 코드 중복, 매직 스트링, JsonOptions 재생성, 동기 I/O |
| **Low** | 12 | 네이밍 혼란, 데드 코드, 주석 언어 혼용, 하드코딩 색상 |

### 아키텍처 레벨 문제

1. **Singleton 남용 + DI 부재** — 모든 서비스가 정적 싱글톤, Model이 Service에 역방향 의존 (37곳), 단위 테스트 불가능
2. **God File / God Class** — `MacroModels.cs` (2242줄), `TeachingViewModel` (2470줄), `TeachingView.xaml` (55KB)
3. **관심사 미분리** — Model이 화면 캡처, 입력 시뮬레이션, 파일 I/O를 직접 수행
4. **ReactiveUI 패턴 미준수** — `WhenActivated` 미사용, `CompositeDisposable` 미사용, `ObservableAsPropertyHelper` 미Dispose

### 권장 개선 우선순위

1. **즉시 수정** — ~~Critical 이슈 8건~~ **완료 (2026-02-11)**
2. **단기 개선** — ~~메모리 누수 (구독 해제, Process Dispose, ImageCache)~~ **완료 (2026-02-11)** — High 이슈 9건 수정
3. **중기 리팩토링** — MacroModels.cs 파일 분리, 변수 해석 헬퍼 추출, ~~JsonSerializerOptions 캐싱~~ 완료
4. **장기 아키텍처** — DI 컨테이너 도입, Model에서 Service 의존 제거, TeachingViewModel 분리

---

## 7. 수정 이력

### 2026-02-11 — Critical 이슈 8건 수정 완료

| # | 이슈 | 수정 내용 | 파일 |
|---|------|-----------|------|
| C1 | `SequenceGroup.Id` 역직렬화 시 새 GUID 생성 | `[JsonConstructor]` 추가, Id를 backing field 방식으로 변경하여 역직렬화 시 원래 ID 복원 | `MacroModels.cs` |
| C2 | `ConcurrentDictionary` ContainsKey + 인덱서 race condition | `ContainsKey` + 인덱서 → `TryGetValue` 패턴으로 교체 (2곳) | `MacroModels.cs:652, 1447` |
| C3 | `TimeoutCheckCondition` JsonDerivedType 미등록 | `IMacroCondition`의 `[JsonDerivedType]` 목록에 `TimeoutCheckCondition` 추가 | `MacroModels.cs:41` |
| C4 | `RunAsync()` 이중 실행 가능 | `Interlocked.CompareExchange`로 원자적 진입 보장. `_isRunning` → `_isRunningFlag` (int), `_isPaused` → `_isPausedFlag` (int). `_logLock`에 `readonly` 추가 | `MacroEngineService.cs` |
| C5 | `Stop()`에서 `_cts` null 경합 | `_cts`를 로컬 변수에 캐싱 후 사용하여 `NullReferenceException` 방지 | `MacroEngineService.cs:676-683` |
| C6 | `ImageSearchService.ClearCache` 동시성 문제 | `ClearCache()`: 먼저 모든 Mat를 제거 후 일괄 Dispose. 캐시 추가: `_imageCache[key] = ...` → `_imageCache.GetOrAdd()` 사용 | `ImageSearchService.cs` |
| C7 | 점프 대상 자기 자신일 때 무한 루프 | 연속 동일 인덱스 점프 감지 카운터 추가 (10,000회 초과 시 자동 중지 + 로그) | `MacroEngineService.cs` |
| C8 | `ReactiveCommand` ThrownExceptions 미처리 | 모든 ViewModel(Teaching, Dashboard, Recipe, MainWindow, VariableManager)의 커맨드에 `.ThrownExceptions.Subscribe()` 추가 | 5개 ViewModel 파일 |

### 2026-02-11 — High 이슈 9건 수정 완료

| # | 이슈 | 수정 내용 | 파일 |
|---|------|-----------|------|
| H1 | `Process.GetProcessesByName()` 핸들 누수 (4곳) | 모든 Process 배열에 Dispose 추가 (`try/finally` 패턴 또는 루프 내 Dispose) | `MacroModels.cs:907,1591`, `MacroEngineService.cs:804`, `TeachingViewModel.Logic.cs:918` |
| H2 | `TimeoutCheckCondition._inner` `[JsonIgnore]` 누락 | `Inner` 프로퍼티에 `[JsonIgnore]` 추가하여 순환 참조 방지 | `MacroModels.cs:938` |
| H3 | `_foundPoint` backing field 직접 대입 → PropertyChanged 미발생 | `_foundPoint = null` / `_foundPoint = new Point(...)` → `FoundPoint` 프로퍼티 setter 사용으로 변경 | `MacroModels.cs:329,418` |
| H4 | `DashboardViewModel` 100ms 타이머 영구 실행 + ObservableCollection 매번 재생성 | `IActivatableViewModel` 구현, `Observable.Interval`을 `WhenActivated` 내부로 이동, `TimeoutListEquals`로 Smart Update 적용 | `DashboardViewModel.cs` |
| H5 | `TeachingViewModel` 구독 해제 누락 | `CollectionChanged += 람다` → 명명된 메서드 참조로 변경 (해제 가능), `RecipeManager` 구독을 `IDisposable` 필드에 저장 | `TeachingViewModel.cs` |
| H6 | `MainWindowViewModel` 구독 해제 수단 없음 | `IDisposable` 구현 + `CompositeDisposable`에 RecipeManager 구독 추가 | `MainWindowViewModel.cs` |
| H7 | `JumpTargetSelector` Unloaded 시 CollectionChanged 미해제 | `Unloaded` 이벤트 핸들러에서 `CollectionChanged` 구독 해제 + 빈 catch를 `InvalidCastException` 캐치로 변경 | `JumpTargetSelector.xaml.cs` |
| H8 | `DashboardView` ViewModel 변경 시 구독 누적 | `SerialDisposable`로 이전 로그 스크롤 구독 자동 해제, 불필요한 `Fluent` using 정리 | `DashboardView.xaml.cs` |
| H9 | `TeachingView` WhenActivated 시 ViewModel null이면 Interaction 미등록 | `if (ViewModel != null)` 체크 → `WhenAnyValue(x => x.ViewModel).WhereNotNull().Take(1)` 패턴으로 ViewModel 도착 시 등록 | `TeachingView.xaml.cs` |

### 2026-02-11 — Medium 이슈 6건 수정 완료

| # | 이슈 | 수정 내용 | 파일 |
|---|------|-----------|------|
| M1 | `JsonSerializerOptions` 매번 재생성 → 캐시 효과 없음 | 각 클래스에 `static readonly` 인스턴스로 캐싱 (6곳) | `TeachingViewModel.Logic.cs`, `RecipeCompiler.cs`, `DashboardViewModel.cs`, `RecipeManager.cs`, `VariableManagerViewModel.cs` |
| M2 | 빈 `catch` 블록으로 예외 삼킴 (다수) | `catch {}` → `catch (Exception ex) { Debug.WriteLine(...) }` 패턴으로 교체 (13곳) | `MacroModels.cs`, `RecipeManager.cs`, `TeachingViewModel.Logic.cs`, `DebugLogger.cs` 등 |
| M3 | `Thread.Sleep(500)` → ThreadPool 블로킹 | `ConfigureRelativeCoordinates`를 `async Task`로 변경, `Thread.Sleep` → `await Task.Delay` | `MacroEngineService.cs` |
| M4 | `double` 비교에서 `==` 사용 → "1.0"과 "1.00" 불일치 | `==`/`!=` 연산 시 양쪽 모두 `double.TryParse` 성공하면 epsilon 기반 비교, 아니면 문자열 비교 | `MacroModels.cs:659-660` |
| M5 | `WindowControlAction.ProcessName`과 `TargetName`이 동일 backing field → 역직렬화 순서 의존 | `ProcessName`에 `[JsonIgnore]` 추가, `TargetName`만 직렬화/역직렬화 | `MacroModels.cs:1555` |
| M6 | `VariableManagerViewModel.LoadVariables()` 데드 코드 | 미사용 메서드 및 관련 `_readOptions` 필드 제거 | `VariableManagerViewModel.cs` |
| M7 | `VariableCompareCondition` double 비교 정밀도 | `double.Epsilon` 대신 `0.0001`을 사용하여 실제 부동 소수점 비교 안정성 확보 | `MacroModels.cs` |
| M8 | 변수명 공백(Trim) 처리 보완 | `VariableCompareCondition`, `VariableSetAction` 등에 `Trim()`을 추가하여 사용자 입력 오차 방지 | `MacroModels.cs` |
| M9 | `RecipeCompiler` 컴파일 시 부작용 제거 | 컴파일 시점에 런타임 변수 저장 파일(`.vars.json`)이 덮어씌워지는 문제 수정 (`UpdateVariableWithoutSave` 도입) | `RecipeCompiler.cs`, `MacroEngineService.cs` |
| M10 | Win32 API 파라미터 타입 수정 | `mouse_event`, `keybd_event`의 `dwExtraInfo` 타입을 `UIntPtr`로 변경하여 64비트 안정성 확보 | `InputHelper.cs` |
| M11 | `Random` 스레드 안전성 개선 | `static Random` 대신 .NET 9.0의 `Random.Shared`를 사용하여 동시성 이슈 방지 | `InputHelper.cs` |
| M12 | `SettingsManager` 파일 락 추가 | 설정 파일 로드/저장 시 `lock`을 추가하여 동시 접근 충돌 방지 | `SettingsManager.cs` |
| M13 | `RecipeManager` 파일 락 추가 | 레시피 데이터 로드 시 `lock`을 추가하여 안정성 확보 | `RecipeManager.cs` |
| M14 | 로그 파일 무한 증식 방지 | `DebugLogger`에 1MB 크기 제한을 추가하여 로그 파일이 무한히 커지는 문제 해결 | `DebugLogger.cs` |
| M15 | 핫키 매직 넘버 제거 | 하드코딩된 핫키 ID를 `HotkeyService`의 상수로 교체 | `MainWindow.xaml.cs`, `MainWindowViewModel.cs` |
| M16 | `SettingsManager` 성능 최적화 | `JsonSerializerOptions`를 캐싱하여 불필요한 객체 생성 방지 | `SettingsManager.cs` |
| M17 | 로그 기록 예외 처리 보완 | 로그 파일 쓰기 실패 시 빈 catch 블록을 수정하여 Debug 출력 추가 | `MacroEngineService.cs` |

### 2026-02-11 — High 이슈 10건 수정 완료

| # | 이슈 | 수정 내용 | 파일 |
|---|------|-----------|------|
... (H1-H9 생략) ...
| H9 | `TeachingView` WhenActivated 시 ViewModel null이면 Interaction 미등록 | `if (ViewModel != null)` 체크 → `WhenAnyValue(x => x.ViewModel).WhereNotNull().Take(1)` 패턴으로 ViewModel 도착 시 등록 | `TeachingView.xaml.cs` |
| H10 | `ImageSearchService` Mat 인스턴스 누수 | `GetOrAdd` 대신 Double-check lock을 사용하여 동시 캐시 미스 시 Mat 누수 방지 | `ImageSearchService.cs` |
