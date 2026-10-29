import 'dart:async';

import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Status tone. Indices equal the C# `UiTone` wire ints (Neutral 0, Done 1,
/// Working 2, Error 3); do not reorder.
enum UiTone {
  neutral,
  done,
  working,
  error;

  /// The tone for a wire int; anything unknown is [neutral].
  static UiTone fromWire(Object? value) =>
      value is int && value > 0 && value < values.length
      ? values[value]
      : neutral;
}

/// How long a kit toast stays up; matches ViewToast.
const kitToastLifetime = Duration(seconds: 4);

OverlayEntry? _current;
Timer? _timer;

void _hide() {
  _timer?.cancel();
  _timer = null;
  final entry = _current;
  _current = null;
  // A live _current is still in its overlay: teardown clears it first.
  entry?.remove();
}

/// Shows the Figma `.toast` over the root overlay of [context] (or [context]
/// itself when it is an [Overlay]'s, e.g. a navigator's `overlay.context`),
/// replacing any toast already up. It hides after [kitToastLifetime], when
/// its action runs, or when its overlay goes away.
void showKitToast(
  BuildContext context,
  String message, {
  UiTone tone = UiTone.neutral,
  String? actionLabel,
  VoidCallback? onAction,
}) {
  final overlay =
      Overlay.maybeOf(context, rootOverlay: true) ??
      switch (context) {
        StatefulElement(:final OverlayState state) => state,
        _ => null,
      };
  if (overlay == null) return;
  _hide();
  late final OverlayEntry entry;
  entry = OverlayEntry(
    builder: (context) => _Lifetime(
      // The overlay unmounting (app torn down) clears the globals.
      onDispose: () {
        if (identical(_current, entry)) {
          _timer?.cancel();
          _timer = null;
          _current = null;
        }
      },
      child: Positioned(
        left: 0,
        right: 0,
        bottom: 22,
        child: Center(
          child: KitToastBody(
            message: message,
            tone: tone,
            actionLabel: actionLabel,
            onAction: onAction == null
                ? null
                : () {
                    _hide();
                    onAction();
                  },
          ),
        ),
      ),
    ),
  );
  _current = entry;
  overlay.insert(entry);
  _timer = Timer(kitToastLifetime, () {
    if (identical(_current, entry)) _hide();
  });
}

final class _Lifetime extends StatefulWidget {
  const _Lifetime({required this.onDispose, required this.child});
  final VoidCallback onDispose;
  final Widget child;
  @override
  State<_Lifetime> createState() => _LifetimeState();
}

class _LifetimeState extends State<_Lifetime> {
  @override
  void dispose() {
    widget.onDispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}

/// The Figma `.toast` box: radius controlRadius (10), padding 10x12,
/// `strong` fill, `page` text in [UiType.toast], shadow-lg. A non-neutral
/// tone adds a leading status dot. Shared by [showKitToast] and ViewToast.
final class KitToastBody extends StatelessWidget {
  const KitToastBody({
    super.key,
    required this.message,
    this.tone = UiTone.neutral,
    this.actionLabel,
    this.onAction,
  });
  final String message;
  final UiTone tone;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final style = UiType.of(context).toast.copyWith(color: c.page);
    final dot = switch (tone) {
      UiTone.neutral => null,
      UiTone.done => c.done,
      UiTone.working => c.working,
      UiTone.error => c.error,
    };
    final label = actionLabel;
    return Material(
      type: MaterialType.transparency,
      child: Semantics(
        liveRegion: true,
        child: Container(
          key: const ValueKey('ui_kit_toast'),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          decoration: BoxDecoration(
            color: c.strong,
            borderRadius: BorderRadius.circular(tokens.controlRadius),
            boxShadow: UiShadows.lg(uiKitShadowBase(context)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (dot != null) ...[
                Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(color: dot, shape: BoxShape.circle),
                ),
                const SizedBox(width: 8),
              ],
              Flexible(child: Text(message, style: style)),
              if (label != null) ...[
                const SizedBox(width: 16),
                TextButton(
                  onPressed: onAction,
                  style: ButtonStyle(
                    padding: const WidgetStatePropertyAll(EdgeInsets.zero),
                    minimumSize: const WidgetStatePropertyAll(Size.zero),
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    foregroundColor: WidgetStatePropertyAll(c.page),
                    overlayColor: const WidgetStatePropertyAll(
                      Colors.transparent,
                    ),
                    textStyle: WidgetStatePropertyAll(
                      style.copyWith(
                        fontWeight: FontWeight.w700,
                        decoration: TextDecoration.underline,
                        decorationColor: c.page,
                      ),
                    ),
                  ),
                  child: Text(label),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
