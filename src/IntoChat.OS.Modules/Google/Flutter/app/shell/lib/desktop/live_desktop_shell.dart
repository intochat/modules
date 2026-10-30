import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'desktop_overlays.dart';
import 'os_bar.dart';
import 'smart_desktop.dart';
import 'window_frame.dart';

class _ParsedDesktopWindow {
  const _ParsedDesktopWindow({
    required this.windowId,
    required this.numericId,
    required this.appId,
    required this.title,
    required this.neuronUri,
    required this.viewportRef,
    required this.tabs,
    this.focusNode,
    this.newTabNode,
    this.starNode,
    this.pinNode,
    this.closeNode,
  });

  final String windowId;
  final int numericId;
  final String appId;
  final String title;
  final String neuronUri;
  final Map<String, dynamic> viewportRef;
  final List<Map<String, dynamic>> tabs;
  final Map<String, dynamic>? focusNode;
  final Map<String, dynamic>? newTabNode;
  final Map<String, dynamic>? starNode;
  final Map<String, dynamic>? pinNode;
  final Map<String, dynamic>? closeNode;
}

/// Live Desktop OS Shell & Floating Acrylic Window Manager backed 100% by
/// `{brain}/apps/desktop/view` and live kernel viewports.
class LiveDesktopShell extends StatefulWidget {
  const LiveDesktopShell({
    super.key,
    required this.brain,
    required this.name,
    required this.load,
    required this.watch,
    required this.onAction,
    required this.onActivate,
    required this.stylePreset,
    required this.barLayout,
    required this.paletteOpen,
    required this.onTogglePalette,
    required this.onSelectPreset,
    required this.onCycleBarLayout,
    this.onOpenCompute,
    this.scenes,
    this.entries,
    this.localPreferences,
  });

  final String brain;
  final String name;
  final NeuronLoader load;
  final Stream<void> Function(String kind, String name)? watch;
  final Future<void> Function(Map<String, dynamic>) onAction;
  final ValueChanged<Map<String, dynamic>> onActivate;
  final IcStylePreset stylePreset;
  final BarLayout barLayout;
  final bool paletteOpen;
  final VoidCallback onTogglePalette;
  final ValueChanged<IcStylePreset> onSelectPreset;
  final VoidCallback onCycleBarLayout;
  final VoidCallback? onOpenCompute;
  final SceneClient? scenes;
  final EntriesClient Function(String brainId)? entries;
  final LocalPreferences? localPreferences;

  @override
  State<LiveDesktopShell> createState() => _LiveDesktopShellState();
}

class _LiveDesktopShellState extends State<LiveDesktopShell> {
  static final _random = Random.secure();

  Map<String, dynamic>? _data;
  Object? _error;
  StreamSubscription<void>? _watchSub;
  Timer? _resubscribeTimer;
  bool _loading = false;
  bool _reloadAgain = false;

  final Map<String, DesktopWindowState> _windowStates = {};
  final List<String> _zOrder = [];
  bool _showDesktopOnly = false;

  DesktopPopoverKind _popover = DesktopPopoverKind.none;
  double _popoverAnchorX = 0;
  String? _dockMenuAppId;
  String? _barTooltipLabel;
  double _barTooltipAnchorX = 0;
  int _notificationsCount = 2;
  final Set<String> _pinnedAppIds = {
    'ino',
    'files',
    'creator',
    'gallery',
    'post',
    'settings',
  };
  bool _switcherOpen = false;
  int _switcherIndex = 0;

  @override
  void initState() {
    super.initState();
    HardwareKeyboard.instance.addHandler(_onHardwareKey);
    _reload();
    _subscribe();
  }

  @override
  void didUpdateWidget(LiveDesktopShell oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.name != widget.name || oldWidget.brain != widget.brain) {
      _data = null;
      _error = null;
      _windowStates.clear();
      _zOrder.clear();
      _reload();
      _subscribe();
    }
  }

  @override
  void dispose() {
    HardwareKeyboard.instance.removeHandler(_onHardwareKey);
    _watchSub?.cancel();
    _resubscribeTimer?.cancel();
    for (final w in _windowStates.values) {
      w.dispose();
    }
    super.dispose();
  }

  List<_ParsedDesktopWindow> _currentMruWindows() {
    final root = _data?['root'];
    if (root is! Map) return const [];
    final windows = _parseWindows(root);
    final byId = {for (final w in windows) w.windowId: w};
    final ordered = _zOrder
        .map((id) => byId[id])
        .whereType<_ParsedDesktopWindow>()
        .toList();
    return ordered.reversed.toList();
  }

  bool _onHardwareKey(KeyEvent e) {
    if (!mounted) return false;
    final kb = HardwareKeyboard.instance;

    if (e is KeyUpEvent &&
        (e.physicalKey == PhysicalKeyboardKey.altLeft ||
            e.physicalKey == PhysicalKeyboardKey.altRight ||
            e.logicalKey == LogicalKeyboardKey.altLeft ||
            e.logicalKey == LogicalKeyboardKey.altRight)) {
      if (_switcherOpen) {
        final mru = _currentMruWindows();
        if (mru.isNotEmpty) {
          final target = mru[_switcherIndex.clamp(0, mru.length - 1)];
          setState(() => _switcherOpen = false);
          _focusWindow(target);
        } else {
          setState(() => _switcherOpen = false);
        }
        return true;
      }
      return false;
    }

    if (e is KeyUpEvent) return false;

    if (kb.isAltPressed &&
        (e.physicalKey == PhysicalKeyboardKey.backquote ||
            e.logicalKey == LogicalKeyboardKey.backquote)) {
      final mru = _currentMruWindows();
      if (mru.isEmpty) return false;
      setState(() {
        _popover = DesktopPopoverKind.none;
        if (!_switcherOpen) {
          _switcherOpen = true;
          _switcherIndex = mru.length > 1 ? 1 : 0;
        } else {
          final delta = kb.isShiftPressed ? -1 : 1;
          _switcherIndex = (_switcherIndex + delta) % mru.length;
          if (_switcherIndex < 0) _switcherIndex += mru.length;
        }
      });
      return true;
    }

    if (e.logicalKey == LogicalKeyboardKey.escape && e is KeyDownEvent) {
      if (_switcherOpen) {
        setState(() => _switcherOpen = false);
        return true;
      }
      if (_popover != DesktopPopoverKind.none) {
        setState(() => _popover = DesktopPopoverKind.none);
        return true;
      }
    }

    return false;
  }

  void _togglePopover(DesktopPopoverKind kind, {double anchorX = 0, String? appId}) {
    setState(() {
      if (_popover == kind && (_dockMenuAppId == appId || kind != DesktopPopoverKind.dockMenu)) {
        _popover = DesktopPopoverKind.none;
        _dockMenuAppId = null;
      } else {
        _popover = kind;
        _popoverAnchorX = anchorX;
        _dockMenuAppId = appId;
      }
    });
  }

  void _subscribe() {
    _watchSub?.cancel();
    final watch = widget.watch;
    if (watch == null) return;
    _watchSub = watch('view', widget.name).listen(
      (_) => _reload(),
      onError: (Object _) => _scheduleReopen(),
      onDone: _scheduleReopen,
    );
  }

  void _scheduleReopen() {
    if (!mounted || _resubscribeTimer != null) return;
    _resubscribeTimer = Timer(const Duration(seconds: 2), () {
      _resubscribeTimer = null;
      if (!mounted) return;
      _reload();
      _subscribe();
    });
  }

  Future<void> _reload() async {
    if (_loading) {
      _reloadAgain = true;
      return;
    }
    _loading = true;
    try {
      final data = await widget.load('view', widget.name);
      if (mounted) {
        setState(() {
          _data = data;
          _error = null;
        });
      }
    } catch (e) {
      if (mounted && _data == null) {
        setState(() => _error = e);
      }
    } finally {
      _loading = false;
      if (mounted && _reloadAgain) {
        _reloadAgain = false;
        unawaited(_reload());
      }
    }
  }

  static String _operationId() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  int get _generation => (_data?['generation'] as num?)?.toInt() ?? 0;

  void _dispatchViewNode(Map<String, dynamic>? node) {
    if (node == null || node['enabled'] == false) return;
    final key = node['key'] as String?;
    final action = node['action'] as String?;
    if (key == null || action == null) return;
    unawaited(
      widget
          .onAction({
            'kind': 'view',
            'name': widget.name,
            'revision': _generation,
            'field': key,
            'action': action,
            'operationId': _operationId(),
          })
          .then((_) => _reload())
          .catchError((Object _) {}),
    );
  }

  static String _inferAppId(String title, Map<String, dynamic>? pinNode) {
    if (pinNode != null && pinNode['action'] is String) {
      final act = pinNode['action'] as String;
      final colon = act.indexOf(':');
      if (colon > 0 && colon + 1 < act.length) {
        return act.substring(colon + 1);
      }
    }
    final lower = title.toLowerCase();
    if (lower.contains('ino')) return 'ino';
    if (lower.contains('gallery')) return 'gallery';
    if (lower.contains('creator')) return 'creator';
    if (lower.contains('file')) return 'files';
    if (lower.contains('post')) return 'post';
    if (lower.contains('setting')) return 'settings';
    if (lower.contains('circle')) return 'circleroom';
    return lower.replaceAll(RegExp(r'[^a-z0-9]'), '');
  }

  List<_ParsedDesktopWindow> _parseWindows(Map root) {
    final topChildren = (root['children'] as List? ?? const [])
        .whereType<Map>()
        .toList();
    if (topChildren.isEmpty) return const [];
    final windowsNode = topChildren.first;
    if (windowsNode['key'] != 'windows') return const [];

    final frameNodes = <Map>[];
    void collectFrames(Map node) {
      final key = node['key'] as String? ?? '';
      if (key.startsWith('frame-')) {
        frameNodes.add(node);
        return;
      }
      for (final child in (node['children'] as List? ?? const []).whereType<Map>()) {
        collectFrames(child);
      }
    }

    collectFrames(windowsNode);

    final result = <_ParsedDesktopWindow>[];
    for (var i = 0; i < frameNodes.length; i++) {
      final frame = frameNodes[i];
      final frameKey = frame['key'] as String;
      final winId = frameKey.substring('frame-'.length);
      final numericId =
          int.tryParse(winId.replaceAll(RegExp(r'[^0-9]'), '')) ?? (i + 1);
      final children = (frame['children'] as List? ?? const [])
          .whereType<Map>()
          .toList();

      Map<String, dynamic>? focusNode;
      Map<String, dynamic>? newTabNode;
      Map<String, dynamic>? starNode;
      Map<String, dynamic>? pinNode;
      Map<String, dynamic>? closeNode;
      var title = winId;

      if (children.isNotEmpty) {
        final chromeChildren = (children[0]['children'] as List? ?? const [])
            .whereType<Map>();
        for (final btn in chromeChildren) {
          final k = btn['key'] as String? ?? '';
          final map = Map<String, dynamic>.from(btn);
          if (k.startsWith('focus-')) {
            focusNode = map;
            title = (btn['text'] as String?) ?? title;
          } else if (k.startsWith('newtab-')) {
            newTabNode = map;
          } else if (k.startsWith('star-')) {
            starNode = map;
          } else if (k.startsWith('pin-') || k.startsWith('unpin-')) {
            pinNode = map;
          } else if (k.startsWith('close-')) {
            closeNode = map;
          }
        }
      }

      final tabs = <Map<String, dynamic>>[];
      if (children.length > 1) {
        for (final t
            in (children[1]['children'] as List? ?? const []).whereType<Map>()) {
          tabs.add(Map<String, dynamic>.from(t));
        }
      }

      Map<String, dynamic>? viewportRef;
      for (final c in children) {
        if (c['kind'] == 5 && c['reference'] is Map) {
          viewportRef = Map<String, dynamic>.from(c['reference'] as Map);
        }
      }
      if (viewportRef == null) continue;

      final appId = _inferAppId(title, pinNode);
      result.add(
        _ParsedDesktopWindow(
          windowId: winId,
          numericId: numericId,
          appId: appId,
          title: title,
          neuronUri: 'neuron://${widget.brain}/apps/$appId',
          viewportRef: viewportRef,
          tabs: tabs,
          focusNode: focusNode,
          newTabNode: newTabNode,
          starNode: starNode,
          pinNode: pinNode,
          closeNode: closeNode,
        ),
      );
    }
    return result;
  }

  Map<String, Map<String, dynamic>> _parseDockButtons(Map root) {
    final buttons = <String, Map<String, dynamic>>{};
    void walk(Map node) {
      final key = node['key'] as String? ?? '';
      if (node['kind'] == 1 && key.isNotEmpty) {
        buttons[key] = Map<String, dynamic>.from(node);
      }
      for (final child in (node['children'] as List? ?? const []).whereType<Map>()) {
        walk(child);
      }
    }

    final topChildren = (root['children'] as List? ?? const [])
        .whereType<Map>()
        .toList();
    for (final child in topChildren) {
      if (child['key'] == 'dock') walk(child);
    }
    return buttons;
  }

  Rect _initialWindowBounds(int index, Size desk) {
    final w = (desk.width * 0.64).clamp(680.0, 980.0);
    final h = (desk.height * 0.78).clamp(460.0, 680.0);
    // Position first window slightly right of center so SmartDesktop left cards remain visible.
    final baseLeft = desk.width > 1180
        ? (desk.width - w - 36).clamp(380.0, desk.width - w - 24)
        : ((desk.width - w) / 2).clamp(16.0, max(16.0, desk.width - w - 16));
    final baseTop = ((desk.height - h) / 2 - 12).clamp(
      18.0,
      max(18.0, desk.height - h - 18),
    );
    final offset = (index % 5) * 28.0;
    return Rect.fromLTWH(
      (baseLeft - offset).clamp(16.0, max(16.0, desk.width - w - 16)),
      (baseTop + offset).clamp(16.0, max(16.0, desk.height - h - 16)),
      w,
      h,
    );
  }

  void _syncWindowStates(List<_ParsedDesktopWindow> parsed, Size desk) {
    final activeIds = {for (final w in parsed) w.windowId};
    _windowStates.removeWhere((id, state) {
      final gone = !activeIds.contains(id);
      if (gone) state.dispose();
      return gone;
    });
    _zOrder.removeWhere((id) => !activeIds.contains(id));

    for (var i = 0; i < parsed.length; i++) {
      final spec = parsed[i];
      final existing = _windowStates[spec.windowId];
      if (existing == null) {
        _windowStates[spec.windowId] = DesktopWindowState(
          id: spec.numericId,
          appId: spec.appId,
          title: spec.title,
          neuronUri: spec.neuronUri,
          bounds: _initialWindowBounds(i, desk),
        );
        _zOrder.add(spec.windowId);
      } else {
        existing.title = spec.title;
        if (!_zOrder.contains(spec.windowId)) {
          _zOrder.add(spec.windowId);
        }
      }
    }
  }

  void _focusWindow(_ParsedDesktopWindow spec) {
    final state = _windowStates[spec.windowId];
    if (state != null && state.minimized) {
      state.setMinimized(false);
    }
    setState(() {
      _showDesktopOnly = false;
      _zOrder.remove(spec.windowId);
      _zOrder.add(spec.windowId);
    });
    _dispatchViewNode(spec.focusNode);
  }

  void _launchOrFocusApp(
    String appId,
    List<_ParsedDesktopWindow> windows,
    Map<String, Map<String, dynamic>> dockButtons,
  ) {
    setState(() => _showDesktopOnly = false);
    for (final w in windows.reversed) {
      if (w.appId == appId) {
        _focusWindow(w);
        return;
      }
    }
    final dockBtn = dockButtons['dock-$appId'];
    if (dockBtn != null) {
      _dispatchViewNode(dockBtn);
      return;
    }
    if (appId == 'settings') {
      widget.onActivate({'kind': 'settings', 'brainId': widget.brain});
      return;
    }
    widget.onActivate({
      'brainId': widget.brain,
      'kind': 'workspace',
      'application': appId,
      'operation': 'open',
    });
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;
    final root = data?['root'];
    final isDesktopTree =
        root is Map &&
        root['key'] == 'desktop' &&
        root['kind'] == 2 &&
        (root['children'] as List? ?? const []).any(
          (c) => c is Map && c['key'] == 'windows',
        );

    if (data == null || !isDesktopTree) {
      if (_error != null && data == null) {
        return Center(child: Text('$_error'));
      }
      return NeuronView(
        key: ValueKey('fallback:${widget.name}'),
        kind: 'view',
        name: widget.name,
        load: widget.load,
        watch: widget.watch,
        onAction: widget.onAction,
        onActivate: widget.onActivate,
        scenes: widget.scenes,
        entries: widget.entries,
        localPreferences: widget.localPreferences,
      );
    }

    final c = IcTheme.of(context);
    final windows = _parseWindows(root);
    final dockButtons = _parseDockButtons(root);

    return LayoutBuilder(
      builder: (context, constraints) {
        final size = constraints.biggest;
        final focusMode = size.width < 1024 || size.height < 640;
        final desk = Size(
          size.width,
          max(320.0, size.height - kOsBarHeight - 8),
        );
        _syncWindowStates(windows, desk);

        final byId = {for (final w in windows) w.windowId: w};
        final orderedWindows = _zOrder
            .map((id) => byId[id])
            .whereType<_ParsedDesktopWindow>()
            .toList();
        final visibleWindows = _showDesktopOnly
            ? const <_ParsedDesktopWindow>[]
            : [
                for (final w in orderedWindows)
                  if (!(_windowStates[w.windowId]?.minimized ?? false)) w,
              ];
        final topWindow = visibleWindows.isEmpty ? null : visibleWindows.last;
        final openAppIds = {for (final w in windows) w.appId};

        return DecoratedBox(
          decoration: BoxDecoration(color: c.surfacePage),
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              // 1. Ambient glass radial glows matching E:\intochat\design\apps\os\lib\shell\desktop.dart
              if (c.isGlass)
                Positioned.fill(
                  child: IgnorePointer(
                    child: DecoratedBox(
                      decoration: BoxDecoration(
                        gradient: RadialGradient(
                          center: const Alignment(-0.6, -0.7),
                          radius: 0.9,
                          colors: [
                            c.accent.withValues(alpha: c.isDark ? 0.16 : 0.08),
                            const Color(0x00000000),
                          ],
                        ),
                      ),
                      child: DecoratedBox(
                        decoration: BoxDecoration(
                          gradient: RadialGradient(
                            center: const Alignment(0.7, 0.6),
                            radius: 0.85,
                            colors: [
                              c.secondaryGlow.withValues(
                                alpha: c.isDark ? 0.12 : 0.06,
                              ),
                              const Color(0x00000000),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),

              // 2. SmartDesktop (Interactive 3D Orbital Neural Graph + Glass Smart Cards)
              Positioned(
                left: 0,
                top: 0,
                width: desk.width,
                height: desk.height,
                child: SmartDesktop(
                  hasOpenWindows: visibleWindows.isNotEmpty,
                  stylePreset: widget.stylePreset,
                  barLayout: widget.barLayout,
                  onLaunchApp: (appId) =>
                      _launchOrFocusApp(appId, windows, dockButtons),
                  onOpenPalette: widget.onTogglePalette,
                  onSelectPreset: widget.onSelectPreset,
                  onCycleBarLayout: widget.onCycleBarLayout,
                ),
              ),

              // 3. Free-Floating, Draggable, 8-Way Resizable Acrylic Windows
              Positioned(
                left: 0,
                top: 0,
                width: desk.width,
                height: desk.height,
                child: Stack(
                  clipBehavior: Clip.none,
                  children: [
                    for (final spec in visibleWindows)
                      WindowFrame(
                        key: ValueKey('window:${spec.windowId}'),
                        window: _windowStates[spec.windowId]!,
                        focused: spec == topWindow,
                        forceZoom: focusMode,
                        desk: desk,
                        tabs: [
                          for (final t in spec.tabs)
                            DesktopTabSpec(
                              key: '${t['key']}',
                              label: '${t['text'] ?? ''}',
                              selected: t['selected'] == true,
                              onTap: () => _dispatchViewNode(t),
                            ),
                        ],
                        onFocus: () => _focusWindow(spec),
                        onClose: () => _dispatchViewNode(spec.closeNode),
                        onMinimize: () {
                          final st = _windowStates[spec.windowId];
                          if (st != null) {
                            setState(() => st.setMinimized(!st.minimized));
                          }
                        },
                        onNewTab: spec.newTabNode != null
                            ? () => _dispatchViewNode(spec.newTabNode)
                            : null,
                        onFavourite: spec.starNode != null
                            ? () => _dispatchViewNode(spec.starNode)
                            : null,
                        isFavourite:
                            (spec.starNode?['text'] as String? ?? '')
                                .toLowerCase()
                                .contains('remove'),
                        onPin: spec.pinNode != null
                            ? () => _dispatchViewNode(spec.pinNode)
                            : null,
                        isPinned:
                            (spec.pinNode?['key'] as String? ?? '')
                                .startsWith('unpin-'),
                        child: spec.appId == 'gallery'
                            ? const UiGalleryScreen()
                            : NeuronView(
                                key: ValueKey(
                                  'viewport:${spec.viewportRef['name']}',
                                ),
                                kind: spec.viewportRef['kind'] as String,
                                name: spec.viewportRef['name'] as String,
                                load: widget.load,
                                watch: widget.watch,
                                onAction: widget.onAction,
                                onActivate: widget.onActivate,
                                scenes: widget.scenes,
                                entries: widget.entries,
                                localPreferences: widget.localPreferences,
                              ),
                      ),
                  ],
                ),
              ),

              // 4. Adaptive 36px Glass OS Bar & Dock
              Positioned(
                left: 0,
                right: 0,
                bottom: widget.barLayout == BarLayout.classicBottom ? 0 : 6,
                height: kOsBarHeight,
                child: OsBar(
                  barLayout: widget.barLayout,
                  stylePreset: widget.stylePreset,
                  openAppIds: openAppIds,
                  focusedAppId: topWindow?.appId,
                  paletteOpen: widget.paletteOpen,
                  showingDesktop: _showDesktopOnly || visibleWindows.isEmpty,
                  notifications: _notificationsCount,
                  onTogglePalette: () {
                    setState(() => _popover = DesktopPopoverKind.none);
                    widget.onTogglePalette();
                  },
                  onToggleShowDesktop: () {
                    setState(() {
                      _popover = DesktopPopoverKind.none;
                      _showDesktopOnly = !_showDesktopOnly;
                    });
                  },
                  onCycleStylePreset: () {
                    final nextIdx =
                        (IcStylePreset.values.indexOf(widget.stylePreset) + 1) %
                        IcStylePreset.values.length;
                    widget.onSelectPreset(IcStylePreset.values[nextIdx]);
                  },
                  onLaunchApp: (appId) {
                    setState(() => _popover = DesktopPopoverKind.none);
                    _launchOrFocusApp(appId, windows, dockButtons);
                  },
                  onSecondaryTapApp: (appId, globalPos) => _togglePopover(
                    DesktopPopoverKind.dockMenu,
                    anchorX: globalPos.dx,
                    appId: appId,
                  ),
                  onHoverTooltip: (label, anchorX) {
                    if (_barTooltipLabel != label || _barTooltipAnchorX != anchorX) {
                      setState(() {
                        _barTooltipLabel = label;
                        _barTooltipAnchorX = anchorX;
                      });
                    }
                  },
                  onOpenSettings: () {
                    setState(() => _popover = DesktopPopoverKind.none);
                    _launchOrFocusApp('settings', windows, dockButtons);
                  },
                  onOpenCompute: () =>
                      _togglePopover(DesktopPopoverKind.tokens),
                  onOpenNotifications: () =>
                      _togglePopover(DesktopPopoverKind.notifications),
                  onOpenClock: () => _togglePopover(DesktopPopoverKind.clock),
                ),
              ),

              // 5. Unclipped Hover Tooltip Above OsBar
              Positioned.fill(
                child: IgnorePointer(
                  child: BarTooltip(
                    screen: size,
                    barLayout: widget.barLayout,
                    label: _popover == DesktopPopoverKind.none
                        ? _barTooltipLabel
                        : null,
                    anchorX: _barTooltipAnchorX,
                  ),
                ),
              ),

              // 6. OS Bar Popovers (Clock, Tokens & Compute, Notifications, Right-Click Dock Menu)
              if (_popover != DesktopPopoverKind.none) ...[
                Positioned(
                  left: 0,
                  right: 0,
                  top: 0,
                  bottom: kOsBarHeight,
                  child: GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onTap: () =>
                        setState(() => _popover = DesktopPopoverKind.none),
                  ),
                ),
                DesktopBarPopover(
                  kind: _popover,
                  screen: size,
                  barLayout: widget.barLayout,
                  anchorX: _popoverAnchorX,
                  dockAppId: _dockMenuAppId,
                  notificationsCount: _notificationsCount,
                  pinnedAppIds: _pinnedAppIds,
                  windows: [
                    for (final w in orderedWindows.reversed)
                      DesktopWindowSummary(
                        windowId: w.windowId,
                        numericId: w.numericId,
                        appId: w.appId,
                        title: w.title,
                        minimized:
                            _windowStates[w.windowId]?.minimized ?? false,
                        isPinned: _pinnedAppIds.contains(w.appId),
                      ),
                  ],
                  onClose: () =>
                      setState(() => _popover = DesktopPopoverKind.none),
                  onOpenSettings: () =>
                      _launchOrFocusApp('settings', windows, dockButtons),
                  onOpenComputeSettings: () {
                    if (widget.onOpenCompute != null) {
                      widget.onOpenCompute!();
                    } else {
                      _launchOrFocusApp('settings', windows, dockButtons);
                    }
                  },
                  onClearNotifications: () =>
                      setState(() => _notificationsCount = 0),
                  onFocusWindow: (winId) {
                    final target = byId[winId];
                    if (target != null) _focusWindow(target);
                  },
                  onNewWindowForApp: (appId) {
                    final dockBtn = dockButtons['dock-$appId'];
                    if (dockBtn != null) {
                      _dispatchViewNode(dockBtn);
                    } else {
                      _launchOrFocusApp(appId, windows, dockButtons);
                    }
                  },
                  onTogglePinApp: (appId) {
                    setState(() {
                      if (_pinnedAppIds.contains(appId)) {
                        _pinnedAppIds.remove(appId);
                      } else {
                        _pinnedAppIds.add(appId);
                      }
                    });
                    for (final w in windows) {
                      if (w.appId == appId && w.pinNode != null) {
                        _dispatchViewNode(w.pinNode);
                        break;
                      }
                    }
                  },
                  onQuitApp: (appId) {
                    for (final w in windows) {
                      if (w.appId == appId && w.closeNode != null) {
                        _dispatchViewNode(w.closeNode);
                      }
                    }
                  },
                ),
              ],

              // 7. Alt+` (⌥`) Visual MRU Window Switcher
              if (_switcherOpen && orderedWindows.isNotEmpty)
                Positioned.fill(
                  child: AltBacktickWindowSwitcher(
                    windows: [
                      for (final w in orderedWindows.reversed)
                        DesktopWindowSummary(
                          windowId: w.windowId,
                          numericId: w.numericId,
                          appId: w.appId,
                          title: w.title,
                          minimized:
                              _windowStates[w.windowId]?.minimized ?? false,
                          isPinned: _pinnedAppIds.contains(w.appId),
                        ),
                    ],
                    selectedIndex: _switcherIndex.clamp(
                      0,
                      orderedWindows.length - 1,
                    ),
                    onSelectWindow: (summary) {
                      setState(() => _switcherOpen = false);
                      final target = byId[summary.windowId];
                      if (target != null) _focusWindow(target);
                    },
                    onCancel: () => setState(() => _switcherOpen = false),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}
