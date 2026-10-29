import 'package:flutter/material.dart';

/// Figma elevation scale. [strongBase] is the palette's strong ink (or black
/// in dark themes); opacity follows the prototype's `--shadow-*` tokens.
abstract final class UiShadows {
  static List<BoxShadow> xs(Color strongBase) =>
      _shadow(strongBase, 0.06, 1, 2);
  static List<BoxShadow> sm(Color strongBase) =>
      _shadow(strongBase, 0.07, 1, 3);
  static List<BoxShadow> md(Color strongBase) =>
      _shadow(strongBase, 0.08, 4, 12);
  static List<BoxShadow> lg(Color strongBase) =>
      _shadow(strongBase, 0.10, 12, 32);
  static List<BoxShadow> xl(Color strongBase) =>
      _shadow(strongBase, 0.14, 24, 60);

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
