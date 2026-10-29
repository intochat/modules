import 'package:flutter/material.dart';

import 'ui_colors.dart';

/// Elevation & ambient shadow scale unified across `UiShadows` and `IcShadow`.
/// Accepts either a base [Color] (single-stop Figma scale) or a [UiColors]
/// palette (multi-stop key + ambient elevation).
abstract final class UiShadows {
  static List<BoxShadow> xs(Object target) {
    if (target is UiColors) {
      return target.isDark
          ? const [
              BoxShadow(
                color: Color(0x40000000),
                offset: Offset(0, 1),
                blurRadius: 2,
              ),
            ]
          : const [
              BoxShadow(
                color: Color(0x0F17140F),
                offset: Offset(0, 1),
                blurRadius: 2,
              ),
              BoxShadow(
                color: Color(0x0817140F),
                offset: Offset(0, 0.5),
                blurRadius: 1,
              ),
            ];
    }
    return _shadow(target as Color, 0.06, 1, 2);
  }

  static List<BoxShadow> sm(Object target) {
    if (target is UiColors) {
      return target.isDark
          ? const [
              BoxShadow(
                color: Color(0x59000000),
                offset: Offset(0, 1),
                blurRadius: 3,
              ),
            ]
          : const [
              BoxShadow(
                color: Color(0x1217140F),
                offset: Offset(0, 1),
                blurRadius: 3,
              ),
              BoxShadow(
                color: Color(0x0A17140F),
                offset: Offset(0, 1),
                blurRadius: 2,
              ),
            ];
    }
    return _shadow(target as Color, 0.07, 1, 3);
  }

  static List<BoxShadow> md(Object target) {
    if (target is UiColors) {
      return target.isDark
          ? const [
              BoxShadow(
                color: Color(0x66000000),
                offset: Offset(0, 4),
                blurRadius: 12,
              ),
            ]
          : const [
              BoxShadow(
                color: Color(0x1417140F),
                offset: Offset(0, 4),
                blurRadius: 12,
              ),
              BoxShadow(
                color: Color(0x0D17140F),
                offset: Offset(0, 1),
                blurRadius: 3,
              ),
            ];
    }
    return _shadow(target as Color, 0.08, 4, 12);
  }

  static List<BoxShadow> lg(Object target) {
    if (target is UiColors) {
      return target.isDark
          ? const [
              BoxShadow(
                color: Color(0x73000000),
                offset: Offset(0, 12),
                blurRadius: 32,
              ),
            ]
          : const [
              BoxShadow(
                color: Color(0x1A17140F),
                offset: Offset(0, 12),
                blurRadius: 32,
              ),
              BoxShadow(
                color: Color(0x0D17140F),
                offset: Offset(0, 4),
                blurRadius: 10,
              ),
            ];
    }
    return _shadow(target as Color, 0.10, 12, 32);
  }

  static List<BoxShadow> xl(Object target) {
    if (target is UiColors) {
      return target.isDark
          ? const [
              BoxShadow(
                color: Color(0x8C000000),
                offset: Offset(0, 24),
                blurRadius: 60,
              ),
            ]
          : const [
              BoxShadow(
                color: Color(0x2417140F),
                offset: Offset(0, 24),
                blurRadius: 60,
              ),
              BoxShadow(
                color: Color(0x0F17140F),
                offset: Offset(0, 8),
                blurRadius: 20,
              ),
            ];
    }
    return _shadow(target as Color, 0.14, 24, 60);
  }

  static List<BoxShadow> _shadow(
    Color base,
    double opacity,
    double dy,
    double blur,
  ) => [
    BoxShadow(
      color: base.withValues(alpha: opacity),
      offset: Offset(0, dy),
      blurRadius: blur,
    ),
  ];
}
