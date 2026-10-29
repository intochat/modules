import 'package:flutter/material.dart';

import '../../design/keys.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_icon.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'ui_kit_surface.dart';

/// Button styles. The order mirrors a C# enum by index; do not reorder.
enum UiKitButtonStyle { primary, secondary, ghost, danger, link }

/// Desktop-dense button unified with `Btn` (`controls.dart`):
/// 36px standard / 30px compact height, 14px/10px horizontal padding,
/// 10px radius, 13px w600 Inter label, 15px Lucide icon, 1px press translate,
/// and [SelectionRing] focus.
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
    final targetHeight = widget.compact
        ? tokens.buttonSmallHeight
        : tokens.buttonHeight;
    final hPad = widget.compact ? 10.0 : 14.0;

    Widget body = Row(
      mainAxisSize: widget.fullWidth ? MainAxisSize.max : MainAxisSize.min,
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (icon != null) ...[
          Icon(icon, size: 15, color: ink),
          const SizedBox(width: 6),
        ],
        Flexible(
          child: Text(
            widget.label,
            style: UiType.textStyle(
              13,
              weight: FontWeight.w600,
              color: ink,
            ),
            maxLines: 1,
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
          borderRadius: BorderRadius.circular(tokens.button),
          border: _focused ? Border.all(color: c.accent, width: 1.5) : border,
          boxShadow: widget.style == UiKitButtonStyle.secondary
              ? UiShadows.xs(uiKitShadowBase(context))
              : null,
        ),
        child: Padding(
          padding: EdgeInsets.symmetric(horizontal: hPad),
          child: Center(widthFactor: 1, heightFactor: 1, child: body),
        ),
      );
      body = LayoutBuilder(
        builder: (context, constraints) {
          final h = constraints.maxHeight < targetHeight
              ? constraints.maxHeight
              : targetHeight;
          return SizedBox(height: h, child: decorated);
        },
      );
    }
    if (widget.fullWidth) body = SizedBox(width: double.infinity, child: body);

    return Stop(
      onActivate: widget.onPressed,
      isDefault: widget.style == UiKitButtonStyle.primary,
      builder: (context, stopView) => Opacity(
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
              child: SelectionRing(
                on: _focused || stopView.selected,
                radius: tokens.button,
                child: Transform.translate(
                  offset: Offset(0, _pressed ? 1 : 0),
                  child: body,
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
