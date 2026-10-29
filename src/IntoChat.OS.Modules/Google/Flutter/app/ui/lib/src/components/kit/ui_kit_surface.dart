import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_tokens.dart';

/// Base colour for Figma shadows: the strong ink, or black in dark themes.
Color uiKitShadowBase(BuildContext context) =>
    Theme.of(context).brightness == Brightness.dark
    ? Colors.black
    : UiColors.of(context).strong;

/// Figma card/surface: `surface` fill, `hairline` border, 14 radius.
final class UiKitSurface extends StatelessWidget {
  const UiKitSurface({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.radius,
  });

  final Widget child;
  final EdgeInsets padding;
  final double? radius;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: c.surface,
        borderRadius: BorderRadius.circular(
          radius ?? UiTokens.of(context).panelRadius,
        ),
        border: Border.all(color: c.hairline),
      ),
      child: Padding(padding: padding, child: child),
    );
  }
}
