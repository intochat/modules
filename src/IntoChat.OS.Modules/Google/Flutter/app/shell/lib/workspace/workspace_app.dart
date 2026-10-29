import 'dart:async';
import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../connections/connect_window.dart';
import '../connections/connectors_menu.dart';
import '../desktop/command_palette.dart';
import '../desktop/smart_desktop.dart';
import '../space/neuron_space.dart';
import '../space/space_controller.dart';
import 'apps/app_activations.dart';
import 'width_hint.dart';
import 'workspace_store.dart';
import 'workspace_link.dart';
import 'compute_usage_panel.dart';
import 'csharp/csharp_manager.dart';
import 'workspace_preferences_io.dart'
    if (dart.library.js_interop) 'workspace_preferences_web.dart'
    as preferences;

class WorkspaceApp extends StatefulWidget {
  const WorkspaceApp({
    super.key,
    this.store,
    this.onSwitchAccount,
    this.initialLocation,
    this.persistenceKey = 'intochatos.preferences.v1',
    this.onOpenUrl,
    this.kernelBaseUri,
    this.statusMessage,
    this.programmingClient,
  });
  final WorkspaceStore? store;
  final Future<void> Function()? onSwitchAccount;
  final Uri? initialLocation, kernelBaseUri;
  final String persistenceKey;
  final OpenUrl? onOpenUrl;
  final String? statusMessage;
  final IntoChatOSUiClient? programmingClient;
  @override
  State<WorkspaceApp> createState() => _WorkspaceAppState();
}

class _WorkspaceAppState extends State<WorkspaceApp>
    with WidgetsBindingObserver {
  // A connector may mount any app-authored root. Product composition stays in that app.
  static const _root = String.fromEnvironment('INTOCHATOS_ROOT');
  static const _rootKind = String.fromEnvironment(
    'INTOCHATOS_ROOT_KIND',
    defaultValue: 'layout',
  );
  late final WorkspaceStore store =
      widget.store ??
      WorkspaceStore(
        readPreferences: () =>
            preferences.readPreferences(widget.persistenceKey),
        writePreferences: (value) =>
            preferences.writePreferences(widget.persistenceKey, value),
      );
  late final _localPreferences = WorkspaceLocalPreferences(store);
  final _navigator = GlobalKey<NavigatorState>();
  SpaceController? _space;
  String? _rootName;
  String? _activationApp;
  String _renderKind = 'layout';
  String? _brain;
  Object? _startupError;
  bool _developerMode = false;
  bool _ready = false;
  bool _paletteOpen = false;
  BarLayout _barLayout = BarLayout.floatingGlass;
  Uri? _pendingLocation;
  @override
  void initState() {
    super.initState();
    _pendingLocation = widget.initialLocation;
    WidgetsBinding.instance.addObserver(this);
    store.addListener(_changed);
    unawaited(_start());
  }

  @override
  Future<bool> didPushRouteInformation(
    RouteInformation routeInformation,
  ) async {
    final action = workspaceLink(routeInformation.uri);
    if (action == null) return false;
    if (!_ready) {
      _pendingLocation = routeInformation.uri;
    } else {
      await _openLink(routeInformation.uri, action);
    }
    return true;
  }

  Future<void> _openLink(Uri location, Map<String, dynamic> action) async {
    await _activate(action);
    if (!mounted) return;
    final route = location.fragment.startsWith('/')
        ? Uri.parse(location.fragment)
        : location;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        unawaited(
          SystemNavigator.routeInformationUpdated(uri: route, replace: true),
        );
      }
    });
    WidgetsBinding.instance.scheduleFrame();
  }

  void _changed() {
    if (mounted) setState(() {});
  }

  Future<void> _start() async {
    await store.load();
    final client = widget.programmingClient;
    if (client == null || !mounted) return;
    try {
      final brain = client.defaultBrainId;
      if (brain == null) throw StateError('No brain selected.');
      await _selectBrain(brain);
      if (!mounted) return;
      _ready = true;
      final location = _pendingLocation;
      _pendingLocation = null;
      if (workspaceLink(location) case final action?) {
        await _openLink(location!, action);
      }
      unawaited(
        client
            .readCapabilities()
            .then((value) {
              if (mounted) setState(() => _developerMode = value.developerMode);
            })
            .catchError((Object _) {}),
      );
      if (_rootName == null &&
          widget.initialLocation?.path.startsWith('/settings') == true) {
        unawaited(_activate({'kind': 'settings'}));
      }
    } catch (error) {
      if (mounted) setState(() => _startupError = error);
    }
  }

  Future<void> _selectBrain(
    String brain, {
    String? show,
    bool reconnect = false,
  }) async {
    if (_brain == brain && !reconnect) return;
    if (_root.isNotEmpty) {
      _space?.dispose();
      _space = null;
      setState(() {
        _brain = brain;
        _rootName = '$brain/$_root';
        _renderKind = _rootKind;
      });
      return;
    }
    final client = widget.programmingClient!;
    final connection = await client.appRequest(brain, 'connect', body: {});
    if (connection['root'] case final Map root) {
      final name = root['name'] as String;
      final kind = root['kind'] as String;
      if (!mounted) return;
      _space?.dispose();
      _space = null;
      setState(() {
        _brain = brain;
        _rootName = name;
        _activationApp = connection['activationApp'] as String?;
        _renderKind = kind;
      });
      return;
    }
    _rootName = null;
    _activationApp = null;
    final old = _space;
    final controller = SpaceController(
      client: client,
      scenes: client.scenes(brain),
      brain: brain,
    );
    // Initial connection exposes its read error and retry on the space screen.
    if (old == null) {
      _brain = brain;
      _space = controller;
      if (mounted) setState(() {});
      await controller.start();
      return;
    }
    try {
      await controller.start();
      if (controller.error.value case final Object problem) throw problem;
      if (show != null) await controller.show(show);
      if (!mounted) {
        controller.dispose();
        return;
      }
      setState(() {
        _brain = brain;
        _space = controller;
      });
      old.dispose();
    } catch (_) {
      controller.dispose();
      rethrow;
    }
  }

  Future<Uint8List?> _icon(String neuron, String icon) async {
    final client = widget.programmingClient;
    if (client == null) return null;
    final marker = neuron.indexOf('/apps/');
    if (marker <= 0) return null;
    final brain = neuron.substring(0, marker);
    final app = neuron.substring(marker + 6).split('/').first;
    final manifest = await client.readAppManifest(brain, app);
    if (manifest.icon != icon || shellIcon(icon) != null) return null;
    return client.readBytes(
      '/brains/${Uri.encodeComponent(brain)}/apps/${Uri.encodeComponent(app)}/icon',
    );
  }

  // The desktop root lays out one window on a narrow screen: tell it which side of 900 px the window is.
  Widget _widthHinted(Widget root) {
    final brain = _brain!;
    if (_renderKind != 'view' || _rootName != '$brain/apps/desktop/view') {
      return root;
    }
    return WidthHint(
      key: ValueKey('width:$_rootName'),
      onNarrow: (narrow) => unawaited(
        widget.programmingClient!
            .requestApp(brain, 'desktop', 'narrow', '$narrow')
            .then<void>((_) {}, onError: (Object _) {}),
      ),
      child: root,
    );
  }

  // The request is handled asynchronously, so connect again (a few times, briefly) until the root changes.
  Future<void> _reconnect(Map<String, dynamic> action) async {
    final brain = _brain!;
    final app = action['application'] as String?;
    final operation = action['operation'] as String?;
    if (app != null && operation != null) {
      await widget.programmingClient!.requestApp(
        brain,
        app,
        operation,
        action['input'] as String? ?? '',
      );
    }
    // A fixed root (INTOCHATOS_ROOT) never changes on connect: say so at once.
    if (_root.isEmpty) {
      final before = _rootName;
      for (var attempt = 0; attempt < 10 && mounted; attempt++) {
        await _selectBrain(brain, reconnect: true);
        if (_rootName != before || operation == null) return;
        await Future<void>.delayed(const Duration(milliseconds: 300));
      }
    }
    if (!mounted) return;
    _toast('Layout will change on next start');
  }

  void _toast(String message, {UiTone tone = UiTone.neutral}) {
    final overlay = _navigator.currentState?.overlay?.context;
    if (overlay != null) showKitToast(overlay, message, tone: tone);
  }

  Future<void> _activate(Map<String, dynamic> action) async {
    final client = widget.programmingClient;
    if (client == null) return;
    // A legacy settings activation is the Settings app's workspace activation.
    if (action['kind'] == 'settings') action = settingsActivation(action);
    // An app's configuration is the Apps app's detail for it.
    if (action['view'] == 'configuration' && action['application'] is String) {
      action = configurationActivation(action);
    }
    try {
      if (action['kind'] == 'account') {
        await widget.onSwitchAccount?.call();
        return;
      }
      // The lobby neuron signed the visitor in: leave the lobby and read the member session.
      if (action['kind'] == 'lobby-ended') {
        await client.endLobby();
        return;
      }
      // The layout switch: hand the app its request, then connect again and mount the new root.
      // Only the desktop's layout switch may reconnect; any other reconnect is ignored.
      if (action['kind'] == 'reconnect') {
        if (action['application'] == 'desktop' && _brain != null) {
          await _reconnect(action);
        }
        return;
      }
      final brain = action['brainId'] as String? ?? _brain!;
      final app = action['application'] as String?;
      final neuron =
          action['neuron'] as String? ??
          (app == null ? null : '$brain/apps/$app');
      final changingBrain = brain != _brain;
      // Install, update and remove run over the person's own session in a brain they belong to; the space
      // stays where it is, and an install then opens the app.
      if (const {'install', 'update', 'remove'}.contains(action['kind'])) {
        if (changingBrain &&
            !(await client.listBrains()).any((entry) => entry.id == brain)) {
          throw StateError('This account is not a member of that brain.');
        }
        final result = await runAppActivation({
          ...action,
          'brainId': brain,
        }, client.jsonRequest);
        if (!mounted) return;
        if (result.toast.isNotEmpty) _toast(result.toast, tone: result.tone);
        if (result.reopen case final String reopen) {
          await _activate({
            'brainId': brain,
            'kind': 'workspace',
            'application': 'apps',
            'operation': 'open',
            'input': reopen,
          });
        }
        if (result.open case final String open) {
          await _activate({
            'brainId': brain,
            'kind': 'workspace',
            'application': open,
            'operation': 'open',
          });
        }
        return;
      }
      if (changingBrain) {
        final brains = await client.listBrains();
        if (!brains.any((entry) => entry.id == brain)) {
          throw StateError('This account is not a member of that brain.');
        }
        if (!mounted) return;
        await _selectBrain(
          brain,
          show:
              const {
                'connections',
                'install',
                'update',
                'remove',
              }.contains(action['kind'])
              ? null
              : neuron,
        );
      }
      if (action['kind'] == 'connections') {
        if (!mounted) return;
        showDialog<void>(
          context: _navigator.currentContext!,
          builder: (_) => Dialog(
            child: SizedBox(
              width: 560,
              height: 520,
              child: ConnectWindow(
                // Connect preselects its connector; the connection is the one the activation names, else the connector.
                initialConnectorId: action['connector'] as String?,
                initialConnectionId: connectWindowConnectionId(action),
                request: (path, {body}) =>
                    client.workspaceConnectionsRequest(brain, path, body: body),
                connectorRequest: (path, {body}) =>
                    client.connectorRequest(brain, path, body: body),
                windowsRequest: (path, {body}) =>
                    client.workspaceWindowsRequest(brain, path, body: body),
                onOpen: widget.onOpenUrl,
              ),
            ),
          ),
        );
        return;
      }
      if (_rootName != null) {
        final receiver = _activationApp;
        if (receiver == null) {
          throw StateError('This root has no navigation receiver.');
        }
        await client.requestApp(
          brain,
          receiver,
          'activate',
          jsonEncode(action),
        );
        return;
      }
      if (neuron != null && !changingBrain) await _space!.show(neuron);
      if (action['operation'] case final String operation) {
        if (app != null) {
          await client.requestApp(
            brain,
            app,
            operation,
            action['input'] as String? ?? '',
          );
        }
      }
    } on StateError catch (error) {
      // Shell-authored copy (e.g. membership), never a transport exception.
      if (mounted) _toast(error.message, tone: UiTone.error);
    } catch (_) {
      if (mounted) {
        _toast('Could not navigate there. Try again.', tone: UiTone.error);
      }
    }
  }

  Future<void> _request(String operation, String input) async {
    final client = widget.programmingClient, space = _space;
    if (client == null || space == null) return;
    final prefix = '${space.brain}/apps/';
    final focused = space.view.value.focused;
    if (!focused.startsWith(prefix)) return;
    try {
      await client.requestApp(
        space.brain,
        focused.substring(prefix.length).split('/').first,
        operation,
        input,
      );
    } catch (_) {
      if (mounted) {
        _toast('Could not run that action. Try again.', tone: UiTone.error);
      }
    }
  }

  Future<void> _tool(String name) async {
    final client = widget.programmingClient!;
    final navigator = _navigator.currentState!;
    try {
      switch (name) {
        case 'preferences':
          await _activate({'kind': 'settings'});
        case 'connections':
          await _activate({'kind': 'connections'});
        case 'compute':
          navigator.push(
            MaterialPageRoute<void>(
              builder: (_) => Scaffold(
                body: ComputeUsagePanel(
                  client: client,
                  workspaceId: _brain!,
                  workspaceName: _brain!,
                  onClose: () => navigator.pop(),
                ),
              ),
            ),
          );
        case 'csharp':
          if (_developerMode) {
            navigator.push(
              MaterialPageRoute<void>(
                builder: (_) => Scaffold(
                  appBar: AppBar(title: const Text('C# files')),
                  body: CSharpManager(
                    request: (method, path, {body}) =>
                        client.csharpRequest(_brain!, method, path, body: body),
                    onAsk: (id, prompt) => unawaited(
                      client.requestApp(_brain!, 'ino', 'ask', prompt),
                    ),
                    onSelected: (_) {},
                  ),
                ),
              ),
            );
          }
        case 'new':
          final brain = await client.createWorkspace();
          if (mounted) await _selectBrain(brain);
        case 'gallery':
          navigator.push(
            MaterialPageRoute<void>(
              builder: (_) => Scaffold(
                appBar: AppBar(title: const Text('IntoChat UI Gallery')),
                body: const UiGalleryScreen(),
              ),
            ),
          );
        case 'palette':
          setState(() => _paletteOpen = !_paletteOpen);
        case 'smart-desktop':
          navigator.push(
            MaterialPageRoute<void>(
              builder: (_) => Scaffold(
                appBar: AppBar(title: const Text('Smart Desktop · 3D Brain')),
                body: SmartDesktop(
                  stylePreset: _currentStylePreset(),
                  barLayout: _barLayout,
                  onLaunchApp: (appId) {
                    navigator.pop();
                    if (appId == 'gallery') {
                      unawaited(_tool('gallery'));
                    } else {
                      unawaited(
                        _activate({
                          'kind': 'workspace',
                          'application': appId,
                          'operation': 'open',
                        }),
                      );
                    }
                  },
                  onOpenPalette: () {
                    navigator.pop();
                    setState(() => _paletteOpen = true);
                  },
                  onSelectPreset: (preset) =>
                      unawaited(_tool('theme:${preset.id}')),
                  onCycleBarLayout: () {
                    setState(() {
                      _barLayout =
                          BarLayout.values[(_barLayout.index + 1) %
                              BarLayout.values.length];
                    });
                  },
                ),
              ),
            ),
          );
        case final String preset when preset.startsWith('theme:'):
          await _localPreferences.write(
            PreferenceStore.localTheme,
            preset.substring('theme:'.length),
          );
        case 'switch':
          await widget.onSwitchAccount?.call();
      }
    } catch (_) {
      if (mounted) {
        _toast('Could not open that tool. Try again.', tone: UiTone.error);
      }
    }
  }

  IcStylePreset _currentStylePreset() => switch (store.settings.theme) {
    'spatial-glass' => IcStylePreset.spatialGlass,
    'cyber-synaptic' => IcStylePreset.cyberSynaptic,
    'warm-paper' || 'intochat' => IcStylePreset.warmPaper,
    'linear-carbon' => IcStylePreset.linearCarbon,
    _ => IcStylePreset.fluentAcrylic,
  };

  Widget _menu() => PopupMenuButton<String>(
    tooltip: 'Profile',
    icon: const Icon(Icons.person_outline),
    onSelected: (value) => unawaited(_tool(value)),
    itemBuilder: (_) => [
      const PopupMenuItem(
        value: 'preferences',
        child: Text('Workspace configuration'),
      ),
      const PopupMenuItem(value: 'connections', child: Text('Connections')),
      const PopupMenuItem(value: 'compute', child: Text('Compute')),
      const PopupMenuItem(value: 'gallery', child: Text('UI Gallery')),
      const PopupMenuItem(
        value: 'smart-desktop',
        child: Text('Smart Desktop (3D)'),
      ),
      const PopupMenuItem(
        value: 'palette',
        child: Text('Command Palette (⌘K)'),
      ),
      if (_developerMode)
        const PopupMenuItem(value: 'csharp', child: Text('C# files')),
      const PopupMenuItem(value: 'new', child: Text('New workspace')),
      if (widget.onSwitchAccount != null)
        const PopupMenuItem(value: 'switch', child: Text('Switch account')),
    ],
  );
  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _space?.dispose();
    store.removeListener(_changed);
    if (widget.store == null) store.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
    navigatorKey: _navigator,
    debugShowCheckedModeBanner: false,
    theme: UiTheme.light(store.settings.theme),
    darkTheme: UiTheme.dark(store.settings.theme),
    themeMode: UiTheme.modeFor(store.settings.theme),
    builder: (context, child) => MediaQuery(
      data: MediaQuery.of(context).copyWith(
        disableAnimations:
            store.settings.reducedMotion ||
            MediaQuery.disableAnimationsOf(context),
      ),
      child: child!,
    ),
    home: CallbackShortcuts(
      bindings: <ShortcutActivator, VoidCallback>{
        const SingleActivator(LogicalKeyboardKey.keyK, control: true): () =>
            setState(() => _paletteOpen = !_paletteOpen),
        const SingleActivator(LogicalKeyboardKey.keyK, meta: true): () =>
            setState(() => _paletteOpen = !_paletteOpen),
      },
      child: Scaffold(
        body: Stack(
          fit: StackFit.expand,
          children: [
            _rootName != null && _brain != null
                ? SafeArea(
                    child: _widthHinted(
                      NeuronView(
                        key: ValueKey(_rootName),
                        kind: _renderKind,
                        name: _rootName!,
                        load: (kind, name) =>
                            widget.programmingClient!.appRequest(
                              _brain!,
                              'node?${Uri(queryParameters: {'kind': kind, 'name': name}).query}',
                            ),
                        // Reads once per change signal instead of every 2 s.
                        watch: (kind, name) => widget.programmingClient!
                            .watchNode(_brain!, kind, name)
                            .map((_) {}),
                        onAction: (event) async {
                          await widget.programmingClient!.appRequest(
                            _brain!,
                            'event',
                            body: event,
                          );
                        },
                        onActivate: (action) => unawaited(_activate(action)),
                        scenes: widget.programmingClient!.scenes(_brain!),
                        entries: (brain) =>
                            EntriesClient(widget.programmingClient!, brain),
                        localPreferences: _localPreferences,
                      ),
                    ),
                  )
                : _space == null
                ? Center(
                    child: Text(
                      _startupError?.toString() ??
                          widget.statusMessage ??
                          'Connect to your brain to begin.',
                    ),
                  )
                : NeuronSpace(
                    key: ValueKey(_brain),
                    controller: _space!,
                    load: (kind, name) => widget.programmingClient!.appRequest(
                      _brain!,
                      'node?${Uri(queryParameters: {'kind': kind, 'name': name}).query}',
                    ),
                    watch: (kind, name) => widget.programmingClient!
                        .watchNode(_brain!, kind, name)
                        .map((_) {}),
                    onAction: (event) async {
                      await widget.programmingClient!.appRequest(
                        _brain!,
                        'event',
                        body: event,
                      );
                    },
                    onActivate: (action) => unawaited(_activate(action)),
                    onRequest: _request,
                    loadIcon: _icon,
                    menu: _menu(),
                    onSettings: () => unawaited(_activate({'kind': 'settings'})),
                    entries: (brain) =>
                        EntriesClient(widget.programmingClient!, brain),
                    localPreferences: _localPreferences,
                  ),
            if (_paletteOpen)
              CommandPalette(
                onClose: () => setState(() => _paletteOpen = false),
                onLaunchApp: (appId) {
                  if (appId == 'gallery') {
                    unawaited(_tool('gallery'));
                  } else {
                    unawaited(
                      _activate({
                        'kind': 'workspace',
                        'application': appId,
                        'operation': 'open',
                      }),
                    );
                  }
                },
                onAskIno: (prompt) => unawaited(
                  _activate({
                    'kind': 'workspace',
                    'application': 'ino',
                    'operation': 'send',
                    'input': prompt,
                  }),
                ),
                onSelectPreset: (preset) =>
                    unawaited(_tool('theme:${preset.id}')),
                onCycleBarLayout: () {
                  setState(() {
                    _barLayout =
                        BarLayout.values[(_barLayout.index + 1) %
                            BarLayout.values.length];
                  });
                },
              ),
          ],
        ),
      ),
    ),
  );
}

/// A legacy `{kind: settings, brainId?, tab?}` (an older Ino card or Desktop button) as the Settings app's
/// workspace activation: open its window in that brain and ask it for the tab (the app maps an old or no name).
Map<String, dynamic> settingsActivation(Map<String, dynamic> action) => {
  'kind': 'workspace',
  if (action['brainId'] case final String brain) 'brainId': brain,
  'application': 'settings',
  'operation': 'open',
  'input': action['tab'] is String ? action['tab'] as String : '',
};
