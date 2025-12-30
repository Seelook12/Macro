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

## 4. 최종 빌드 상태
- **결과**: `dotnet build` 성공 (Exit Code: 0)
- **경고**: NU1701 (ReactiveUI 버전 호환성) 및 일부 미사용 변수 경고 존재하나 실행에 지장 없음.

## 5. 향후 계획
- **실행 엔진 고도화**: 조건 실패 시 재시도(Retry) 및 루프 기능 추가.
- **Global Hotkey**: 프로그램이 최소화된 상태에서도 실행/중지가 가능한 전역 단축키 구현.
- **멀티 모니터 대응**: `SystemParameters`를 활용한 다중 모니터 좌표 보정.