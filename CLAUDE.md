# Macro 프로젝트 규칙

## 기본
- 한국어로 응답할 것.
- 코드 변경 사항은 주석보다 응답 텍스트로 설명할 것.
- C# WPF 프로젝트, MVVM 패턴(ReactiveUI) 사용.

## 코드 스타일
- 인덴트: 4 spaces.
- 중괄호: Allman style.
- 불필요한 줄바꿈 금지. 메서드 내 논리적 블록 사이 1줄만. 중괄호 전후 불필요한 공백 금지.

## 네임스페이스 충돌 주의
`UseWindowsForms`가 활성화되어 있으므로 아래 클래스 사용 시 반드시 명시적 네임스페이스 또는 별칭 사용:
- `Application` -> `System.Windows.Application`
- `MessageBox` -> `System.Windows.MessageBox`
- `UserControl` -> `using UserControl = System.Windows.Controls.UserControl;`
- `Point` -> `using Point = System.Windows.Point;`
- `KeyEventArgs`, `MouseEventArgs` -> `System.Windows.Input` 소속 명시

## View 패턴
- `ReactiveUserControl`보다 `UserControl` + `IViewFor<T>` + `WhenActivated` 명시적 바인딩 패턴 사용.

## 이미지/OpenCV
- `Image` 컨트롤 바인딩 시 파일 잠금 방지를 위해 `UriToBitmapConverter` 필수.
- `OpenCvSharp4` 사용. `Mat` 객체는 반드시 `using` 문으로 메모리 해제.
- 템플릿 이미지는 레시피 폴더에 타임스탬프와 함께 복사하여 상대 관리.

## UI 컴포넌트 표준화
- 점프 대상 선택: `JumpTargetSelector` 사용 (ComboBox 직접 사용 금지).
- 변수/프로세스 입력(IsEditable 필요 시): `VariableSelector` 사용.
- 수치 입력(Delay, RepeatCount 등): `DynamicIntInput` 사용 ([123]=상수, [{x}]=변수).
- 트리뷰 노드 전환 시 `SwitchCaseItem` 등은 ViewModel 래퍼(Proxy) 거쳐 바인딩.
- `VariableSelector`의 `ItemsSource`는 새 컬렉션 인스턴스 할당으로 보호 로직 트리거.

## 데이터 무결성 (Smart Update)
- 컬렉션 갱신 시 `SequenceEqual` 등으로 변경 사항 있을 때만 갱신.
- 커스텀 컨트롤은 `ItemsSource` 변경 시 자동 null/empty 초기화 방어 로직 포함.

## 인코딩 안전성
- UI 표시용 문자열에 이모지 사용 금지 -> `[Step]`, `[Group]` 등 텍스트 라벨 사용.

## 시스템 내부 변수
- `__` (언더바 2개)로 시작하는 변수명은 시스템 내부 런타임용 (메모리상에만 존재, `.vars.json` 미저장).

## 레시피 계층 구조
- `SequenceGroup`은 `Nodes` 컬렉션으로 하위 그룹과 스텝 모두 소유 (재귀적 트리 구조).
- `SequenceGroup`은 하위 `SequenceItem` 관리, 윈도우 설정(TargetProcessName 등) 공통 소유.
- 흐름 제어(Jump)는 이름이 아닌 `SuccessJumpId` 등 Guid 기반 ID 우선.
- `JumpTargets` 컬렉션 `Clear()` 시 ComboBox 바인딩 끊김 주의 -> 대량 로드 시 플래그로 갱신 제어.

## 매크로 진입점
- 레시피 최상단에 `IsStartGroup` 활성화된 START 그룹 존재 (삭제/이동 불가 고정 진입점).
- 실행 시작 위치는 `StartJumpId`로 결정.

## 그룹 경계 및 흐름
- 각 그룹은 숨겨진 `Start`와 `End` 스텝 보유.
- 그룹 간 이동은 `Next Group` 설정 (내부 End 스텝의 점프 ID로 자동 관리).
- `(Finish Group)` 선택 시 해당 그룹 종료 로직으로 연결.

## 좌표
- 중첩 자식 그룹에서 `ParentRelative` 모드 사용 시 부모 윈도우 컨텍스트 자동 상속.
- 각 그룹 고유 좌표 변수(`Name`, `X`, `Y`) 소유 가능. 하위에서 상속 (Scope-based).
- `MouseClickAction`에서 `SourceType.Variable`로 참조, 실행 시점에 계층 따라 해석/주입.

## RecipeCompiler 아키텍처
- **역할**: 계층적 그룹 트리를 평탄화된 실행 시퀀스로 변환하는 전용 서비스.
- **규칙 1 (Centralization)**: 모든 실행 준비 로직은 반드시 `RecipeCompiler` 경유. 뷰모델 자체 변환 금지.
- **규칙 2 (Consistency)**: 전체 실행(`Compile`)과 단일 스텝 테스트(`CompileSingleStep`)는 동일 컴파일러 로직 공유.
- 컨텍스트(`CoordinateMode`, `TargetProcessName` 등)는 부모에서 자식으로 명시적 상속, 변수 스코프는 계층 따라 병합(Override).
- **타임아웃**: Decorator Pattern (엔진 수정 없이 컴파일 타임에 주입).
  - `Start` 스텝에 `CurrentTimeAction` 주입 -> `__GroupStart_{ID}` 변수에 기록.
  - 모든 스텝 조건을 `TimeoutCheckCondition`으로 래핑.
  - 중첩 그룹은 상위 타임아웃 조건 누적 (Chain Check).

## 기타 엔진 동작
- `VariableSetAction` 등에 의해 변경된 값은 즉시 `.vars.json`에 저장 (앱 재시작 후 유지).
- `PostCondition`은 그룹 마지막 스텝(`End`) 직후 체크, `Switch Case` 등으로 유연한 점프.
- `ProcessRunningCondition`: 프로세스 이름/윈도우 타이틀 기준, 자체 재시도(`MaxSearchCount`).
- 그룹 위/아래 이동 시 `FindParentGroup`으로 부모 확인 후 적절한 컬렉션에서 `Move`.
- 일시정지: 엔진 루프 내 `while(IsPaused) await Task.Delay` 패턴.
- `VariableCompareCondition`: '조건 만족 시 이동(Jump on True)' 방식. 엔진은 항상 `true` 반환, 조건 만족 시 `GetForceJumpId`로 분기.
- 프로세스 이름/윈도우 타이틀에 변수 사용 가능, 실행 시점에 해석.
