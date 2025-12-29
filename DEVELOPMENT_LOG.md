# 개발 진행 이력 (Development Log)

**작성일**: 2025년 12월 29일
**프로젝트**: Macro Automation (WPF + ReactiveUI)

## 1. 프로젝트 개요
- **목표**: 업무 자동화 프로그램의 기본 골격(Skeleton) 구현
- **기술 스택**:
  - Language: C# (.NET 9.0)
  - Framework: WPF
  - Library: ReactiveUI.WPF (v22.3.1)
  - Pattern: MVVM (Model-View-ViewModel)

## 2. 주요 제약 사항 및 구현 방식
- **DI Container 미사용**: 별도의 DI 라이브러리 없이 의존성을 직접 관리(Pure DI).
- **ViewLocator 직접 구현**: `Splat`의 자동 주입에 의존하되, 필요 시 수동으로 뷰를 찾아주는 로직 포함.
- **참조 프로젝트 스타일 준수**: `SG_Aurora_E_Pjt_RIVETING` 프로젝트의 구조와 패턴을 학습하여 적용.

## 3. 구현 내용 상세

### 3.1. 네비게이션 구조 (Navigation)
- **MainWindowViewModel (Shell)**
  - `IScreen`을 구현하여 라우팅 상태(`RoutingState`) 관리.
  - 대시보드(실행), 레시피(관리), 티칭(편집) 3가지 모드 간 전환 구현.
- **View 구조**
  - 모든 View는 `ReactiveUserControl<TViewModel>`을 상속.
  - `RoutedViewHost`를 사용하여 ViewModel 상태에 따라 중앙 화면이 전환됨.

### 3.2. ViewLocator 구현 (핵심 트러블슈팅)
- **초기 시도**: 단순 `switch` 문을 사용하는 `AppViewLocator` 구현.
- **변경 사항 (참조 코드 반영)**: 
  - `MainUiViewLocator.Register()` 메서드를 통해 `Splat.Locator`에 뷰-뷰모델 매핑을 등록하는 방식 적용.
  - `AppViewLocator`를 수정하여 **1차로 Splat 컨테이너를 조회**하고, 실패 시 **2차로 직접 매핑(Switch)**하는 하이브리드 안전장치(Fail-safe) 구현.
  - 이를 통해 "화면을 불러오는 중입니다..." 상태에서 멈추는 문제 해결.

### 3.3. 문제 해결 이력 (Troubleshooting)
1.  **RoutedViewHost 바인딩 에러**
    - 증상: `RoutedViewHost`의 `ViewLocator` 속성에 `{Binding}` 사용 시 `XamlParseException` 발생.
    - 원인: `ViewLocator`는 `DependencyProperty`가 아님.
    - 해결: XAML에서 바인딩을 제거하고, 코드 비하인드(`MainWindow.xaml.cs`) 생성자에서 직접 할당.

2.  **화면 전환 실패 (빈 화면)**
    - 증상: 뷰모델은 로드되었으나 뷰가 보이지 않음.
    - 원인: `RxApp.DefaultViewLocator`가 올바르게 설정되지 않았거나 Splat 조회 실패.
    - 해결: `AppViewLocator`를 통해 Splat 조회 및 수동 생성을 모두 지원하도록 개선하고, `MainWindow`에서 이를 명시적으로 사용하도록 설정.

3.  **버튼 클릭 무반응**
    - 증상: 네비게이션 버튼을 눌러도 반응 없음.
    - 원인: `ReactiveWindow`의 `DataContext`가 `ViewModel` 속성과 동기화되지 않는 현상.
    - 해결: `App.xaml.cs`에서 `mainWindow.DataContext = mainViewModel;`을 명시적으로 코딩.

4.  **폴더 구조 정리**
    - `MainWindow` 관련 파일을 프로젝트 루트에서 `Macro/Views` 폴더로 이동.
    - 네임스페이스를 `Macro.Views`로 통일하여 MVVM 구조 정립.

## 4. 최종 프로젝트 구조
```
D:\test\Macro\Macro\
├── ViewModels\
│   ├── MainWindowViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── RecipeViewModel.cs
│   └── TeachingViewModel.cs
├── Views\
│   ├── MainWindow.xaml (+.cs)
│   ├── DashboardView.xaml (+.cs)
│   ├── RecipeView.xaml (+.cs)
│   └── TeachingView.xaml (+.cs)
├── App.xaml (+.cs)      // 부트스트래핑 & DI 설정
├── AppViewLocator.cs    // 뷰 로케이터 구현체
└── MainUiViewLocator.cs // 뷰 등록 헬퍼 (참조 코드 스타일)
```

## 5. 향후 참고 사항
- 새로운 화면 추가 시 `MainUiViewLocator.cs`에 `Locator.CurrentMutable.Register` 코드를 추가해야 함.
- 만약 Splat 기반 자동 찾기가 안 될 경우 `AppViewLocator.cs`의 `switch` 문에도 추가해야 함.
