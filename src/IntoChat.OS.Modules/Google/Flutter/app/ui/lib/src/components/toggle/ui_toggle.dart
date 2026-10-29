import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';

/// A labelled on/off switch backed by a server `toggle` neuron. The Figma
/// track is 40x22; the whole row toggles.
final class UiToggle extends StatelessWidget {
  const UiToggle({
    super.key,
    required this.label,
    required this.on,
    this.onChanged,
  });

  final String label;
  final bool on;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final enabled = onChanged != null;
    void flip() => onChanged?.call(!on);
    return Semantics(
      toggled: on,
      enabled: enabled,
      label: label,
      excludeSemantics: true,
      onTap: enabled ? flip : null,
      child: FocusableActionDetector(
        enabled: enabled,
        mouseCursor: enabled
            ? SystemMouseCursors.click
            : SystemMouseCursors.basic,
        actions: {
          ActivateIntent: CallbackAction<ActivateIntent>(
            onInvoke: (_) => flip(),
          ),
        },
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: enabled ? flip : null,
          child: Opacity(
            opacity: enabled ? 1 : 0.45,
            child: Row(
              children: [
                Expanded(child: Text(label, style: UiType.of(context).body)),
                const SizedBox(width: 12),
                AnimatedContainer(
                  key: const ValueKey('ui_toggle_track'),
                  duration: tokens.quick,
                  curve: tokens.ease,
                  width: 40,
                  height: 22,
                  padding: const EdgeInsets.all(2),
                  alignment: on ? Alignment.centerRight : Alignment.centerLeft,
                  decoration: BoxDecoration(
                    color: on ? c.accent : c.hairline,
                    borderRadius: BorderRadius.circular(tokens.pill),
                  ),
                  child: Container(
                    width: 18,
                    height: 18,
                    decoration: BoxDecoration(
                      color: on ? c.onAccent : c.surface,
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
