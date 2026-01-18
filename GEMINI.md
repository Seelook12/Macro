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
--- 
- `SGMachine_Rivet` 프로젝트 관련 정보 (생략)
- `Riveting` 프로젝트 Viewbox 관련 요청 (생략)