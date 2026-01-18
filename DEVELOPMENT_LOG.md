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
- **Concept**: 매크로 시작 시 수행할 첫 번째 스텝을 명시적으로 지정할 수 있는 전용 인터페이스 도입.
- **START Group**:
    - 레시피 최상단에 고정된 **🏁 START** 노드(`IsStartGroup`)를 강제 생성.
    - 해당 노드는 삭제, 이동, 복제 및 하위 스텝 추가가 불가능하도록 제한하여 매크로 구조의 일관성 확보.
- **Entry Point Logic**:
    - START 그룹 전용 에디터에서 `StartJumpId`를 설정하여 실제 매크로가 시작될 위치를 지정.
    - 실행 엔진(Dashboard) 로드 시, START 그룹을 `Initialize (Start)`라는 더미 액션으로 변환하여 지정된 스텝으로 즉시 점프(Jump)하도록 구현.
- **UI/UX**: 
    - 트리뷰 및 점프 타겟 목록에서 START 지점을 특수 아이콘(🏁)으로 시각화하여 일반 그룹(📁)과 명확히 구분.

## 4. 최종 빌드 상태
- **결과**: `dotnet build` 성공 (Exit Code: 0)
- **주요 해결 사항**: 
    - 프로세스 점유로 인한 빌드 실패(`Macro.exe` 파일 잠금) 해결.
    - 변수 시스템 도입으로 복합 로직 구현 가능성 확보.



## 5. 향후 계획

- **조건부 분기(Branching)**: 변수 비교 및 복합 조건 지원.

- **스크립트 지원**: C# 스크립트 엔진(Roslyn) 연동 검토.

- **UI 개선**: 다크 모드 및 실행 로그 내 변화량 수치 시각화.