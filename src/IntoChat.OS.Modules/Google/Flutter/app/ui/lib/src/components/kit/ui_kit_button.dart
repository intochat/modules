import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_icon.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Button styles. The order mirrors a C# enum by index; do not reorder.
enum UiKitButtonStyle { primary, secondary, ghost, danger, link }

/// Figma button: 48 high, radius 10, 10x18 padding, 14/700; compact is 36
/// high, radius 8, 7x11 padding, 13/700. Pressed moves down 1px; disabled
/// draws at 0.45 opacity. A link has no box: `accentCopy` text, no underline.
final class UiKitButton extends StatefulWidget {
  const UiKitButton({
    super.key,
    required this.label,
    this.onPressed,
    this.style = UiKitButtonStyle.primary,
    this.icon,
    this.compact = false,
    this.fullWidth = false,
    this.focusNode,
  });

  final String label;
  final VoidCallback? onPressed;
  final UiKitButtonStyle style;
  final String? icon;
  final bool compact;
  final bool fullWidth;

  /// Optional node for the button's own focus, e.g. a menu trigger.
  final FocusNode? focusNode;

  @override
  State<UiKitButton> createState() => _UiKitButtonState();
}

class _UiKitButtonState extends State<UiKitButton> {
  bool _hover = false, _pressed = false, _focused = false;

  bool get _enabled => widget.onPressed != null;

  void _setPressed(bool value) {
    if (_pressed != value) setState(() => _pressed = value);
  }

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final hover = _hover && _enabled;
    final (Color? fill, Color ink, Border? border) = switch (widget.style) {
      UiKitButtonStyle.primary => (
        hover ? c.accentHover : c.accent,
        c.onAccent,
        null,
      ),
      UiKitButtonStyle.secondary => (
        hover ? c.recessed : c.surface,
        c.strong,
        Border.all(color: c.inputBorder),
      ),
      UiKitButtonStyle.ghost => (
        hover ? c.recessed : Colors.transparent,
        c.body,
        null,
      ),
      // Figma `.destructive-button`: outlined, errorBg on hover.
      UiKitButtonStyle.danger => (
        hover ? c.errorBg : Colors.transparent,
        c.strong,
        Border.all(color: c.error),
      ),
      UiKitButtonStyle.link => (
        null,
        hover ? c.accentCopyHover : c.accentCopy,
        null,
      ),
    };
    final icon = widget.icon == null ? null : uiIcon(widget.icon!);
    Widget body = Row(
      mainAxisSize: widget.fullWidth ? MainAxisSize.max : MainAxisSize.min,
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (icon != null) ...[
          Icon(icon, size: uiIconSize, color: ink),
          const SizedBox(width: 8),
        ],
        Flexible(
          child: Text(
            widget.label,
            style: UiType.of(context).control
                .copyWith(color: ink, fontSize: widget.compact ? 13 : 14),
            maxLines: widget.compact ? 1 : 2,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
          ),
        ),
      ],
    );
    if (widget.style != UiKitButtonStyle.link) {
      final decorated = DecoratedBox(
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(
            widget.compact ? tokens.radiusSmall : tokens.controlRadius,
          ),
          border: _focused ? Border.all(color: c.accent, width: 2) : border,
          boxShadow: widget.style == UiKitButtonStyle.secondary
              ? UiShadows.xs(uiKitShadowBase(context))
              : null,
        ),
        child: Padding(
          padding: widget.compact
              ? const EdgeInsets.symmetric(vertical: 7, horizontal: 11)
              : const EdgeInsets.symmetric(vertical: 10, horizontal: 18),
          child: Center(widthFactor: 1, heightFactor: 1, child: body),
        ),
      );
      body = widget.compact
          // Compact is 36 high, or the whole slot when that is shorter.
          ? LayoutBuilder(
              builder: (context, constraints) => SizedBox(
                height: constraints.maxHeight < 36 ? constraints.maxHeight : 36,
                child: decorated,
              ),
            )
          : ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 48),
              child: decorated,
            );
    }
    if (widget.fullWidth) body = SizedBox(width: double.infinity, child: body);
    return Opacity(
      opacity: _enabled ? 1 : 0.45,
      child: Semantics(
        button: true,
        enabled: _enabled,
        child: FocusableActionDetector(
          focusNode: widget.focusNode,
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
            onTapDown: _enabled ? (_) => _setPressed(true) : null,
            onTapUp: _enabled ? (_) => _setPressed(false) : null,
            onTapCancel: _enabled ? () => _setPressed(false) : null,
            onTap: widget.onPressed,
            child: Transform.translate(
              offset: Offset(0, _pressed ? 1 : 0),
              child: body,
            ),
          ),
        ),
      ),
    );
  }
}
