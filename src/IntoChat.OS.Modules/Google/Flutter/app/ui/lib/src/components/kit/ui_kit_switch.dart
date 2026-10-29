import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Figma `.toggle`: a 40x22 pill, 3 px padding, `inputBorder` track off and
/// `accent` on, with a 16x16 `surface` thumb that moves 18 px over 200 ms.
/// A null [onChanged] disables it.
final class UiKitSwitch extends StatelessWidget {
  const UiKitSwitch({
    super.key,
    required this.value,
    required this.onChanged,
    this.semanticLabel,
  });

  final bool value;
  final ValueChanged<bool>? onChanged;
  final String? semanticLabel;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final ease = UiTokens.of(context).ease;
    const duration = Duration(milliseconds: 200);
    final enabled = onChanged != null;
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
          child: Opacity(
            opacity: enabled ? 1 : 0.45,
            child: TweenAnimationBuilder<Color?>(
              tween: ColorTween(end: value ? c.accent : c.inputBorder),
              duration: duration,
              curve: ease,
              builder: (context, color, _) => DecoratedBox(
                key: const ValueKey('ui_kit_switch_track'),
                decoration: BoxDecoration(
                  color: color,
                  borderRadius: BorderRadius.circular(
                    UiTokens.of(context).pill,
                  ),
                ),
                child: SizedBox(
                  width: 40,
                  height: 22,
                  child: Stack(
                    children: [
                      AnimatedPositioned(
                        duration: duration,
                        curve: ease,
                        top: 3,
                        left: value ? 21 : 3,
                        child: DecoratedBox(
                          key: const ValueKey('ui_kit_switch_thumb'),
                          decoration: BoxDecoration(
                            color: c.surface,
                            shape: BoxShape.circle,
                            boxShadow: UiShadows.sm(uiKitShadowBase(context)),
                          ),
                          child: const SizedBox(width: 16, height: 16),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
