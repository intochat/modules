import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Desktop-dense toggle switch unified with `IcSwitch` (`controls.dart`):
/// 36×20px pill track, 14px thumb, optional lock icon. A null [onChanged] or
/// [locked] = true disables it.
final class UiKitSwitch extends StatelessWidget {
  const UiKitSwitch({
    super.key,
    required this.value,
    required this.onChanged,
    this.semanticLabel,
    this.locked = false,
  });

  final bool value;
  final ValueChanged<bool>? onChanged;
  final String? semanticLabel;
  final bool locked;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final ease = tokens.ease;
    final duration = tokens.fast;
    final enabled = onChanged != null && !locked;
    final track = TweenAnimationBuilder<Color?>(
      tween: ColorTween(end: value ? c.accent : c.recessed),
      duration: duration,
      curve: ease,
      builder: (context, color, _) => DecoratedBox(
        key: const ValueKey('ui_kit_switch_track'),
        decoration: BoxDecoration(
          color: color,
          borderRadius: BorderRadius.circular(tokens.pill),
          border: Border.all(color: value ? c.accent : c.inputBorder),
        ),
        child: SizedBox(
          width: 36,
          height: 20,
          child: Stack(
            children: [
              AnimatedPositioned(
                duration: duration,
                curve: ease,
                top: 2,
                left: value ? 18 : 2,
                child: DecoratedBox(
                  key: const ValueKey('ui_kit_switch_thumb'),
                  decoration: BoxDecoration(
                    color: value ? c.onAccent : c.borderStrong,
                    shape: BoxShape.circle,
                    boxShadow: UiShadows.sm(uiKitShadowBase(context)),
                  ),
                  child: const SizedBox(width: 14, height: 14),
                ),
              ),
            ],
          ),
        ),
      ),
    );
    return Semantics(
      toggled: value,
      enabled: enabled,
      label: semanticLabel,
      child: FocusableActionDetector(
        enabled: enabled,
        mouseCursor: enabled
            ? SystemMouseCursors.click
            : SystemMouseCursors.basic,
        actions: {
          ActivateIntent: CallbackAction<ActivateIntent>(
            onInvoke: (_) => onChanged?.call(!value),
          ),
        },
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: enabled ? () => onChanged!(!value) : null,
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (locked) ...[
                Icon(LucideIcons.lock, size: 13, color: c.muted),
                const SizedBox(width: 6),
              ],
              Opacity(opacity: enabled ? 1 : 0.45, child: track),
            ],
          ),
        ),
      ),
    );
  }
}
