## Gemini Added Memories
- 사용자는 한국인이며 한국어 응답을 선호함.
- 코드 변경 사항은 주석보다 응답 텍스트로 설명하는 것을 선호함.
- 사용자는 C# 개발자이며 MVVM 패턴(특히 ReactiveUI)을 선호함.
- **WPF 프로젝트 규칙**:
  1. 인덴트: 4 spaces.
  2. 중괄호: Allman style.
  3. **네임스페이스 충돌 주의**: `UseWindowsForms`가 활성화되어 있으므로 아래 클래스 사용 시 반드시 명시적 네임스페이스 또는 별칭(alias)을 사용할 것.
     - `Application` -> `System.Windows.Application`
     - `MessageBox` -> `System.Windows.MessageBox` (WPF용 사용 시)
     - `UserControl` -> `using UserControl = System.Windows.Controls.UserControl;`
     - `Point` -> `using Point = System.Windows.Point;`
     - `KeyEventArgs`, `MouseEventArgs` -> `System.Windows.Input` 소속임을 명시.
  4. **이미지 처리**: `Image` 컨트롤 바인딩 시 파일 잠금 방지를 위해 `UriToBitmapConverter`를 반드시 사용할 것.
  5. **OpenCV**: `Mat` 객체 사용 시 반드시 `using` 문으로 메모리 해제 처리할 것.
  6. **View 패턴**: `ReactiveUserControl`보다 `UserControl` + `IViewFor<T>` + `WhenActivated` 명시적 바인딩 패턴이 이 프로젝트에서 더 안정적임.
- **이미지 매칭**: `OpenCvSharp4`를 사용하며, 템플릿 이미지는 레시피 폴더에 타임스탬프와 함께 복사하여 상대적으로 관리함.
- **레시피 계층 구조**:
  1. `SequenceGroup`은 하위 `SequenceItem`들을 관리하며, 윈도우 설정(TargetProcessName 등)을 공통으로 소유함.
  2. 실행 엔진(`MacroEngineService`)은 평탄화된 리스트를 요구하므로, 실행 시점에 설정을 주입하고 변환(`DashboardViewModel`)해야 함.
  3. 흐름 제어(Jump) 시 '이름'이 아닌 `SuccessJumpId` 등 Guid 기반의 ID를 우선 사용하여 바인딩의 안정성을 확보할 것.
  4. UI 갱신 시 `JumpTargets` 컬렉션이 `Clear()` 되면 ComboBox 바인딩이 끊길 수 있으므로, 대량 로드(`LoadData`) 시에는 플래그를 사용하여 갱신을 제어해야 함.
  5. **매크로 진입점**: 레시피 최상단에는 항상 `IsStartGroup`이 활성화된 **🏁 START** 그룹이 존재해야 하며, 이는 삭제나 이동이 불가능한 고정 진입점 역할을 함. 실제 실행 시작 위치는 이 그룹의 `StartJumpId`를 통해 결정됨.
  6. **중첩 그룹 구조**: `SequenceGroup`은 `Nodes` 컬렉션을 통해 하위 그룹과 스텝을 모두 소유할 수 있음. 모든 데이터 처리는 이 재귀적 트리 구조를 기반으로 수행함.
  7. **그룹 경계 및 흐름**: 
     - 각 그룹은 내부적으로 숨겨진 `Start`와 `End` 스텝을 가짐. 
     - 그룹 간 이동은 반드시 `Next Group` 설정을 통해 이루어지며, 이는 내부 `End` 스텝의 점프 ID로 자동 관리됨.
     - 그룹 내부 스텝에서 `(Finish Group)`을 선택하면 해당 그룹의 종료 로직으로 연결됨.
  8. **좌표 상속**: 중첩된 자식 그룹에서 `ParentRelative` 모드를 사용하면 부모 그룹의 윈도우 컨텍스트를 자동으로 상속받음.
  9. **좌표 변수(Coordinate Variables)**: 
     - 각 그룹은 고유한 좌표 변수(`Name`, `X`, `Y`)를 소유할 수 있음.
     - 하위 그룹/스텝은 상위 그룹에 정의된 좌표 변수를 상속받아 사용할 수 있음 (Scope-based).
     - `MouseClickAction`에서 `SourceType.Variable`을 통해 이 변수들을 참조하며, 실행 시점에 계층 구조를 따라 해석되어 주입됨.
  10. **2026-01-24 : 런타임 변수 지속성**: `VariableSetAction` 등에 의해 변경된 값은 즉시 `.vars.json` 파일에 저장되어 앱 재시작 후에도 유지됨.
  11. **2026-01-24 : 그룹 종료 조건**: 그룹 설정의 `PostCondition`은 그룹의 마지막 스텝(`End`) 실행 직후 체크되며, `Switch Case` 등을 통해 유연한 그룹 간 점프를 지원함.
  12. **2026-01-25 : 프로세스 확인 조건 고도화**: `ProcessRunningCondition`은 프로세스 이름 또는 윈도우 타이틀을 기준으로 실행 여부를 감지하며, 자체적인 재시도(`MaxSearchCount`) 로직을 포함함.
  13. **2026-01-25 : 그룹 이동 로직**: 그룹을 위/아래로 이동할 때는 반드시 `FindParentGroup`을 통해 부모가 중첩 그룹인지 확인한 후, 부모의 `Nodes` 또는 최상위 `Groups` 중 적절한 컬렉션에서 `Move`를 수행해야 함.
  14. **2026-01-25 : RecipeCompiler 아키텍처 (중요)**
     - **역할**: 계층적인 그룹 트리를 엔진이 실행 가능한 평탄화된 시퀀스로 변환하는 전용 서비스.
     - **규칙 1 (Centralization)**: 모든 실행 준비 로직(평탄화, 좌표 변환, 변수 주입)은 반드시 `RecipeCompiler`를 통해야 함. 뷰모델에서 자체적으로 변환 로직을 구현하지 말 것.
     - **규칙 2 (Consistency)**: 전체 실행(`Compile`)과 단일 스텝 테스트(`CompileSingleStep`)는 동일한 컴파일러 로직을 공유하여 실행 결과의 일관성을 보장해야 함.
     - `CoordinateMode`, `TargetProcessName` 등의 컨텍스트는 부모 그룹에서 자식으로 명시적으로 상속되며, 변수 스코프는 계층 구조를 따라 병합(Override)됨.
  15. **2026-01-28 : UI 컴포넌트 표준화 규칙**
     - **점프 대상 선택**: `ComboBox`를 직접 사용하지 말고 반드시 `JumpTargetSelector`를 사용할 것. (목록 갱신 시 데이터 유실 방지 로직 내장)
     - **변수 및 프로세스 입력**: `IsEditable`이 필요한 경우 `VariableSelector`를 사용할 것. (입력 즉시 반영 및 텍스트 유실 방지 로직 내장)
     - **Proxy 바인딩 패턴**: 트리뷰 노드 전환 시 데이터 유실을 막기 위해, `SwitchCaseItem` 등 컬렉션 내부 아이템은 반드시 ViewModel 래퍼(Proxy)를 거쳐 바인딩할 것.
  16. **2026-01-28 : 데이터 무결성 규칙 (Smart Update)**
     - 콤보박스와 바인딩된 컬렉션(`ObservableCollection`)은 무조건 `Clear()`/`Add()` 하지 말고, `SequenceEqual` 등으로 **변경 사항이 있을 때만 갱신**해야 함. (불필요한 UI 리셋 방지)
     - 커스텀 컨트롤(`VariableSelector` 등)은 `ItemsSource` 변경 시 시스템이 자동으로 `null/empty`로 초기화하는 것을 방어하는 로직(`_isInternalChange`, 복구 로직)을 반드시 포함해야 함.
  17. **일시정지(Pause) 구현**: 엔진 루프 내부에 `while(IsPaused) await Task.Delay` 패턴을 사용하여 즉각적인 반응성을 확보하고, UI에서는 [재개] 버튼으로 상태를 명확히 표시함.
  18. **2026-02-01 : 그룹 컨텍스트 바인딩 보호**: `VariableSelector`를 사용할 때 `ItemsSource`를 단순 갱신(`Clear/Add`)하면 내부 보호 로직이 작동하지 않으므로, 반드시 **새 컬렉션 인스턴스를 할당(`new ObservableCollection`)**하여 보호 로직을 트리거해야 함.
  19. **2026-02-01 : 인코딩 안전성**: UI 표시용 문자열(점프 대상 등)에 이모지를 사용하면 인코딩(CP949/UTF-8) 문제로 깨질 수 있으므로, 반드시 `[Step]`, `[Group]` 등의 텍스트 라벨을 사용할 것.
  20. **코드 포맷팅 엄수**: 불필요한 줄바꿈(Blank lines)을 과도하게 생성하지 말 것. 메서드 내에서는 논리적 블록 사이에 1줄만 띄우고, 중괄호(`{}`) 시작 전후에 불필요한 공백을 두지 않는다. 특히 들여쓰기가 깊어질 때 탭/공백이 증식되지 않도록 주의한다.
--- 
- `SGMachine_Rivet` 프로젝트 관련 정보 (생략)
- `Riveting` 프로젝트 Viewbox 관련 요청 (생략)