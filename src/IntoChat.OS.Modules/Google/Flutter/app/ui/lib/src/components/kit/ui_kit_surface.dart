import 'package:flutter/material.dart';

import '../../design/tokens.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_shadows.dart';
import '../../theme/ui_tokens.dart';

/// Base colour for Figma shadows: the strong ink, or black in dark themes.
Color uiKitShadowBase(BuildContext context) =>
    Theme.of(context).brightness == Brightness.dark
    ? Colors.black
    : UiColors.of(context).strong;

/// Card/surface built on [IcAcrylicBox]: `surface` fill, `BackdropFilter` blur
/// when glassmorphic, `1px` `specularBorder` (or `hairline`), 14 radius, and
/// multi-stop [UiShadows].
final class UiKitSurface extends StatelessWidget {
  const UiKitSurface({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.radius,
    this.shadows,
    this.color,
    this.borderColor,
  });

  final Widget child;
  final EdgeInsets padding;
  final double? radius;
  final List<BoxShadow>? shadows;
  final Color? color;
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final effectiveRadius = radius ?? UiTokens.of(context).card;
    final effectiveBorder =
        borderColor ?? (c.isGlass ? c.specularBorder : c.hairline);
    return IcAcrylicBox(
      radius: effectiveRadius,
      color: color ?? c.surface,
      borderColor: effectiveBorder,
      shadows: shadows ?? UiShadows.sm(c),
      padding: padding,
      sigmaOverride: c.blurSigma,
      child: child,
    );
  }
}
