import 'dart:async';
import 'dart:math' as math;

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';

import 'space_controller.dart';
import 'space_field.dart';
import 'space_slots.dart';
import 'space_toolbar.dart';

class NeuronSpace extends StatefulWidget {
  const NeuronSpace({
    super.key,
    required this.controller,
    required this.load,
    this.watch,
    this.onAction,
    this.onActivate,
    this.onSettings,
    this.onRequest,
    this.loadIcon,
    this.menu,
    this.entries,
    this.localPreferences,
  });
  final SpaceController controller;
  final NeuronLoader load;
  final Stream<void> Function(String, String)? watch;
  final Future<void> Function(Map<String, dynamic>)? onAction;
  final ValueChanged<Map<String, dynamic>>? onActivate;
  final VoidCallback? onSettings;
  final Future<void> Function(String, String)? onRequest;
  final SphereIconLoader? loadIcon;
  final Widget? menu;
  // As the root view has them: Settings saves keys and variables over the person's session and device choices here.
  final EntriesClient Function(String brainId)? entries;
  final LocalPreferences? localPreferences;
  @override
  State<NeuronSpace> createState() => _NeuronSpaceState();
}

class _NeuronSpaceState extends State<NeuronSpace>
    with SingleTickerProviderStateMixin {
  late final AnimationController _slots = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 240),
    value: 1,
  );
  late String _focused = widget.controller.view.value.focused;
  @override
  void initState() {
    super.initState();
    widget.controller.view.addListener(_change);
  }

  void _change() {
    final view = widget.controller.view.value;
    if (view.focused != _focused &&
        view.animate &&
        !MediaQuery.disableAnimationsOf(context)) {
      _slots.forward(from: 0);
    }
    if (view.cut || MediaQuery.disableAnimationsOf(context)) _slots.value = 1;
    _focused = view.focused;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) _slots.value = 1;
  }

  @override
  void dispose() {
    widget.controller.view.removeListener(_change);
    _slots.dispose();
    super.dispose();
  }

  Widget _arrival(Widget child) => FadeTransition(
    opacity: _slots,
    child: SlideTransition(
      position: Tween(
        begin: const Offset(.03, 0),
        end: Offset.zero,
      ).animate(CurvedAnimation(parent: _slots, curve: Curves.easeOutCubic)),
      child: child,
    ),
  );
  void _run(Future<void> operation) =>
      unawaited(operation.catchError((Object _) {}));
  @override
  Widget build(BuildContext context) => ValueListenableBuilder<Object?>(
    valueListenable: widget.controller.error,
    builder: (context, problem, _) => ValueListenableBuilder<SpaceView>(
      valueListenable: widget.controller.view,
      builder: (context, view, _) => LayoutBuilder(
        builder: (context, size) {
          final hasMain = view.main != null || view.error != null;
          final x = size.maxWidth * (hasMain ? .28 : .5);
          return Stack(
            fit: StackFit.expand,
            children: [
              SpaceField(
                view: view,
                loadIcon: widget.loadIcon,
                onInput: (id, kind, x, y, payload) =>
                    _run(widget.controller.input(id, kind, x, y, payload)),
              ),
              if (view.side != null)
                Positioned(
                  left: 0,
                  top: 12,
                  bottom: 76,
                  child: _arrival(
                    SideSlot(
                      key: ValueKey(
                        'side:${view.side!.kind}:${view.side!.name}',
                      ),
                      reference: view.side!,
                      load: widget.load,
                      watch: widget.watch,
                      onAction: widget.onAction,
                      onActivate: widget.onActivate,
                      scenes: widget.controller.scenes,
                      entries: widget.entries,
                      localPreferences: widget.localPreferences,
                    ),
                  ),
                ),
              if (hasMain)
                Positioned(
                  left: math.max(
                    size.maxWidth * .44,
                    view.side == null ? 0 : 332,
                  ),
                  right: 12,
                  top: 12,
                  bottom: 76,
                  child: _arrival(
                    MainSlot(
                      key: ValueKey(
                        'main:${view.main?.kind}:${view.main?.name}:${view.error}',
                      ),
                      reference: view.main,
                      load: widget.load,
                      error: view.error,
                      watch: widget.watch,
                      onAction: widget.onAction,
                      onActivate: widget.onActivate,
                      scenes: widget.controller.scenes,
                      entries: widget.entries,
                      localPreferences: widget.localPreferences,
                    ),
                  ),
                ),
              Positioned(
                left: math.max(0, x - 120),
                top: math.max(12, (size.maxHeight - 64) / 2 - 160),
                width: 240,
                child: IgnorePointer(
                  child: Column(
                    children: [
                      Text(
                        view.title,
                        style: Theme.of(context).textTheme.titleLarge,
                        textAlign: TextAlign.center,
                      ),
                      if (view.description.isNotEmpty)
                        Text(view.description, textAlign: TextAlign.center),
                      if (view.state == 'error')
                        Semantics(
                          label: 'Needs attention',
                          child: Icon(Icons.error, color: Colors.red),
                        ),
                    ],
                  ),
                ),
              ),
              if (problem != null)
                Positioned(
                  top: 12,
                  left: 12,
                  right: 12,
                  child: Center(
                    child: Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Text('Could not update your space.'),
                            TextButton(
                              onPressed: () => _run(widget.controller.retry()),
                              child: const Text('Retry'),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              Align(
                alignment: Alignment.bottomCenter,
                child: SpaceToolbar(
                  entries: view.toolbar,
                  icon: view.icon,
                  iconRoute: _iconRoute(view.focused),
                  loadIcon: widget.loadIcon == null || view.icon == null
                      ? null
                      : (_) => widget.loadIcon!(view.focused, view.icon!),
                  onHome: () => _run(widget.controller.home()),
                  onBack: widget.controller.workspace?.path.length == 1
                      ? null
                      : () => _run(widget.controller.back()),
                  onSettings: widget.onSettings,
                  onRequest: widget.onRequest ?? (_, _) async {},
                  menu: widget.menu,
                ),
              ),
            ],
          );
        },
      ),
    ),
  );

  String? _iconRoute(String neuron) {
    final marker = neuron.indexOf('/apps/');
    if (marker <= 0) return null;
    final brain = Uri.encodeComponent(neuron.substring(0, marker));
    final app = Uri.encodeComponent(
      neuron.substring(marker + 6).split('/').first,
    );
    return '/brains/$brain/apps/$app/icon';
  }
}
