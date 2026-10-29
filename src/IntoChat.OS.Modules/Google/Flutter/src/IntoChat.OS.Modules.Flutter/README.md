# IntoChat.OS.Modules.Flutter

Flutter module integration for the neuron kernel.

Module id: `flutter`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Google/Flutter/tests/IntoChat.OS.Modules.Flutter.Tests.Hosting/IntoChat.OS.Modules.Flutter.Tests.Hosting.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:flutter:Options` (environment prefix `IntoChatOS__Modules__flutter__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `Hosting` | `FlutterHostingOptions` | `FlutterHostingOptions { Kind = Window, ResourceName = FlutterShell, DeviceTarget = windows, ShellName = desk, ChatName = main, FlutterCommand = , WorkingDirectory = , ReleaseBuild = False, WebContainer = False }` | no |

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: assembly `IntoChat.OS.Modules.Flutter.Contracts` (`IButton`, `ICalendar`, `ICard`, `IChart`, `IChatIndex`, `IChat`, `IClock`, `ICollectionView`, `IColor`, `IComposer`, `ICompute`, `IEntryControl`, `IExpander`, `IFileInput`, `IForm`, `IGraph`, `IImageCanvas`, `IImage`, `IInfoBar`, `ILayout`, `IMap`, `IPerson`, `IPreferenceControl`, `IProgress`, `IRating`, `ISelect`, `ISheet`, `ISlider`, `ISurface`, `ITable`, `ITabs`, `ITextField`, `IText`, `IToggle`, `ITree`, `IUiBinding`, `IUiEventHandler`, `IVideo`, `IView`, `IViewport`, `IVoiceInput`, `IWebBrowser`, `IWorkspace`).
Hosting: runs in process.
<!-- END GENERATED -->
