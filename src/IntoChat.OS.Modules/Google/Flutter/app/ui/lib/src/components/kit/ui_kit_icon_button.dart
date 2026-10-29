import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_icon.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Icon button looks. Figma: `.message-actions button` (ghost), `.send-button`
/// (primary), `.send-button.stop` (strong) and `.jump-latest-button`
/// (floating).
enum UiKitIconButtonStyle { ghost, primary, strong, floating }

/// A square icon-only button with a tooltip that is also its semantic label.
final class UiKitIconButton extends StatefulWidget {
  const UiKitIconButton({
    super.key,
    required this.icon,
    required this.label,
    this.onPressed,
    this.style = UiKitIconButtonStyle.ghost,
    this.size = 28,
    this.iconSize = 14,
  });

  final String icon;
  final String label;
  final VoidCallback? onPressed;
  final UiKitIconButtonStyle style;
  final double size, iconSize;

  @override
  State<UiKitIconButton> createState() => _UiKitIconButtonState();
}

class _UiKitIconButtonState extends State<UiKitIconButton> {
  bool _hover = false, _focused = false;

  bool get _enabled => widget.onPressed != null;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final hover = _hover && _enabled;
    final (Color fill, Color ink) = switch (widget.style) {
      UiKitIconButtonStyle.ghost => (
        hover ? c.recessed : Colors.transparent,
        hover ? c.strong : c.muted,
      ),
      UiKitIconButtonStyle.primary => (
        hover ? c.accentHover : c.accent,
        c.onAccent,
      ),
      UiKitIconButtonStyle.strong => (c.strong, c.page),
      UiKitIconButtonStyle.floating => (
        hover ? c.recessed : c.surface,
        c.strong,
      ),
    };
    final floating = widget.style == UiKitIconButtonStyle.floating;
    final radius = switch (widget.style) {
      UiKitIconButtonStyle.ghost => tokens.radiusSmall * 0.75,
      UiKitIconButtonStyle.floating => tokens.pill,
      _ => tokens.controlRadius,
    };
    return Tooltip(
      message: widget.label,
      child: Semantics(
        button: true,
        enabled: _enabled,
        label: widget.label,
        excludeSemantics: true,
        child: Opacity(
          opacity: _enabled ? 1 : 0.45,
          child: FocusableActionDetector(
            enabled: _enabled,
            mouseCursor: _enabled
                ? SystemMouseCursors.click
                : SystemMouseCursors.basic,
            onShowHoverHighlight: (v) => setState(() => _hover = v),
            onShowFocusHighlight: (v) => setState(() => _focused = v),
            actions: {
              ActivateIntent: CallbackAction<ActivateIntent>(
                onInvoke: (_) => widget.onPressed?.call(),
              ),
            },
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: widget.onPressed,
              child: Container(
                width: widget.size,
                height: widget.size,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: fill,
                  borderRadius: BorderRadius.circular(radius),
                  border: _focused
                      ? Border.all(color: c.accent, width: 2)
                      : floating
                      ? Border.all(color: c.hairline)
                      : null,
                  boxShadow: floating
                      ? UiShadows.md(uiKitShadowBase(context))
                      : null,
                ),
                child: Icon(
                  uiIcon(widget.icon),
                  size: widget.iconSize,
                  color: ink,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
