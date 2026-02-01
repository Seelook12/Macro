# 개발 진행 이력 (Development Log)

**작성일**: 2025년 12월 29일 ~ 30일
**프로젝트**: Macro Automation (WPF + ReactiveUI)

## 1. 프로젝트 개요
- **목표**: 업무 자동화 프로그램의 기본 골격(Skeleton) 및 핵심 실행 엔진 구현
- **기술 스택**:
  - Language: C# (.NET 9.0)
  - Framework: WPF (WinForms 혼합 모드 활성화)
  - Library: ReactiveUI.WPF (v22.3.1), OpenCvSharp4.Windows (v4.10)
  - Pattern: MVVM (Model-View-ViewModel)

## 2. 주요 제약 사항 및 구현 방식
- **Hybrid UI**: WPF 환경에서 스크린 캡처 및 시스템 제어를 위해 `UseWindowsForms` 및 `user32.dll` API 활용.
- **Pure DI & Singleton**: `MacroEngineService`, `RecipeManager` 등 핵심 서비스는 싱글톤으로 관리.
- **Fail-safe ViewLocator**: Splat 컨테이너와 직접 매핑을 혼합한 하이브리드 로케이터 사용.

## 3. 구현 내용 상세

### 3.1 ~ 3.4 (이전 기록 생략)
- 네비게이션 구조, ViewLocator 트러블슈팅, 레시피 관리 기본 기능 구현 완료.

### 3.5. 실행 엔진 (MacroEngineService) 구현
- **Core**: `CancellationTokenSource`를 이용한 비동기 실행 및 중지 로직 구현.
- **Sequence**: PreCondition -> Action -> PostCondition의 3단계 실행 파이프라인 완성.
- **Logging**: UI 스레드 안전하게 로그를 수집하여 대시보드에 실시간 출력.

### 3.6. 시스템 제어 (InputHelper)
- **Win32 API**: `user32.dll`의 `mouse_event`, `keybd_event`, `SetCursorPos` 등을 래핑.
- **Mouse**: 절대 좌표 이동, 좌/우/더블 클릭 구현.
- **Keyboard**: WPF `Key` Enum을 가상 키 코드로 변환하여 입력 시뮬레이션 구현.

### 3.7. 이미지 인식 (OpenCV Image Matching)
- **OpenCvSharp4**: `CCoeffNormed` 알고리즘을 사용한 템플릿 매칭 엔진 구현.
- **ROI (Region of Interest)**: 화면 전체가 아닌 지정된 영역만 검색하여 성능과 정확도 향상.
- **Screen Capture**: `Graphics.CopyFromScreen`을 활용한 실시간 화면 캡처 유틸리티 구현.

### 3.8. 티칭 에디터 고도화 (UX/UI)
- **Coordinate Picker**: 메인창을 최소화하고 화면 클릭으로 좌표를 즉시 가져오는 기능.
- **Region Picker**: 드래그앤드롭으로 검색 영역(ROI)을 시각적으로 설정하는 기능.
- **Image Snipping (Capture)**: 스크린샷에서 원하는 부분을 직접 잘라내어 템플릿 이미지로 즉시 등록하는 기능.
- **File Management**: 이미지를 레시피 폴더로 자동 복사하고 미리보기(`UriToBitmapConverter`) 지원.

### 3.9. 문제 해결 이력 (Troubleshooting) - Part 4
1.  **DashboardView 바인딩 실패**
    - 원인: `ReactiveUserControl`의 제네릭 처리 이슈로 인한 DataContext 미연결.
    - 해결: `UserControl` + `IViewFor` 패턴으로 변경하고 코드 비하인드에서 명시적 바인딩 수행.
2.  **WPF/WinForms 모호한 참조 (Ambiguous Reference)**
    - 증상: `Application`, `MessageBox`, `UserControl`, `Point`, `KeyEventArgs` 등 다수 클래스 충돌.
    - 해결: 네임스페이스 별칭(alias) 및 완전한 이름(Fully Qualified Name)을 사용하여 명시적으로 구분.
3.  **NuGet 패키지 복구 실패**
    - 원인: 사설 NuGet 서버 설정으로 인해 공식 패키지 다운로드 불가.
    - 해결: `nuget.config`를 생성하여 `nuget.org` 공식 소스를 우선순위로 추가.
4.  **이미지 미리보기 파일 잠금**
    - 증상: `ImagePath` 바인딩 시 파일이 사용 중이어서 덮어쓰기/삭제 불가.
    - 해결: `OnLoad` 캐시 옵션을 사용하는 `UriToBitmapConverter`를 개발하여 메모리 로드 후 핸들 즉시 해제.

### 3.10. 실행 엔진 고도화 및 편의 기능 (2025-12-30)
- **Retry & Loop**: 각 시퀀스 스텝별로 반복 실행(RepeatCount) 및 조건 실패 시 재시도(RetryCount) 기능을 엔진과 UI에 통합.
- **Global Hotkey**: `RegisterHotKey` Win32 API를 활용한 전역 단축키 서비스 구현. F5(시작), F6(중지) 지원.
- **Multi-Monitor & DPI Aware**: 
    - `SystemParameters.VirtualScreen` 기반의 전체 영역 캡처 및 좌표 픽업 지원.
    - DPI 스케일링 배율을 계산하여 WPF DIP 좌표와 Win32 물리 픽셀 좌표 간의 변환 로직 적용.

### 3.11. 좌표 연동 및 인간적인 동작 구현 (2025-12-30)
- **Condition-Action 좌표 연동**:
    - `IMacroCondition`에 `FoundPoint` 속성 추가 (이미지 매칭 성공 시 중심 좌표 저장).
    - `MouseClickAction`에 `UseConditionAddress` 옵션 추가. 활성화 시 이전 단계에서 찾은 좌표로 자동 이동 및 클릭.
    - 실행 엔진 파이프라인에서 `PreCondition`의 결과 좌표를 `Action`으로 안전하게 전달하도록 개선.
- **Human-like Mouse Movement**:
    - **Smooth Move**: 직선 순간 이동 대신 Sine Easing 함수를 활용하여 가속/감속이 포함된 부드러운 마우스 경로 구현.
    - **Randomization**: 목표 좌표에 미세 오차(±1px) 부여, 클릭 유지 시간 및 더블 클릭 간격에 랜덤 지연 적용.
    - **Reliability**: 클릭 전후의 짧은 대기 시간을 랜덤하게 부여하여 시스템 및 대상 프로그램의 반응 안정성 확보.

### 3.12. 조건부 분기 (Conditional Branching) 고도화 (2025-12-30)

- **Granular Flow Control**: `Pre-Condition`, `Action`, `Post-Condition` 각각에 실패 시 점프할 수 있는 `FailJumpName` 설정을 추가하여 정밀한 예외 처리 지원.

- **Success Path**: 모든 단계 성공 시에만 수행되는 `SuccessJumpName` (Flow Control) 로직 구현.

- **Explicit Options**: `(Next Step)`, `(Ignore & Continue)`, `(Stop Execution)` 등 명시적인 흐름 제어 옵션을 제공하여 엔진 안정성 확보.

- **Engine Upgrade**: 실행 루프를 인덱스 제어 방식으로 전환하여 자유로운 스텝 점프 및 루프 시나리오 지원.



### 3.13. GrayChangeCondition (화면 변화 감지) 추가 (2025-12-30)

- **Logic**: 지정된 영역의 평균 밝기(Gray Scale) 변화량을 측정하여 동작 전후의 상태 변화를 검증.

- **Engine Injection**: `Action` 실행 직전의 스냅샷을 찍어 `ReferenceValue`를 조건 객체에 자동 주입하는 파이프라인 구축 (동적 변화량 측정).

- **UI Enhancement**:

    - `Pick Region` 기능을 통합하여 화면 드래그로 검사 영역 설정 가능.

    - `DelayMs` 속성을 추가하여 액션 후 화면 갱신 대기 시간 보장.



### 3.14. 시스템 설정 및 지속성 (Persistence) (2025-12-30)
- **Settings**: `Setting/appsettings.json` 파일을 통해 애플리케이션 환경 설정 저장.
- **History**: 마지막으로 사용한 레시피 이름을 저장하고, 앱 재시작 시 해당 레시피를 자동으로 선택(Load)하여 사용자 편의성 제공.
- **Structure**: `SettingsManager` 유틸리티를 통한 싱글톤 형태의 설정 관리 체계 구축.

### 3.15. 변수 시스템 및 실행 현황 시각화 (2025-12-30)
- **Runtime Variables**: `MacroEngineService` 내부에 `Dictionary<string, string>` 기반의 변수 저장소 구현.
- **Variable Operations**:
    - `VariableSetAction`: 변수 값 설정, 더하기(Add), 빼기(Sub) 연산 지원.
    - `VariableCompareCondition`: 변수 값 비교(==, !=, >, <, Contains)를 통한 조건부 분기 지원.
- **IdleAction**: 마우스/키보드 제어 없이 지정된 시간만큼 대기하거나, 조건 확인용 스텝 구성을 위한 "아무것도 하지 않는 액션" 추가.
- **Condition Integration**: `ImageMatchCondition` 성공 여부를 지정된 변수에 즉시 저장하는 기능 추가.
- **Dashboard UI Upgrade**:
    - **Progress Tracking**: 현재 실행 중인 스텝 이름, 인덱스, 전체 스텝 수를 상단 패널에 실시간 표시.
    - **Visual Feedback**: `ProgressBar`를 통해 전체 시퀀스의 진행률을 시각화.
    - **Auto Scroll**: 로그 발생 시 최신 로그 위치로 자동 스크롤되는 UX 개선.

### 3.16. 창 제어 및 편집 기능 강화 (2026-01-04)
- **Window Control Action**: 프로세스 이름 또는 윈도우 타이틀을 기반으로 창 최대화/최소화/복원 기능 추가.
- **Teaching Editor UI**:
  - **Localization**: 주요 인터페이스 전면 한글화.
  - **Sequence Management**: 스텝 순서 변경(위/아래) 및 복사/붙여넣기(Copy/Paste) 기능 구현.
  - **Smart Paste**: 복사 시 고유 ID 재생성 로직 적용으로 충돌 방지.
- **Search Logic**: 실행 중인 프로세스 및 윈도우 타이틀 목록 자동 갱신 기능 추가.

### 3.17. 윈도우 상대 좌표 시스템(Window Relative) 고도화 (2026-01-04)
- **Step 0 Context**: 모든 스텝의 실행 전 단계로 '대상 윈도우 설정' 컨텍스트를 추가. 실행 시 자동으로 창을 찾고 설정된 상태(최대화 등)로 변경 후 좌표 계산 수행.
- **Dynamic Scaling**: 티칭 당시의 창 크기와 실행 시점의 창 크기를 비교하여 이미지 검색 ROI 및 마우스 클릭 좌표를 비율(Scale)에 맞춰 자동으로 재계산.
- **Process/Title Search**: 프로세스 이름뿐만 아니라 창 제목(Title)을 통한 대상 윈도우 검색 기능 통합.
- **Fail-safe Jump**: 대상 프로세스나 윈도우를 찾지 못했을 경우 지정된 스텝으로 즉시 점프하는 `ProcessNotFoundJump` 로직 구현.

### 3.18. 티칭 에디터 UX 개선 및 시각화 (2026-01-04)
- **Auto-Capture Reference**: [좌표 픽업] 또는 [영역 선택] 시 대상 윈도우의 크기를 자동으로 감지하여 '기준 해상도'로 저장하고, 절대 좌표를 창 기준 상대 좌표로 자동 변환.
- **Visual Test Feedback**: [Test Match] 실행 시 캡처된 화면 위에 ROI(파란색)와 찾은 위치(빨간색 박스)를 그려서 표시. 마커 크기 또한 현재 창의 스케일에 맞춰 유동적으로 변화하도록 구현.
- **Engine Integration**: 티칭 화면의 개별 스텝 재생(▶) 버튼 클릭 시에도 엔진의 좌표 보정 로직(`ConfigureRelativeCoordinates`)을 거치도록 개선하여 실행 결과의 일관성 확보.

### 3.19. 레시피 계층 구조(Group) 도입 및 UI 고도화 (2026-01-04)
- **SequenceGroup 모델**: 
    - 각 스텝별로 설정하던 '대상 윈도우(Context)' 설정을 그룹 레벨로 격상하여 공통 관리.
    - `CoordinateMode`, `TargetProcessName`, `WindowState` 등을 그룹에서 일괄 설정하여 유지보수성 향상.
- **TreeView 기반 티칭 에디터**: 
    - 좌측 목록을 `TreeView`로 개편하여 [그룹 > 스텝] 계층 구조 시각화.
    - 선택 항목에 따라 그룹 설정 패널과 스텝 상세 설정 패널이 동적으로 전환되는 UX 구현.
- **실행 엔진 평탄화(Flattening)**: 
    - 엔진의 핵심 로직 수정을 최소화하기 위해, 실행 시점에 계층 구조를 단일 리스트로 변환.
    - 그룹 설정을 하위 스텝에 자동 주입하고, 이름을 `그룹명_스텝명`으로 자동 변경하여 로그 가독성 확보.
- **하위 호환성(Migration)**: 레거시 평탄 리스트 형식의 JSON 로드 시 'Default Group'으로 자동 마이그레이션 처리.

### 3.20. 흐름 제어(Jump) 안정화 및 예외 처리 (2026-01-04)
- **ID 기반 점프 시스템**: 
    - 점프 대상을 '이름'이 아닌 '고유 ID(Guid)'로 관리하도록 변경.
    - 스텝 이름을 변경하더라도 흐름 제어 연결이 유지되도록 개선.
- **계층적 점프 타겟 UI**: 
    - 흐름 제어 콤보박스에 `JumpTargetViewModel` 도입.
    - 그룹(📁, 볼드)과 스텝(📄, 들여쓰기)을 구분하여 표시함으로써 직관적인 점프 대상 선택 지원.
- **바인딩 안정화(State Management)**: 
    - `_isLoading` 플래그를 도입하여 데이터 로드 중 콤보박스 리스트가 초기화되는 문제 방지.
    - 노드 간 이동 시 선택된 점프 타겟 값이 소실되지 않도록 목록 갱신 타이밍 최적화.
- **Fail-safe 생성자**: JSON 로드 중 Action 데이터가 누락된 경우 `null` 대신 `IdleAction`으로 자동 복구하여 앱 크래시(ArgumentNullException) 방지.

### 3.21. 매크로 진입점(Entry Point) 시스템 도입 (2026-01-18)
- **Concept**: 매크로 실행 시 수행할 첫 번째 스텝을 명시적으로 지정할 수 있는 전용 인터페이스 도입.
- **START Group**:
    - 레시피 최상단에 고정된 **🏁 START** 노드(`IsStartGroup`)를 강제 생성.
    - 해당 노드는 삭제, 이동, 복제 및 하위 스텝 추가가 불가능하도록 제한하여 매크로 구조의 일관성 확보.
- **Entry Point Logic**:
    - START 그룹 전용 에디터에서 `StartJumpId`를 설정하여 실제 매크로가 시작될 위치를 지정.
    - 실행 엔진(Dashboard) 로드 시, START 그룹을 `Initialize (Start)`라는 더미 액션으로 변환하여 지정된 스텝으로 즉시 점프(Jump)하도록 구현.
- **UI/UX**: 
    - 트리뷰 및 점프 타겟 목록에서 START 지점을 특수 아이콘(🏁)으로 시각화하여 일반 그룹(📁)과 명확히 구분.

### 3.22. 중첩 그룹(Nested Group) 및 계층적 흐름 제어 (2026-01-18)

- **재귀적 데이터 구조 도입**: 
    - `ISequenceTreeNode` 인터페이스를 기반으로 `SequenceGroup` 내부에 또 다른 `SequenceGroup`을 포함할 수 있는 트리 구조 구현.
    - 기존 `Items` 리스트를 `Nodes` 컬렉션으로 전면 교체하여 무한 중첩 지원.
- **그룹 경계 자동화 (Start/End Step)**:
    - 그룹 생성 시 내부 진입점(`Start`)과 탈출점(`End`) 스텝을 자동 생성.
    - 티칭 화면의 트리뷰에서는 해당 스텝들을 숨겨 시각적 복잡도를 낮추고, 그룹 설정 패널에서 논리적으로 제어하도록 개선.
- **그룹 흐름 제어 (Group Flow Control)**:
    - **시작 스텝 (Entry Step)**: 그룹 진입 시 내부의 어떤 스텝부터 실행할지 지정.
    - **종료 후 이동 (Next Group)**: 그룹 실행 완료(`End` 도달) 후 이동할 다음 그룹을 지정.
- **좌표 설정 상속 (ParentRelative Mode)**:
    - `CoordinateMode.ParentRelative` 옵션 추가.
    - 자식 그룹이 부모 그룹의 대상 윈도우 설정(프로세스명, 기준 해상도 등)을 그대로 상속받아 사용함으로써 중첩 구조에서의 티칭 효율성 극대화.
- **실행 엔진 평탄화 (Recursive Flattening)**:
    - 엔진의 선형 실행 구조를 유지하기 위해, 실행 직전 중첩 트리 구조를 재귀적으로 탐색하여 일렬로 변환하는 로직 구현.
- **데이터 안정성 및 마이그레이션**:
    - **Legacy Support**: 구형 `Items` 기반 JSON 로드 시 `Nodes` 구조로 자동 마이그레이션.
    - **ID Sanitization**: 로드 시 중복된 ID(특히 복사된 Start/End 스텝)를 감지하여 새 ID를 발급하고 참조를 자동 갱신.
    - **Smart Paste**: 스텝 복사/붙여넣기 시 점프 대상이 유효한 범위(현재 그룹 내)에 있는지 검증하고 자동 보정.

### 3.23. 엄격한 그룹 흐름 제어 (Strict Same Depth) 및 좌표 픽업 보정 (2026-01-19)
- **Same Depth Filtering**:
    - 그룹의 독립성을 보장하기 위해, 점프 대상(Jump Targets) 선택 시 **현재 스텝과 동일한 부모를 가진 형제(Sibling) 노드들**만 목록에 표시하도록 로직 수정.
    - 이제 그룹 내부의 스텝들은 외부 그룹의 내부 스텝으로 직접 점프할 수 없으며, 반드시 그룹의 `Start/End`를 통해 상위 레벨로 흐름을 제어해야 함.
    - 목록에 `(Finish Group)`을 명시적으로 추가하여 그룹 탈출 로직의 직관성 확보.
- **Coordinate Picking Fix**:
    - 중첩된 `ParentRelative` 그룹 내부에서 좌표/영역 픽업 시, 직계 부모가 아닌 **최상위 기준 윈도우**를 재귀적으로 추적(`GetEffectiveGroupContext`)하여 상대 좌표를 계산하도록 수정.
    - 이를 통해 티칭 시 저장된 좌표와 실제 실행 시 계산되는 좌표의 오차를 제거.

### 3.24. 그룹별 좌표 변수(Coordinate Variables) 시스템 도입 (2026-01-24)
- **Coordinate Variable Type**:
    - 단순 문자열이 아닌 X, Y 좌표 쌍을 가지는 전용 데이터 타입(`CoordinateVariable`) 신설.
    - 각 그룹(`SequenceGroup`) 내부에 고유한 좌표 변수 목록을 저장할 수 있도록 확장.
- **Scope-based Inheritance**:
    - 상위 그룹에서 정의된 좌표 변수는 하위 그룹의 스텝들이 상속받아 사용할 수 있는 계층적 스코프(Scope) 구조 구현.
    - 변수명 충돌 시 하위 그룹(가장 가까운 그룹)의 변수가 우선권을 가짐.
- **Hybrid Mouse Source UI**:
    - `MouseClickAction`의 좌표 소스를 3가지 모드로 확장:
        1. **지정 좌표 (Fixed)**: 기존 방식 (직접 입력 또는 현재 위치 픽업).
        2. **찾은 좌표 (Found)**: 이전 이미지 매칭 결과 좌표 사용.
        3. **변수 좌표 (Variable)**: 그룹 스코프에 정의된 좌표 변수 참조.
- **Enhanced Teaching Editor**:
    - 그룹 설정 패널 내 변수 관리 UI(DataGrid) 및 스크롤 처리 추가.
    - 변수 설정 시에도 **[📍 위치]** 버튼을 통해 화면 클릭으로 좌표를 즉시 획득 가능.
    - 중첩된 `ParentRelative` 그룹에서도 최상위 윈도우 컨텍스트를 추적하여 정확한 상대 좌표를 계산하도록 보정 로직(`ResolveGroupContext`) 적용.
- **Execution Engine Upgrade**:
    - 엔진 평탄화(`Flattening`) 시점에 그룹별 변수 스코프를 해석하여 액션에 동적 주입(`RuntimeContextVariables`).
    - 티칭 에디터의 단일 스텝 실행(`RunSingleStep`) 시에도 상위 계층 변수를 수집하여 주입하는 로직을 추가하여 실행 결과의 일관성 확보.

### 3.25. 프로세스 감지 및 변수 지속성 강화 (2026-01-24)
- 2026-01-24 : 프로세스 실행 여부 확인 조건 (`ProcessRunningCondition`) 추가. (특정 프로그램의 실행/종료 상태에 따른 분기 지원)
- 2026-01-24 : 런타임 변수 변경 시 파일(`.vars.json`) 즉시 저장 로직 구현. (`VariableSetAction`, `ImageMatchCondition` 결과값 영구 보존)
- 2026-01-24 : 그룹 스코프 정수 변수(`GroupIntVariable`) 추가 및 계층적 스코프 연동. (전역 변수와 통합 관리)
- 2026-01-24 : 티칭 에디터 트리뷰 빈 공간 클릭 시 선택 해제 기능 추가. (최상위 레벨 그룹 생성 편의성 확보)
- 2026-01-24 : 그룹 설정에 '완료 확인 및 흐름 제어(Post-Condition)' UI 추가 및 엔진 로직 반영. (그룹 종료 시점에 조건 검사 및 `Switch Case` 분기 지원)

### 3.26. 프로세스 감지 고도화 및 그룹 이동 버그 수정 (2026-01-25)
- **ProcessRunningCondition 기능 확장**:
    - **검색 기준 선택**: 프로세스 이름 뿐만 아니라 윈도우 타이틀을 기준으로 실행 여부를 감지할 수 있도록 `SearchMethod` 속성 추가 및 `CheckAsync` 로직 반영.
    - **재시도 로직 도입**: `MaxSearchCount` 및 `SearchIntervalMs` 속성을 추가하여 조건 충족 시까지 반복 대기 및 확인 기능 구현.
    - **UI 통합**: 사전/사후/그룹종료/공통 조건 UI에 검색 기준 및 재시도 설정 항목 일체 추가.
- **안정성 및 버그 수정**:
    - **Nested Group 이동 오류 수정**: 중첩 그룹 이동(Move Up/Down) 시 최상위 컬렉션에서 인덱스를 찾아 발생하던 `ArgumentOutOfRangeException` 해결 (`FindParentGroup`을 통한 부모 노드 내 이동 로직 적용).
    - **XAML Resource Error 해결**: 특정 조건 선택 시 `EnumToBooleanConverter`를 찾지 못하던 `XamlParseException` 해결을 위해 로컬 리소스 등록 보강.
    - **Refresh Command 개선**: `RefreshContextTargetCommand`가 개별 조건 객체의 검색 기준 설정을 인식하여 목록을 갱신하도록 범용성 확보.

### 3.27. 실행 엔진 안정성 강화 및 디버깅 고도화 (2026-01-25)
- **Deadlock Fix**: `ImageMatchCondition` 및 `GrayChangeCondition` 실행 시 불필요한 UI 스레드 동기화(`Dispatcher.Invoke`)를 제거하여, 루프 실행 중 발생하던 멈춤 현상(데드락) 해결. `ScreenCaptureHelper`를 백그라운드 스레드에서 직접 호출하고 `Freeze()`로 안전성 확보.
- **Debug Logging**: 
    - `MacroEngineService`의 좌표 설정 단계(`ConfigureRelativeCoordinates`)에 타겟 윈도우 감지 로그(`TargetProcessName`, `hWnd`) 추가.
    - `ImageMatchCondition`에 ROI 영역 및 스케일(`ScaleX/Y`) 계산 로그 추가.
    - 이를 통해 다단계 중첩 그룹(`ParentRelative`) 구조에서의 좌표계 상속 및 타겟팅 문제를 정밀하게 추적 가능하도록 개선.
- **Log Persistence**: 
    - 프로그램 실행 시 `Logs/Log_yyyyMMdd_HHmmss.txt` 파일을 자동 생성.
    - 대시보드의 모든 실행 로그를 파일로 실시간 저장하는 기능을 구현하여 사후 분석 지원.
- **Code Clean-up**: `UriToBitmapConverter`, `ScreenCaptureHelper`, `TeachingView` 등에서 발생하던 주요 Nullable 관련 컴파일 경고 해결 및 예외 처리 강화.

### 3.28. 이미지 매칭 정확도 개선 및 자동 ROI 도입 (2026-01-25)
- **Auto ROI (Window-Based)**: 
    - `WindowRelative` 모드에서 영역(`UseRegion`) 미지정 시, 전체 화면 대신 타겟 윈도우 영역만 검색하도록 자동 ROI 로직 구현.
    - `ImageMatchCondition`에 `SetContextSize` 메서드 추가 및 실행 엔진(`MacroEngineService`)을 통한 윈도우 크기 동적 주입.
    - 이를 통해 듀얼 모니터 환경이나 창 크기 변화 시에도 티칭 당시의 의도를 정확히 반영하도록 매칭 성공률 향상.
- **Enhanced Debugging**:
    - 매칭 실패 시 최고 점수(`Best Score`) 및 임계값(`Threshold`) 정보를 로그로 출력하여 원인 분석 지원.
    - 평탄화(`Flattening`) 과정에서 그룹별 기준 해상도(`RefWindowWidth/Height`) 상속 경로를 추적하는 로그 추가.
- **Error Visualization**: 매칭 최종 실패 시 `Logs/DebugImages` 폴더에 ROI가 표시된 실패 화면(`Fail_*.png`)을 자동 저장하는 기능 추가.

### 3.29. 티칭 에디터 단일 실행 로직 정교화 (2026-01-25)
- **Pre-Condition Failure Handling**: 티칭 화면에서 개별 스텝 실행 시, 시작 조건(Pre-Condition)이 실패했음에도 액션이 실행되던 로직을 수정.
- **Stop on Failure**: 조건 실패 시 로그를 출력하고 즉시 실행을 중단하도록 변경하여 실제 매크로 실행 엔진의 로직과 일관성 확보.

### 3.31. 그룹 설정 UI 바인딩 수정 (2026-01-26)
- **Group Post-Condition SwitchCase Fix**:
    - 그룹 설정의 '완료 확인 및 조건부 분기'에서 `Switch Case` 사용 시, 점프 대상 목록(`ComboBox`)이 스텝 레벨(`JumpTargets`)로 잘못 표시되던 문제 수정.
    - 해당 섹션 전용 `DataTemplate`을 정의하고 `AvailableGroupExitTargets`로 명시적 바인딩하여, 그룹 흐름 제어와 동일한 뎁스(Sibling Groups)의 점프 대상만 표시되도록 개선.
- **VariableSetAction Binding Fix**:
    - 변수 설정 액션(`VariableSetAction`)에서 변수명 입력 시 값이 저장되지 않거나 초기화되는 문제 해결.
    - `ComboBox`의 `SelectedValuePath="."` 속성이 사용자 입력(Custom Text) 바인딩과 충돌하는 것을 확인하여 제거하고, `Text` 바인딩에 `Mode=TwoWay`를 명시적으로 적용.

### 3.32. 그룹 설정 데이터 유실 방지 및 바인딩 보호 (2026-01-27)
- **현상**: 그룹 설정의 '완료 확인 및 조건부 분기' 내에서 설정한 점프 ID(Switch Case 이동, 실패 시 이동)가 다른 그룹을 클릭한 후 저장하면 삭제되는 현상 발생.
- **원인 분석**: 
    - 해당 영역의 ComboBox들이 모델 객체(`SwitchCaseItem`, `IMacroCondition`)에 직접 Two-Way 바인딩되어 있었음.
    - 그룹 이동 시 점프 타겟 리스트(`AvailableGroupExitTargets`)가 새 그룹 기준으로 교체되는데, 이때 화면에 남아있던 이전 그룹의 ComboBox가 "현재 값이 목록에 없음"을 감지하고 모델의 값을 `null`로 덮어씌움.
- **해결 방안 (Proxy & Protection)**:
    - **`SwitchCaseItemViewModel` 도입**: 모델 아이템을 래핑하고, 점프 타겟 리스트가 갱신 중(`_isUpdatingGroupTargets`)일 때는 UI로부터의 값 변경 요청을 무시하는 보호 로직 추가.
    - **`SelectedGroupPostConditionFailJumpId` 프록시 속성**: 그룹 레벨의 실패 시 이동(`FailJumpId`) 전용 보호 속성을 `TeachingViewModel`에 추가.
    - **UI 바인딩 교체**: `TeachingView.xaml`의 모든 그룹 포스트 컨디션 템플릿에서 모델 직접 바인딩을 이 보호된 프록시 속성들로 전면 교체.
- **결과**: 그룹 간 전환 시에도 데이터가 원천적으로 보호되며, 불필요한 렉(Lag) 없이 안정적으로 데이터가 유지됨.

### 3.33. 티칭 UI 컴포넌트 표준화 및 바인딩 안정화 (2026-01-28)
- **전용 선택 컨트롤 도입**:
    - **`JumpTargetSelector`**: 점프 대상(Guid Id) 선택 전용 컨트롤. 목록 갱신(`Clear/Add`) 시 WPF가 선택값을 `null`로 밀어내는 현상을 컨트롤 내부에서 차단(보호 로직)하고, 그룹/스텝별 아이콘 스타일을 내장함.
    - **`VariableSelector`**: 변수명 및 프로세스 이름 입력 전용 컨트롤. `IsEditable="True"`를 기본으로 하며, 텍스트 입력 시 즉시 뷰모델에 반영되도록 `UpdateSourceTrigger=PropertyChanged` 바인딩을 최적화함.
- **바인딩 버그 수정 및 안정화**:
    - **Switch Case 데이터 유실 해결**: 스텝 에디터의 Switch Case가 모델을 직접 바인딩하던 구조에서 `StepPre/PostSwitchCases` Proxy 컬렉션을 사용하도록 개선하여 데이터 유실 방지.
    - **변수명 초기화 오류 수정**: `UpdateGroupProxyProperties`에서 변수명을 강제로 `string.Empty`로 리셋하던 잔재 코드를 제거하여, `VariableSelector`와 바인딩 충돌로 데이터가 날아가던 문제 해결.
    - **명시적 동기화**: `Add/RemoveSwitchCaseCommand` 실행 시 Proxy 컬렉션(`SyncStepSwitchCases`)을 명시적으로 호출하여 UI가 즉시 갱신되도록 보완.
- **코드 정제**: `TeachingView.xaml.cs`에 흩어져 있던 임시 보호용 이벤트 핸들러들을 제거하고, XAML 코드를 커스텀 컨트롤 기반으로 단순화하여 유지보수성 향상.

### 3.34. 일시정지(Pause) 기능 구현 (2026-01-28)
- **Engine Logic**: `IsPaused` 상태와 루프 내부 대기 로직을 추가하여, 실행 중 즉시 멈춤 및 재개 가능하도록 구현.
- **UI/UX**: 
    - 대시보드에 **[⏸ 일시정지]** 버튼 추가.
    - [▶ 실행] 버튼을 일시정지 시 **[▶ 재개]** (황금색) 스타일로 동적 변경.
    - `RunCommand` 수정: 일시정지 상태에서 실행 시 처음부터가 아닌 멈춘 지점부터 재개하도록 로직 변경.
- **Hotkey**: **F7** 키를 일시정지/재개 단축키로 추가 등록. (F5: 시작/재개, F6: 중지)

### 3.35. 데이터 무결성 및 바인딩 안정화 (Critical Fixes) (2026-01-28)
- **현상**: 스텝을 선택했다가 다시 그룹을 선택하거나, 특정 UI 조작 시 변수명 등 설정 데이터가 사라지는 문제 지속.
- **원인 분석**: 
    - `VariableSelector` 내부의 `ComboBox`가 `ItemsSource` 갱신(Clear/Add) 시 `Text` 속성을 자동으로 초기화(`""`)하며 바인딩된 모델 데이터를 삭제.
    - 뷰모델에서 불필요하게 리스트를 `Clear()` 하고 다시 채우는 로직이 콤보박스의 초기화를 유발.
    - 스텝에서 그룹으로 돌아올 때(`SelectedSequence = null`), 그룹이 변경되지 않았으면 프록시 속성 갱신을 건너뛰는 최적화 오류.
- **해결 방안**:
    1.  **Component Protection**: `VariableSelector`와 `JumpTargetSelector`에서 바인딩을 끊고, **시스템에 의한 `null` 또는 빈 값 변경을 감지하여 원본 데이터를 복구**하는 수동 동기화 로직 적용.
    2.  **Smart List Update**: `AvailableIntVariables` 등 목록 갱신 시 `SequenceEqual`로 변경 사항이 있을 때만 업데이트하도록 수정하여 불필요한 초기화 방지.
    3.  **Force Refresh**: 스텝 선택 해제 시 그룹 프록시 속성을 강제로 동기화(`UpdateGroupProxyProperties`)하도록 로직 보완.
- **결과**: UI 이동이나 목록 갱신 시 데이터가 절대 유실되지 않는 강력한 안정성 확보.

### 3.36. 그룹 점프 바인딩 안정화 및 UI 타이밍 이슈 해결 (2026-02-01)
- **현상**: 동일 레벨(Depth)의 그룹 간 전환 시 '종료 후 이동' 등 점프 대상 콤보박스의 값이 UI에 표시되지 않는 문제 발생.
- **원인 분석**:
  1. **Data Detachment**: 그룹 전환 시 이전 그룹의 설정 ID가 새 목록에 포함되지 않아, WPF 바인딩 시스템이 값을 `null`로 강제 초기화.
  2. **WPF Binding Race Condition**: 커스텀 컨트롤(`JumpTargetSelector`)의 `ItemsSource`가 바뀔 때, 내부 `ComboBox`의 실제 바인딩 갱신보다 선택값 동기화(`SynchronizeSelection`)가 먼저 실행되어 값을 찾지 못함.
- **해결 방안**:
  1. **ID Persistence**: `UpdateGroupJumpTargets` 로직을 수정하여 전환 직전 그룹(`oldGroup`)의 점프 ID를 강제로 목록에 포함시켜 바인딩 유실 방지.
  2. **Deferred Synchronization**: `JumpTargetSelector.xaml.cs`에서 `OnItemsSourceChanged` 시점에 `Dispatcher.InvokeAsync`를 사용하여, 내부 바인딩 갱신이 완료된 후 선택값을 동기화하도록 개선.
  3. **Binding Error Cleanup**: `SequenceGroup` 모델에 `IsGroupStart/End` 더미 속성을 추가하여 트리뷰 스타일에서 발생하던 `Path Error` 로그 제거.

## 4. 최종 빌드 상태
- **결과**: `dotnet build` 성공 (Exit Code: 0)
- **주요 해결 사항**: 
    - 프로세스 점유로 인한 빌드 실패(`Macro.exe` 파일 잠금) 해결.
    - 변수 시스템 도입으로 복합 로직 구현 가능성 확보.



## 5. 향후 계획

- **조건부 분기(Branching)**: 변수 비교 및 복합 조건 지원.

- **스크립트 지원**: C# 스크립트 엔진(Roslyn) 연동 검토.

- **UI 개선**: 다크 모드 및 실행 로그 내 변화량 수치 시각화.