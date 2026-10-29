import 'dart:ui' show lerpDouble;

import 'package:flutter/material.dart';

@immutable
class UiTokens extends ThemeExtension<UiTokens> {
  const UiTokens({
    this.spaceSmall = 8,
    this.spaceMedium = 16,
    this.spaceLarge = 24,
    this.controlRadius = 10,
    this.surfaceRadius = 18,
    this.radiusSmall = 8,
    this.fieldRadius = 12,
    this.panelRadius = 14,
    this.modalRadius = 20,
    this.pill = 999,
    this.ease = const Cubic(0.2, 0.8, 0.2, 1),
    this.quick = const Duration(milliseconds: 120),
    this.standard = const Duration(milliseconds: 200),
  });
  final double spaceSmall,
      spaceMedium,
      spaceLarge,
      controlRadius,
      surfaceRadius,
      radiusSmall,
      fieldRadius,
      panelRadius,
      modalRadius,
      pill;
  final Duration quick, standard;
  final Curve ease;
  static UiTokens of(BuildContext context) =>
      Theme.of(context).extension<UiTokens>() ?? const UiTokens();
  @override
  UiTokens copyWith({
    double? spaceSmall,
    double? spaceMedium,
    double? spaceLarge,
    double? controlRadius,
    double? surfaceRadius,
    double? radiusSmall,
    double? fieldRadius,
    double? panelRadius,
    double? modalRadius,
    double? pill,
    Curve? ease,
    Duration? quick,
    Duration? standard,
  }) => UiTokens(
    spaceSmall: spaceSmall ?? this.spaceSmall,
    spaceMedium: spaceMedium ?? this.spaceMedium,
    spaceLarge: spaceLarge ?? this.spaceLarge,
    controlRadius: controlRadius ?? this.controlRadius,
    surfaceRadius: surfaceRadius ?? this.surfaceRadius,
    radiusSmall: radiusSmall ?? this.radiusSmall,
    fieldRadius: fieldRadius ?? this.fieldRadius,
    panelRadius: panelRadius ?? this.panelRadius,
    modalRadius: modalRadius ?? this.modalRadius,
    pill: pill ?? this.pill,
    ease: ease ?? this.ease,
    quick: quick ?? this.quick,
    standard: standard ?? this.standard,
  );
  @override
  UiTokens lerp(UiTokens? other, double t) {
    if (other == null) return this;
    return UiTokens(
      spaceSmall: lerpDouble(spaceSmall, other.spaceSmall, t)!,
      spaceMedium: lerpDouble(spaceMedium, other.spaceMedium, t)!,
      spaceLarge: lerpDouble(spaceLarge, other.spaceLarge, t)!,
      controlRadius: lerpDouble(controlRadius, other.controlRadius, t)!,
      surfaceRadius: lerpDouble(surfaceRadius, other.surfaceRadius, t)!,
      radiusSmall: lerpDouble(radiusSmall, other.radiusSmall, t)!,
      fieldRadius: lerpDouble(fieldRadius, other.fieldRadius, t)!,
      panelRadius: lerpDouble(panelRadius, other.panelRadius, t)!,
      modalRadius: lerpDouble(modalRadius, other.modalRadius, t)!,
      pill: lerpDouble(pill, other.pill, t)!,
      ease: t < 0.5 ? ease : other.ease,
      quick: Duration(
        microseconds: lerpDouble(
          quick.inMicroseconds,
          other.quick.inMicroseconds,
          t,
        )!.round(),
      ),
      standard: Duration(
        microseconds: lerpDouble(
          standard.inMicroseconds,
          other.standard.inMicroseconds,
          t,
        )!.round(),
      ),
    );
  }
}
