# VSMVVM

[![CI](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml/badge.svg)](https://github.com/MizzleAa/VSMVVM/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/tests-388%20passing-brightgreen)](#testing)
[![Targets](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0-blue)](#testing)
[![NuGet](https://img.shields.io/nuget/v/VSMVVM.Core?label=NuGet)](https://www.nuget.org/packages/VSMVVM.Core/)

Lightweight, modular MVVM framework for **WPF** with built-in Source Generator, DI Container, and Tailwind-inspired Design System.

GitHub Actions 에서 빌드/테스트되고 [nuget.org](https://www.nuget.org/packages?q=VSMVVM) 에 공개 배포됩니다.

## Architecture

| Package | Target | Description |
|---------|--------|-------------|
| **VSMVVM.Core** | .NET Standard 2.0 | MVVM base, DI, Source Generator, Messenger, Guard |
| **VSMVVM.WPF** | .NET 8 (WPF) | Host, Services, Controls, Behaviors, SVG |
| **VSMVVM.WPF.Design** | .NET 8 (WPF) | Tailwind-inspired design tokens, themed controls |

의존성: `VSMVVM.WPF.Design` → `VSMVVM.WPF` → `VSMVVM.Core` (NuGet 이 자동 해결).

---

## Installation

[nuget.org](https://www.nuget.org/packages?q=VSMVVM) 에서 설치합니다.

```bash
dotnet add package VSMVVM.WPF
dotnet add package VSMVVM.WPF.Design   # 선택
```

`VSMVVM.WPF` 가 `VSMVVM.Core` 를 자동으로 가져옵니다.

---

## Quick Start

```csharp
// Program.cs
using VSMVVM.WPF.Host;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VSMVVMHost
            .CreateHost<Bootstrapper, App>(args, "MyApp")
            .UseSplash<SplashWindow>()
            .Build()
            .RunApp<MainWindow>();
    }
}
```

```csharp
// Bootstrapper.cs
public class Bootstrapper : AppBootstrapper
{
    protected override void RegisterServices(IServiceCollection sc)
    {
        sc.AddSingleton<IDialogService, DialogService>();
        sc.AddSingleton<IDispatcherService, WPFDispatcherService>();
    }

    protected override void ViewModelMapping(IViewModelMapper mapper)
    {
        mapper.Register<MainView, MainViewModel>();
    }

    protected override void RegionMapping(IRegionManager rm)
    {
        rm.Mapping<HomeView>("MainRegion");
    }

    protected override void OnStartUp(IServiceContainer container)
    {
        var splash = container.GetService<ISplashService>();
        splash?.Report("Loading...", 0.5);
    }

    protected override void RegisterModules() { }
}
```

---

## VSMVVM.Core

### Source Generator

코드 생성기가 `partial class` 의 boilerplate 를 자동 생성합니다.

#### `[Property]`

```csharp
public partial class MyViewModel : ViewModelBase
{
    [Property]
    private string _name;
    // → public string Name { get; set; }  (with OnPropertyChanged)
}
```

#### `[PropertyChangedFor]`

```csharp
[Property]
[PropertyChangedFor(nameof(FullName))]
private string _firstName;
// → Name setter에서 OnPropertyChanged(nameof(FullName)) 자동 호출
```

#### `[NotifyCanExecuteChangedFor]`

```csharp
[Property]
[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
[NotifyCanExecuteChangedFor(nameof(ResetCommand))]
private string _name;
// → Name setter에서 SaveCommand?.RaiseCanExecuteChanged() 자동 호출
```

#### `[RelayCommand]` / `[AsyncRelayCommand]`

```csharp
[RelayCommand]
private void Save() => /* ... */;
// → public RelayCommand SaveCommand { get; }

[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() => /* ... */;
private bool CanSave() => !string.IsNullOrEmpty(Name);

[AsyncRelayCommand(CanExecute = nameof(CanLoad))]
private async Task LoadData()
{
    IsBusy = true;
    await Task.Delay(1000);
    IsBusy = false;
}
private bool CanLoad() => !IsBusy;
```

#### `[Log]`

`[RelayCommand]` / `[AsyncRelayCommand]` 메서드 또는 `[Property]` 필드에 함께 적용하면, Source Generator 가 생성된 코드 안에 `ILoggerService` 호출을 자동 삽입합니다 — "어떤 버튼이 눌렸는가 / 어떤 항목이 선택되었는가" 를 별도 코드 없이 기록할 때 사용.

```csharp
// 1) Command 메서드 — Command 실행 람다에 진입 로그 삽입.
[RelayCommand]
[Log]
private void Save() => /* ... */;
// → "[Command] MyNs.MyVm.Save" 를 Info 레벨로 자동 로깅

[RelayCommand]
[Log(Level = LogLevel.Debug)]
private void ToggleAside() => /* ... */;
// → Debug 레벨

[AsyncRelayCommand]
[Log(Level = LogLevel.Warn)]
private async Task SubmitOrder() => /* ... */;
// → 비동기 Command 도 동일

// 2) 파라미터가 있는 Command — CommandParameter 가 메시지에 보간되어 어떤 항목인지 식별 가능.
[RelayCommand]
[Log]
private void NavigateToPage(object naviObject) => /* ... */;
// → "[Command] MyNs.MyVm.NavigateToPage(Dashboard)" (naviObject.ToString() 보간)

// 3) [Property] 필드 — 생성된 setter 안에 값 변경 시 로깅 삽입. EqualityComparer 가드 후이므로 실제 변경 시에만 기록.
[Property]
[Log]
private ActionItem _selectedAction;
// → SelectedAction = 새 값 일 때 "[Property] MyNs.MyVm.SelectedAction = Import" 로깅
```

메시지 포맷:

- 파라미터 없는 Command: `[Command] {Namespace}.{ClassName}.{MethodName}`
- 파라미터 있는 Command: `[Command] {Namespace}.{ClassName}.{MethodName}({param})` — `param.ToString()` 결과. `null` 도 안전 (빈 문자열로 표시).
- Property: `[Property] {Namespace}.{ClassName}.{PropertyName} = {value}` — `value.ToString()` 결과.

동작 원리:

- 메서드/필드 본문은 변경하지 않음. 생성된 `XxxCommand` 람다 또는 `XxxProperty` setter 에만 로깅 호출이 삽입됨.
- 직접 메서드를 호출하거나 필드를 직접 대입(`_selectedAction = items[0]`)하면 로그가 남지 않음 — 오직 Command 경로(`ICommand.Execute`) 또는 Property setter 경로로 변경할 때만 기록.
- `ServiceLocator.GetServiceProvider()` 를 통해 `ILoggerService` 를 해석. ServiceLocator 가 미초기화여도 try/catch 가드로 명령/setter 실행은 정상 진행.
- `[Property]` setter 는 `EqualityComparer<T>.Default.Equals` (또는 ObservableValidator 의 `SetProperty`) 가드 안에서 로깅 — 같은 값으로 다시 set 해도 로그는 남지 않음 (의도된 변경만 기록).

### DI Container

Singleton / Transient / Scoped 라이프사이클 지원. 생성자 주입 기반.

```csharp
// 등록
sc.AddSingleton<IMyService, MyService>();
sc.AddTransient<ILogger, FileLogger>();
sc.AddSingleton<ICache>(new MemoryCache());
sc.AddSingleton<IDb, SqlDb>(c => new SqlDb(c.GetService<IConfig>()));

// 해석
var service = container.GetService<IMyService>();
```

### Services (Core)

Core 가 제공하는 핵심 서비스. 인터페이스만 정의된 항목 (WPF 에서 구현) 은 VSMVVM.WPF 섹션 참조.

| 서비스 | 인터페이스 | 핵심 메서드 | 용도 |
|---|---|---|---|
| **Messenger** | `IMessenger` | `Register`, `Send`, `Unregister`, `Cleanup` | WeakReference 기반 Pub/Sub, 토큰 채널 분리 |
| **LocalizeService** | `ILocalizeService` | `SetResourceManager`, `ChangeLocale`, `GetString`, `Subscribe` | 다국어 ResourceManager 기반 로컬라이제이션 |
| **RegionManager** | `IRegionManager` | `Mapping<TView>`, `Navigate`, `GoBack` / `GoForward`, `GetCurrentViewDisplayName` | View 네비게이션 + Back/Forward 히스토리 |
| **ServiceContainer** | `IServiceContainer` | `GetService<T>`, `GetService(Type)`, `ContainsService` | DI 해석 (resolve) |
| **ServiceCollection** | `IServiceCollection` | `AddSingleton` / `AddTransient` / `AddScoped`, `CreateContainer` | DI 등록 |

인터페이스만 Core 에 정의되고 구현이 WPF 에 있는 항목:
`IDispatcherService`, `IDialogService`, `IWindowService`, `IBuildInfoService`, `ISplashService`, `IVersionControlService` → **VSMVVM.WPF / Services (WPF)** 참조.

`ILoggerService` 는 사용자 정의 구현 (NLog/Serilog 등을 어댑터로 등록).

### Messenger

타입 기반 Pub/Sub 메시징. WeakReference 기반이라 수신자가 GC 되면 자동 해제.

```csharp
// 구독
messenger.Register<MyMessage>(this, msg => Handle(msg));

// 토큰 채널 분리
messenger.Register<MyMessage>(this, "channelA", msg => HandleA(msg));

// 발행
messenger.Send(new MyMessage { Data = "Hello" });
messenger.Send(new MyMessage(), "channelA");

// 해제
messenger.Unregister<MyMessage>(this);
messenger.UnregisterAll(this);
```

### ObservableValidator

DataAnnotation 기반 유효성 검증.

```csharp
public partial class FormViewModel : ObservableValidator
{
    [Property]
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(2)]
    private string _name;

    [RelayCommand]
    private void Submit()
    {
        ValidateAllProperties();
        if (!HasErrors) { /* save */ }
    }
}
```

### StateStore (Redux-style)

전역 상태 관리. WeakReference 기반 구독.

```csharp
public class AppStateStore : StateStoreBase<AppState>
{
    public AppStateStore() : base(new AppState()) { }

    public void IncrementCounter()
    {
        var next = new AppState { Counter = State.Counter + 1 };
        UpdateState(next);  // → 모든 구독자에 자동 통지
    }
}

// 구독
store.Subscribe(state => Counter = state.Counter);
```

### Guard

Fail-fast 방어적 검증 유틸리티.

```csharp
Guard.IsNotNull(param, nameof(param));
Guard.IsNotNullOrEmpty(name, nameof(name));
Guard.IsInRange(age, 1, 150, nameof(age));
Guard.IsOfType<IService>(obj, nameof(obj));
Guard.IsNotEmpty(list, nameof(list));
Guard.IsTrue(condition, nameof(condition));
```

### ViewModelBase

`INotifyPropertyChanged` / `INotifyPropertyChanging` / `ICleanup` 구현.

```csharp
public partial class MyViewModel : ViewModelBase
{
    // SetProperty, OnPropertyChanged, OnPropertyChanging 제공
    // partial 메서드: On{Property}Changing(value), On{Property}Changed(oldValue, newValue)
}
```

### BatchObservableCollection

대량 추가/제거 시 단일 `CollectionChanged` 이벤트 발생.

### Helper Interfaces

| 인터페이스 | 용도 |
|---|---|
| `ICleanup` | ViewModel 정리 (`Cleanup()`) |
| `IAsyncInitializable` | 비동기 초기화 (`InitializeAsync()`) |
| `IServiceInitializable` | 서비스 초기화 (`Initialize(IServiceContainer)`) |
| `IModule` | 모듈 시스템 (`RegisterServices`, `ViewModelMapping`, `RegionMapping`) |
| `INavigateAware` | 네비게이션 인식 (`OnNavigatedTo`, `OnNavigatedFrom`) |
| `IRegion` | Region 컨트롤 인터페이스 (`Activate`, `Deactivate`) |
| `IViewModelMapper` | View-ViewModel 매핑 (`Register<TView, TViewModel>`) |
| `IAsyncRelayCommand` | 비동기 명령 (`ExecuteAsync`, `CanExecute`) |

---

## VSMVVM.WPF

### Host (Fluent Builder)

```csharp
VSMVVMHost
    .CreateHost<Bootstrapper, App>(args, "MyApp")
    .UseSplash<SplashWindow>()         // 별도 STA 스레드 스플래시
    .Build()                           // Bootstrapper 라이프사이클 실행
    .ShutdownMode(ShutdownMode.OnMainWindowClose)
    .Popup<LoginWindow>(dialog: true)  // 모달 팝업
    .RunApp<MainWindow>();             // Application.Run()
```

### Services (WPF)

Core 인터페이스의 구현체 + WPF 고유 서비스. DI 컨테이너에 등록해 사용.

| Service | Interface | Description |
|---------|-----------|-------------|
| **WPFDispatcherService** | `IDispatcherService` | UI 스레드 디스패칭 (`Invoke`, `InvokeAsync`, `Yield` Background priority) |
| **DialogService** | `IDialogService` | XAML 디자인 시스템 기반 모달 다이얼로그 (OK / OK·Cancel / Yes·No), 파일/폴더 다이얼로그 |
| **WindowService** | `IWindowService` | Window 상속 View 직접 표시. `DialogParameter` / `DialogResultData` 반사 주입 |
| **SplashService** | `ISplashService` | 별도 STA 스레드 스플래시 (`Report(msg, progress)` + 자동 Close) |
| **BuildInfoService** | `IBuildInfoService` | `AppVersion`, `BuildTime`, `FrameworkVersion`, `OSArchitecture` |
| **VersionControlService** | `IVersionControlService` | Git 정보 (`BranchName`, `CommitHash`, `CommitDate`, `IsDirty`) |
| **WindowPlacementService** | `IWindowPlacementService` | 창 위치/크기 JSON 저장/복원 |
| **ShortcutService** | `IShortcutService` | 글로벌/스코프 키보드 단축키 (`RegisterGlobal`, `RegisterScoped`) |
| **ZoomService** | `IZoomService` | 줌/팬 좌표 변환 (Canvas 인터랙션) |
| **UndoRedoService** | `IUndoRedoService` | Undo/Redo 스택 (용량 제한 50, `CanUndo` / `CanRedo`) |
| **WpfFileDialogService** | `IFileDialogService` | 경량 파일 열기/저장 다이얼로그 |

#### 등록 예

```csharp
protected override void RegisterServices(IServiceCollection sc)
{
    sc.AddSingleton<IDispatcherService, WPFDispatcherService>();
    sc.AddSingleton<IDialogService, DialogService>();
    sc.AddSingleton<ISplashService, SplashService>();
    sc.AddSingleton<IBuildInfoService, BuildInfoService>();
    sc.AddSingleton<IUndoRedoService, UndoRedoService>();
    sc.AddSingleton<IShortcutService, ShortcutService>();
    // ...
}
```

### Controls

| Control | Description |
|---------|-------------|
| **WPFRegion** | ContentControl 기반 Region (Navigation + `INavigateAware` + Back/Forward History) |
| **ImageCanvas** | 줌/팬 캔버스, 자식 선택/리사이즈/드래그 지원 |
| **LayeredCanvas** | Z-Order 레이어 캔버스 |
| **CanvasSelectionAdorner** | 8방향 리사이즈 핸들 Adorner |

### Behaviors (Interaction)

XAML 에서 이벤트→커맨드 바인딩.

```xml
<Button xmlns:i="clr-namespace:VSMVVM.WPF.MarkupExtensions;assembly=VSMVVM.WPF">
    <i:Interaction.Triggers>
        <i:EventTrigger EventName="MouseEnter">
            <i:InvokeCommandAction Command="{Binding HoverCommand}"/>
        </i:EventTrigger>
    </i:Interaction.Triggers>
</Button>
```

### MarkupExtensions

| Extension | Description |
|-----------|-------------|
| **ViewModelLocator** | `AutoWireViewModel="True"` — View-ViewModel 자동 바인딩 |
| **LocalizeExtension** | `{me:Localize Key=UI_TITLE}` — 다국어 바인딩 |
| **BindingProxy** | DataContext 프록시 (DataGrid 등 비시각 트리 바인딩) |
| **EqualityConverter** | 두 바인딩 값 비교 MultiValueConverter |

### Media

| Feature | Description |
|---------|-------------|
| **SvgImageConverter** | SVG → DrawingImage 변환 |
| **SvgImageExtension** | `{me:SvgImage Source=/Assets/icon.svg}` XAML 마크업 |

### GlobalExceptionHandler

```csharp
GlobalExceptionHandler.Initialize(app);
// → DispatcherUnhandledException + TaskScheduler.UnobservedTaskException 자동 처리
```

---

## VSMVVM.WPF.Design

Tailwind CSS 에서 영감받은 유틸리티 기반 WPF 디자인 시스템.

### 적용

```xml
<!-- App.xaml -->
<ResourceDictionary Source="/VSMVVM.WPF.Design;component/Index.xaml"/>
```

### Tokens

| Category | Examples |
|----------|---------|
| **Spacing** | `P1`~`P8`, `M1`~`M8`, `Mb2`, `Px2Py1`, `Gap2`~`Gap4` |
| **Typography** | `TextXs`~`Text4xl`, `FontSans`, `FontBold`, `FontSemibold` |
| **Sizing** | `W12`~`W96`, `H1`~`H12`, `MinW20`~`MinW64` |
| **Effects** | `RoundedSm`/`Md`/`Lg`/`Full`, `ShadowSm`/`Md`/`Lg`, `Border`, `Opacity50` |

### Colors (Dark / Light)

Zinc + Blue 팔레트. `ThemeDark.xaml` / `ThemeLight.xaml` 런타임 전환.

| Token | Description |
|-------|-------------|
| `BgPrimary` / `BgSecondary` / `BgTertiary` | 배경 |
| `TextPrimary` / `TextSecondary` / `TextMuted` | 텍스트 |
| `AccentPrimary` / `AccentHover` | 강조색 (Blue) |
| `BorderDefault` / `BorderHover` / `BorderFocus` | 테두리 |
| `Success` / `Warning` / `Error` / `Info` | 상태 색상 |

### Styled Controls (15)

모든 기본 컨트롤에 디자인 토큰이 적용됩니다:

`Button` · `TextBox` · `PasswordBox` · `CheckBox` · `ComboBox` · `ListBox` · `ListView` · `DataGrid` · `TabControl` · `Expander` · `ScrollViewer` · `ProgressBar` · `ContextMenu` · `Window` · `Dialog`

### Components

| Component | Description |
|-----------|-------------|
| **DateTimePicker** | 날짜/시간 선택기 |
| **InfoPopup** | 정보 팝업/툴팁 컴포넌트 |
| **JsonEditor** | JSON 구문 강조 편집기 |
| **LoadingOverlay** | 로딩 오버레이 (`IsLoading` 바인딩) |

### Window Chrome

커스텀 타이틀바 + 최소화/최대화/닫기 버튼. `WindowChrome` + `WindowButtonsBehavior` 기반.
`WindowChrome.CustomButtons` attached property 로 AppBar 에 커스텀 버튼(Back/Forward 등) 배치 가능.

### Navigation History (Back / Forward)

`IRegionManager` 가 네비게이션 히스토리 스택을 자동 관리합니다.

```csharp
// 이전/다음 페이지 이동
regionManager.GoBack("MainRegion");
regionManager.GoForward("MainRegion");

// 상태 확인
bool canBack = regionManager.CanGoBack("MainRegion");
bool canForward = regionManager.CanGoForward("MainRegion");

// 현재 View 표시 이름 자동 생성 (PascalCase → 공백 분리)
// "DefaultDesignView" → "Default Design"
string displayName = regionManager.GetCurrentViewDisplayName("MainRegion");
```

### Logging

`ILoggerService` 인터페이스로 로깅 추상화. Trace/Debug/Info/Warn/Error/Fatal 6단계 레벨 지원.

```csharp
// DI 등록
sc.AddSingleton<ILoggerService, MyLoggerService>();

// 사용
logger.Info("App started");
logger.Error("Failed", exception);
```

#### Command / Property 자동 로깅 (`[Log]` 어트리뷰트)

`[RelayCommand]` / `[AsyncRelayCommand]` 메서드 또는 `[Property]` 필드에 `[Log]` 를 함께 붙이면 Source Generator 가 Command 람다 / Property setter 에 로깅 호출을 삽입합니다. "어떤 버튼이 눌렸는가 / 어떤 항목이 선택되었는가" 를 명시 호출 없이 추적할 때 사용 — 자세한 사용법은 위의 [`[Log]`](#log) 섹션 참조.

- 기본 레벨: `Info` (`Level = LogLevel.Debug` 등으로 변경 가능)
- 메시지 포맷:
  - `[Command] {Ns}.{Cls}.{Method}` (파라미터 없음)
  - `[Command] {Ns}.{Cls}.{Method}({param})` (CommandParameter 보간)
  - `[Property] {Ns}.{Cls}.{Property} = {value}` (setter 호출)
- 사전 조건: `ServiceLocator.SetServiceProvider(...)` 가 호출되어 `ILoggerService` 해석 가능해야 함. 미초기화여도 try/catch 가드로 명령 실행 / setter 는 정상 동작.

```csharp
[RelayCommand]
[Log]
private void NavigateToPage(object naviObject) { /* ... */ }
// 사용자 클릭 시 NLog 출력 예시:
// {"level":"INFO","message":"[Command] DeepInsight.Aside.ViewModels.AsideViewModel.NavigateToPage(Dashboard)", ...}

[Property]
[Log]
private ActionItem _selectedAction;
// 사이드바 메뉴 항목 클릭 시:
// {"level":"INFO","message":"[Property] DeepInsight.TrainingManagement.ViewModels.TrainingActionsViewModel.SelectedAction = Train Start", ...}
```

---

## Sample

### `sample/VSMVVM.WPF.Sample`

전체 기능을 한 곳에서 확인할 수 있는 WPF 데모 애플리케이션.

```powershell
cd C:\workspace\VSMVVM
dotnet run --project sample/VSMVVM.WPF.Sample
```

데모하는 기능:

- Source Generator (`[Property]`, `[RelayCommand]`, `[AsyncRelayCommand]`, `[Log]`) 동작
- DI 컨테이너 + Bootstrapper 패턴
- Region 네비게이션 + Back/Forward 히스토리
- Dialog / Window 서비스 (모달, 파일/폴더 다이얼로그)
- Splash + Progress Report
- Design 시스템 (Dark / Light 테마, 컴포넌트, 토큰)
- Localize (다국어)
- StateStore, Messenger
- Undo/Redo, Shortcut, Zoom

---

## Testing

xUnit + FluentAssertions 기반 단위 테스트. **Warning-free build (0 error)**, multi-targeting (net8.0 + net9.0).

```powershell
# 전체 테스트
dotnet test

# 개별 프로젝트
dotnet test tests/VSMVVM.Core.Tests/
dotnet test tests/VSMVVM.WPF.Tests/
```

### 현재 테스트 수치 (v1.2.7)

| Suite | net8.0 | net9.0 | Total | Coverage |
|---|---:|---:|---:|---|
| VSMVVM.Core.Tests | 165 | 165 | **330** | DI, Guard, Messenger, ViewModelBase, RelayCommand, AsyncRelayCommand, StateStore, ObservableValidator, BatchObservableCollection, Logging (ILoggerService + `[Log]` for Command/Property/ObservableValidator + CommandParameter 보간), RegionManager (Back/Forward, DisplayName) |
| VSMVVM.WPF.Tests | 29 | 29 | **58** | ServiceLocator, DialogResult, Canvas (Mask, Histogram), Behaviors |
| **합계** | **194** | **194** | **388** | |

---

## Release Workflow

GitHub Actions 가 빌드/테스트/nuget.org publish 까지 자동 수행합니다. **태그 한 줄** 또는 **GitHub UI 버튼**으로 트리거합니다.

### 옵션 A — 태그 push (권장)

```powershell
git tag v1.2.8
git push origin main
git push origin v1.2.8
# → Actions 가 자동으로 Directory.Build.props 의 <Version> 갱신 → build → test → pack → nuget.org push
```

태그 이름의 `v` 다음 값이 그대로 버전이 됩니다 (`v1.2.8` → `1.2.8`).

### 옵션 B — GitHub UI 수동 실행

Actions 탭 → **Publish to NuGet** 워크플로우 → **Run workflow** 버튼 → `version` 입력 (예: `1.2.8`) → Run.

### 안전장치

- `--skip-duplicate` — 같은 버전이 이미 nuget.org 에 있어도 실패하지 않음 (재실행 안전)
- test 실패 → publish 단계 실행되지 않음
- `NUGET_API_KEY` 는 GitHub repo Secret 으로만 주입 (로그/yml 평문 노출 없음)
- 패키지 3개(`Core` → `WPF` → `WPF.Design`)는 의존성 순서로 빌드/팩/푸시되며, 산출물은 Actions artifact (`nupkgs-{version}`) 로도 보존됨

### 로컬에서 빌드/팩 (publish 전 검증용)

```powershell
cd C:\workspace\VSMVVM

# Directory.Build.props 의 <Version> 을 원하는 값으로 수정 후
dotnet build VSMVVM.sln -c Release    # → bin/Release/*.X.Y.Z.nupkg 생성
dotnet test  VSMVVM.sln -c Release --no-build

# 직접 push 가 필요하면 (자동 워크플로우 우회 시)
.\scripts\publish-nuget.ps1 -Version 1.2.8 -ApiKey $env:NUGET_API_KEY
```

---

## CI/CD

| 워크플로우 | 트리거 | 동작 |
| --- | --- | --- |
| [.github/workflows/ci.yml](.github/workflows/ci.yml) | `main` / `develop` push, PR | build + test |
| [.github/workflows/publish-nuget.yml](.github/workflows/publish-nuget.yml) | `v*.*.*` 태그 push, 수동 실행 | build + test + pack + **nuget.org publish** |

### 확인 URL

- Actions: <https://github.com/MizzleAa/VSMVVM/actions>
- nuget.org: <https://www.nuget.org/packages?q=VSMVVM>

---

## Project Structure

```
vsmvvm/
├── src/
│   ├── VSMVVM.Core/              # .NET Standard 2.0
│   │   ├── Attributes/           # [Property], [RelayCommand], [AsyncRelayCommand], ...
│   │   ├── CodeGen/              # Source Generator 구현
│   │   ├── Guard/                # 방어적 검증
│   │   └── MVVM/                 # ViewModelBase, DI, Messenger, StateStore, RegionManager, ...
│   │
│   ├── VSMVVM.WPF/               # .NET 8 (WPF)
│   │   ├── Behaviors/            # EventTrigger, EventToCommand, InvokeCommandAction
│   │   ├── Controls/             # WPFRegion, ImageCanvas, LayeredCanvas
│   │   ├── Host/                 # VSMVVMHost (Fluent Builder)
│   │   ├── MarkupExtensions/     # ViewModelLocator, Localize, BindingProxy
│   │   ├── Media/                # SVG 지원
│   │   └── Services/             # Dialog, Window, Splash, Dispatcher, BuildInfo,
│   │                             # VersionControl, WindowPlacement, Shortcut, Zoom, UndoRedo, ...
│   │
│   └── VSMVVM.WPF.Design/        # .NET 8 (WPF)
│       ├── Colors/               # Palette, ThemeDark, ThemeLight
│       ├── Components/           # DateTimePicker, JsonEditor, LoadingOverlay, ...
│       ├── Controls/             # Button, TextBox, DataGrid, Window, Dialog, ...
│       ├── Core/                 # SharedResourceDictionary, WindowChrome
│       └── Tokens/               # Spacing, Typography, Sizing, Effects
│
├── tests/
│   ├── VSMVVM.Core.Tests/        # 147 × 2 framework
│   └── VSMVVM.WPF.Tests/         # 29 × 2 framework
│
├── sample/
│   └── VSMVVM.WPF.Sample/        # 데모 애플리케이션
│
├── scripts/
│   ├── publish-nuget.ps1         # nuget.org push (PowerShell)
│   └── publish-nuget.sh          # nuget.org push (bash)
│
├── .github/workflows/ci.yml      # GitHub Actions CI (build + test)
├── Directory.Build.props         # 공통 메타데이터 + <Version>
└── NuGet.config
```

> `src/VSMVVM.WinForms` 및 `src/VSMVVM.WinForms.Design` 도 저장소에 존재하지만 NuGet 배포 대상이 아니며 본 문서에서 다루지 않습니다.

---

## License

MIT
