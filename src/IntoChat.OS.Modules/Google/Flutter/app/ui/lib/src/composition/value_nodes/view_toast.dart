import 'dart:async';

import 'package:flutter/material.dart';

import '../../components/kit/kit_toast.dart';

/// Toast value node: bottom-centre overlay that hides itself after 4 s,
/// taking its single action button with it.
final class ViewToast extends StatefulWidget {
  const ViewToast({
    super.key,
    required this.node,
    required this.generation,
    required this.action,
  });
  final Map node;
  final int generation;
  final VoidCallback? Function(Map node) action;

  static const lifetime = Duration(seconds: 4);

  @override
  State<ViewToast> createState() => _ViewToastState();
}

class _ViewToastState extends State<ViewToast> {
  final _portal = OverlayPortalController();
  Timer? _timer;
  // The portal stays shown; visibility only swaps its content, because
  // OverlayPortalController.show may not run during build (didUpdateWidget).
  bool _visible = false;

  @override
  void initState() {
    super.initState();
    _portal.show();
    _show();
  }

  void _show() {
    _visible = true;
    _timer?.cancel();
    _timer = Timer(ViewToast.lifetime, () {
      if (mounted) setState(() => _visible = false);
    });
  }

  static Object? _action(Map node) => (node['children'] as List? ?? const [])
      .whereType<Map>()
      .where((m) => m['kind'] == 1)
      .map((m) => m['action'])
      .firstOrNull;

  // A republished toast (same key; new generation, text or action) shows
  // again.
  @override
  void didUpdateWidget(ViewToast old) {
    super.didUpdateWidget(old);
    if (old.generation != widget.generation ||
        old.node['text'] != widget.node['text'] ||
        _action(old.node) != _action(widget.node)) {
      _show();
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => OverlayPortal(
    controller: _portal,
    overlayChildBuilder: _overlay,
    child: const SizedBox.shrink(),
  );

  Widget _overlay(BuildContext context) {
    if (!_visible) return const SizedBox.shrink();
    final button = (widget.node['children'] as List? ?? const [])
        .whereType<Map>()
        .where((m) => m['kind'] == 1)
        .firstOrNull;
    return Positioned(
      left: 0,
      right: 0,
      bottom: 22,
      child: Center(
        child: KitToastBody(
          message: widget.node['text'] as String? ?? '',
          tone: UiTone.fromWire(widget.node['tone']),
          actionLabel: button == null ? null : button['text'] as String? ?? '',
          onAction: button == null ? null : widget.action(button),
        ),
      ),
    );
  }
}
