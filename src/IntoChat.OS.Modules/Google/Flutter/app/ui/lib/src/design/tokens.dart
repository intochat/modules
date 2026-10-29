import 'dart:ui';

import 'package:flutter/material.dart' show Theme;
import 'package:flutter/widgets.dart';

import '../theme/ui_colors.dart';

/// Shell layout & positioning preset paired with a visual identity.
enum BarLayout {
  /// WinUI 3 / macOS modern floating glass dock + status pills at the bottom.
  floatingGlass('Floating Glass Dock'),

  /// Top translucent status bar + bottom centered floating glass dock.
  splitTopAndDock('Top Bar + Floating Dock'),

  /// Classic full-width 36px bottom bar.
  classicBottom('Full-Width Bottom Bar'),

  /// Compact minimal pill bar.
  compactPill('Compact Pill Bar');

  const BarLayout(this.label);
  final String label;
}

/// Visual identity preset: changes colors, acrylic/glassmorphism materiality,
/// 3D brain aesthetic, and default bar/dock layout while the OS works identically.
enum IcStylePreset {
  fluentAcrylic(
    'Fluent Acrylic',
    'Warm Mica & WinUI 3 Acrylic glass with specular borders and spring motion.',
    BarLayout.floatingGlass,
  ),
  spatialGlass(
    'Spatial Glass',
    'Deep translucent VisionOS frosted glass floating over the live 3D brain.',
    BarLayout.splitTopAndDock,
  ),
  cyberSynaptic(
    'Cyber Synaptic',
    'Bioluminescent neural void with emerald & cyan synaptic signal glow.',
    BarLayout.floatingGlass,
  ),
  warmPaper(
    'Warm Ink & Paper',
    'High-contrast tactile warm paper and ink with crisp 1px hairlines.',
    BarLayout.classicBottom,
  ),
  linearCarbon(
    'Linear Carbon',
    'Hyper-fluid dark carbon with top-lit 1px borders and electric indigo accent.',
    BarLayout.compactPill,
  );

  const IcStylePreset(this.label, this.summary, this.defaultBarLayout);
  final String label;
  final String summary;
  final BarLayout defaultBarLayout;

  /// Alias for [summary].
  String get description => summary;

  /// Canonical kebab-case theme id matching `SettingPreset`.
  String get id => switch (this) {
    IcStylePreset.fluentAcrylic => 'fluent-acrylic',
    IcStylePreset.spatialGlass => 'spatial-glass',
    IcStylePreset.cyberSynaptic => 'cyber-synaptic',
    IcStylePreset.warmPaper => 'warm-paper',
    IcStylePreset.linearCarbon => 'linear-carbon',
  };
}

/// IntoChat v2 tokens (light + warm dark + glassmorphic Fluent Acrylic presets).
@immutable
class IcColors {
  const IcColors({
    required this.brightness,
    required this.surfacePage,
    required this.surfaceSunk,
    required this.surfaceCard,
    required this.surfaceInk,
    required this.textStrong,
    required this.textBody,
    required this.textMuted,
    required this.textSubtle,
    required this.borderHairline,
    required this.borderInput,
    required this.borderStrong,
    required this.accent,
    required this.accentHover,
    required this.accentTint,
    required this.onAccent,
    required this.ring,
    required this.done,
    required this.doneBg,
    required this.working,
    required this.workingBg,
    required this.error,
    required this.errorBg,
    required this.idle,
    required this.chartContext,
    required this.chrome,
    required this.chromeBorder,
    required this.titleBarFocused,
    required this.titleBarBlurred,
    this.preset = IcStylePreset.fluentAcrylic,
    this.blurSigma = 22.0,
    this.windowOpacity = 0.88,
    this.specularBorder = const Color(0x33FFFFFF),
    this.ambientGlow = const Color(0xFF2563EB),
    this.secondaryGlow = const Color(0xFF06B6D4),
  });

  final Brightness brightness;
  final Color surfacePage;
  final Color surfaceSunk;
  final Color surfaceCard;
  final Color surfaceInk;
  final Color textStrong;
  final Color textBody;
  final Color textMuted;
  final Color textSubtle;
  final Color borderHairline;
  final Color borderInput;
  final Color borderStrong;
  final Color accent;
  final Color accentHover;
  final Color accentTint;
  final Color onAccent;
  final Color ring;
  final Color done;
  final Color doneBg;
  final Color working;
  final Color workingBg;
  final Color error;
  final Color errorBg;
  final Color idle;
  final Color chartContext;
  final Color chrome;
  final Color chromeBorder;
  final Color titleBarFocused;
  final Color titleBarBlurred;
  final IcStylePreset preset;
  final double blurSigma;
  final double windowOpacity;
  final Color specularBorder;
  final Color ambientGlow;
  final Color secondaryGlow;

  bool get isDark => brightness == Brightness.dark;
  bool get isGlass => blurSigma > 0 && windowOpacity < 0.99;

  factory IcColors.fromUiColors(UiColors c) => IcColors(
    brightness: c.brightness,
    surfacePage: c.page,
    surfaceSunk: c.recessed,
    surfaceCard: c.surface,
    surfaceInk: c.surfaceInk,
    textStrong: c.strong,
    textBody: c.body,
    textMuted: c.muted,
    textSubtle: c.subtle,
    borderHairline: c.hairline,
    borderInput: c.inputBorder,
    borderStrong: c.borderStrong,
    accent: c.accent,
    accentHover: c.accentHover,
    accentTint: c.accentTint,
    onAccent: c.onAccent,
    ring: c.focus,
    done: c.done,
    doneBg: c.doneBg,
    working: c.working,
    workingBg: c.workingBg,
    error: c.error,
    errorBg: c.errorBg,
    idle: c.subtle,
    chartContext: c.chart4,
    chrome: c.chrome,
    chromeBorder: c.chromeBorder,
    titleBarFocused: c.titleBarFocused,
    titleBarBlurred: c.titleBarBlurred,
    blurSigma: c.blurSigma,
    windowOpacity: c.windowOpacity,
    specularBorder: c.specularBorder,
    ambientGlow: c.ambientGlow,
    secondaryGlow: c.secondaryGlow,
  );

  UiColors toUiColors() => UiColors(
    isDark: isDark,
    blurSigma: blurSigma,
    windowOpacity: windowOpacity,
    specularBorder: specularBorder,
    chrome: chrome,
    chromeBorder: chromeBorder,
    titleBarFocused: titleBarFocused,
    titleBarBlurred: titleBarBlurred,
    borderStrong: borderStrong,
    tone1: surfaceSunk,
    tone2: surfacePage,
    tone3: surfaceCard,
    tone4: borderHairline,
    chart1: accent,
    chart2: done,
    chart3: working,
    chart4: chartContext,
    chart5: secondaryGlow,
    chart6: error,
    ambientGlow: ambientGlow,
    secondaryGlow: secondaryGlow,
    page: surfacePage,
    recessed: surfaceSunk,
    surface: surfaceCard,
    strong: textStrong,
    body: textBody,
    muted: textMuted,
    subtle: textSubtle,
    hairline: borderHairline,
    inputBorder: borderInput,
    accent: accent,
    accentHover: accentHover,
    accentTint: accentTint,
    onAccent: onAccent,
    done: done,
    doneBg: doneBg,
    working: working,
    workingBg: workingBg,
    error: error,
    errorBg: errorBg,
    accentCopy: accent,
    accentCopyHover: accentHover,
    focus: ring,
    userBubble: isDark ? accent : textStrong,
    userText: isDark ? onAccent : surfacePage,
    assistant: surfaceCard,
    assistantText: textBody,
    assistantBorder: borderHairline,
    menuHover: surfaceSunk,
  );

  /// Resolves the palette for a given [IcStylePreset] and [dark] mode.
  static IcColors forPreset(IcStylePreset preset, {required bool dark}) =>
      switch (preset) {
        IcStylePreset.fluentAcrylic =>
          dark ? fluentAcrylicDark : fluentAcrylicLight,
        IcStylePreset.spatialGlass =>
          dark ? spatialGlassDark : spatialGlassLight,
        IcStylePreset.cyberSynaptic =>
          dark ? cyberSynapticDark : fluentAcrylicLight,
        IcStylePreset.warmPaper => dark ? IcColors.dark : IcColors.light,
        IcStylePreset.linearCarbon => dark ? linearCarbonDark : IcColors.light,
      };

  static const fluentAcrylicLight = IcColors(
    brightness: Brightness.light,
    preset: IcStylePreset.fluentAcrylic,
    blurSigma: 24.0,
    windowOpacity: 0.86,
    specularBorder: Color(0x99FFFFFF),
    ambientGlow: Color(0xFF2563EB),
    secondaryGlow: Color(0xFF0EA5E9),
    surfacePage: Color(0xFFF7F6F2),
    surfaceSunk: Color(0xDAEDEAE2),
    surfaceCard: Color(0xE8FFFFFF),
    surfaceInk: Color(0xFF14161D),
    textStrong: Color(0xFF12141A),
    textBody: Color(0xFF252830),
    textMuted: Color(0xFF686D7A),
    textSubtle: Color(0xFF8E94A2),
    borderHairline: Color(0xFFDDE0E7),
    borderInput: Color(0xFFB8BECB),
    borderStrong: Color(0xFF12141A),
    accent: Color(0xFF2563EB),
    accentHover: Color(0xFF1D4ED8),
    accentTint: Color(0xFFE0EAFF),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x592563EB),
    done: Color(0xFF10B981),
    doneBg: Color(0xFFDFF9EE),
    working: Color(0xFFF59E0B),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFEF4444),
    errorBg: Color(0xFFFEE2E2),
    idle: Color(0xFF8E94A2),
    chartContext: Color(0xFF8E94A2),
    chrome: Color(0xCCFFFFFF),
    chromeBorder: Color(0xB3FFFFFF),
    titleBarFocused: Color(0xD9F1EFE9),
    titleBarBlurred: Color(0xCCF8F7F4),
  );

  static const fluentAcrylicDark = IcColors(
    brightness: Brightness.dark,
    preset: IcStylePreset.fluentAcrylic,
    blurSigma: 26.0,
    windowOpacity: 0.82,
    specularBorder: Color(0x2EFFFFFF),
    ambientGlow: Color(0xFF3B82F6),
    secondaryGlow: Color(0xFF06B6D4),
    surfacePage: Color(0xFF11131A),
    surfaceSunk: Color(0xCC0B0D13),
    surfaceCard: Color(0xD91B1F2B),
    surfaceInk: Color(0xFF090B10),
    textStrong: Color(0xFFF8FAFC),
    textBody: Color(0xFFE2E8F0),
    textMuted: Color(0xFF94A3B8),
    textSubtle: Color(0xFF64748B),
    borderHairline: Color(0xFF2A3040),
    borderInput: Color(0xFF3E475E),
    borderStrong: Color(0xFFF1F5F9),
    accent: Color(0xFF3B82F6),
    accentHover: Color(0xFF2563EB),
    accentTint: Color(0x333B82F6),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x733B82F6),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFF87171),
    errorBg: Color(0x29EF4444),
    idle: Color(0xFF64748B),
    chartContext: Color(0xFF94A3B8),
    chrome: Color(0xBF171B26),
    chromeBorder: Color(0x38FFFFFF),
    titleBarFocused: Color(0xD9222736),
    titleBarBlurred: Color(0xBF181C26),
  );

  static const spatialGlassLight = IcColors(
    brightness: Brightness.light,
    preset: IcStylePreset.spatialGlass,
    blurSigma: 34.0,
    windowOpacity: 0.74,
    specularBorder: Color(0xCCFFFFFF),
    ambientGlow: Color(0xFF0284C7),
    secondaryGlow: Color(0xFF6366F1),
    surfacePage: Color(0xCCF1F5F9),
    surfaceSunk: Color(0xB3E2E8F0),
    surfaceCard: Color(0xD9FFFFFF),
    surfaceInk: Color(0xFF0F172A),
    textStrong: Color(0xFF0F172A),
    textBody: Color(0xFF1E293B),
    textMuted: Color(0xFF64748B),
    textSubtle: Color(0xFF94A3B8),
    borderHairline: Color(0x99CBD5E1),
    borderInput: Color(0xFF94A3B8),
    borderStrong: Color(0xFF0F172A),
    accent: Color(0xFF0284C7),
    accentHover: Color(0xFF0369A1),
    accentTint: Color(0xFFE0F2FE),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x590284C7),
    done: Color(0xFF059669),
    doneBg: Color(0xFFD1FAE5),
    working: Color(0xFFD97706),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFE11D48),
    errorBg: Color(0xFFFFE4E6),
    idle: Color(0xFF94A3B8),
    chartContext: Color(0xFF64748B),
    chrome: Color(0xB8FFFFFF),
    chromeBorder: Color(0xCCFFFFFF),
    titleBarFocused: Color(0xCCE2E8F0),
    titleBarBlurred: Color(0xB3F1F5F9),
  );

  static const spatialGlassDark = IcColors(
    brightness: Brightness.dark,
    preset: IcStylePreset.spatialGlass,
    blurSigma: 36.0,
    windowOpacity: 0.72,
    specularBorder: Color(0x45FFFFFF),
    ambientGlow: Color(0xFF38BDF8),
    secondaryGlow: Color(0xFF818CF8),
    surfacePage: Color(0xB80B101B),
    surfaceSunk: Color(0xB3070A12),
    surfaceCard: Color(0xC7161F30),
    surfaceInk: Color(0xFF060911),
    textStrong: Color(0xFFF8FAFC),
    textBody: Color(0xFFE2E8F0),
    textMuted: Color(0xFF94A3B8),
    textSubtle: Color(0xFF64748B),
    borderHairline: Color(0x4094A3B8),
    borderInput: Color(0x6694A3B8),
    borderStrong: Color(0xFFF8FAFC),
    accent: Color(0xFF38BDF8),
    accentHover: Color(0xFF0284C7),
    accentTint: Color(0x3338BDF8),
    onAccent: Color(0xFF082F49),
    ring: Color(0x7338BDF8),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFFB7185),
    errorBg: Color(0x29E11D48),
    idle: Color(0xFF64748B),
    chartContext: Color(0xFF94A3B8),
    chrome: Color(0xA6131C2E),
    chromeBorder: Color(0x4DFFFFFF),
    titleBarFocused: Color(0xCC1E293B),
    titleBarBlurred: Color(0xA6131C2E),
  );

  static const cyberSynapticDark = IcColors(
    brightness: Brightness.dark,
    preset: IcStylePreset.cyberSynaptic,
    blurSigma: 28.0,
    windowOpacity: 0.80,
    specularBorder: Color(0x4010B981),
    ambientGlow: Color(0xFF10B981),
    secondaryGlow: Color(0xFF06B6D4),
    surfacePage: Color(0xFF060B0E),
    surfaceSunk: Color(0xD9040709),
    surfaceCard: Color(0xD90C151B),
    surfaceInk: Color(0xFF030608),
    textStrong: Color(0xFFECFDF5),
    textBody: Color(0xFFD1FAE5),
    textMuted: Color(0xFF6EE7B7),
    textSubtle: Color(0xFF34D399),
    borderHairline: Color(0xFF132A2E),
    borderInput: Color(0xFF1E464B),
    borderStrong: Color(0xFF34D399),
    accent: Color(0xFF10B981),
    accentHover: Color(0xFF059669),
    accentTint: Color(0x2910B981),
    onAccent: Color(0xFF022C22),
    ring: Color(0x7310B981),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFF87171),
    errorBg: Color(0x29EF4444),
    idle: Color(0xFF047857),
    chartContext: Color(0xFF06B6D4),
    chrome: Color(0xBF091217),
    chromeBorder: Color(0x4D10B981),
    titleBarFocused: Color(0xD90F1E26),
    titleBarBlurred: Color(0xBF091217),
  );

  static const linearCarbonDark = IcColors(
    brightness: Brightness.dark,
    preset: IcStylePreset.linearCarbon,
    blurSigma: 20.0,
    windowOpacity: 0.92,
    specularBorder: Color(0x24FFFFFF),
    ambientGlow: Color(0xFF5E6AD2),
    secondaryGlow: Color(0xFF818CF8),
    surfacePage: Color(0xFF0D0E12),
    surfaceSunk: Color(0xFF090A0D),
    surfaceCard: Color(0xFF15171E),
    surfaceInk: Color(0xFF07080A),
    textStrong: Color(0xFFF4F5F8),
    textBody: Color(0xFFD8DAE0),
    textMuted: Color(0xFF8A8F9E),
    textSubtle: Color(0xFF5C606E),
    borderHairline: Color(0xFF232630),
    borderInput: Color(0xFF343846),
    borderStrong: Color(0xFFF4F5F8),
    accent: Color(0xFF5E6AD2),
    accentHover: Color(0xFF4E5AC0),
    accentTint: Color(0x2E5E6AD2),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x735E6AD2),
    done: Color(0xFF4CB782),
    doneBg: Color(0x294CB782),
    working: Color(0xFFF2C94C),
    workingBg: Color(0x29F2C94C),
    error: Color(0xFFEB5757),
    errorBg: Color(0x29EB5757),
    idle: Color(0xFF5C606E),
    chartContext: Color(0xFF8A8F9E),
    chrome: Color(0xD914161D),
    chromeBorder: Color(0xFF262934),
    titleBarFocused: Color(0xFF1B1E27),
    titleBarBlurred: Color(0xFF13151B),
  );

  static const light = IcColors(
    brightness: Brightness.light,
    preset: IcStylePreset.warmPaper,
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFFE4DFD5),
    ambientGlow: Color(0xFF1F5EFF),
    secondaryGlow: Color(0xFF12A150),
    surfacePage: Color(0xFFFBFAF7),
    surfaceSunk: Color(0xFFF2EFE9),
    surfaceCard: Color(0xFFFFFFFF),
    surfaceInk: Color(0xFF17140F),
    textStrong: Color(0xFF17140F),
    textBody: Color(0xFF2A251E),
    textMuted: Color(0xFF7C7264),
    textSubtle: Color(0xFF9C9182),
    borderHairline: Color(0xFFE4DFD5),
    borderInput: Color(0xFFC2B9AB),
    borderStrong: Color(0xFF17140F),
    accent: Color(0xFF1F5EFF),
    accentHover: Color(0xFF1541C7),
    accentTint: Color(0xFFE7EEFF),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x591F5EFF),
    done: Color(0xFF12A150),
    doneBg: Color(0xFFE1F6EA),
    working: Color(0xFFE8A317),
    workingBg: Color(0xFFFCF1DA),
    error: Color(0xFFE0402F),
    errorBg: Color(0xFFFCE7E3),
    idle: Color(0xFF9C9182),
    chartContext: Color(0xFF9C9182),
    chrome: Color(0xE6FFFFFF),
    chromeBorder: Color(0xFFE4DFD5),
    titleBarFocused: Color(0xFFF2EFE9),
    titleBarBlurred: Color(0xFFFBFAF7),
  );

  static const dark = IcColors(
    brightness: Brightness.dark,
    preset: IcStylePreset.warmPaper,
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFF423B31),
    ambientGlow: Color(0xFF4B7DFF),
    secondaryGlow: Color(0xFF8FE0AF),
    surfacePage: Color(0xFF17140F),
    surfaceSunk: Color(0xFF110F0B),
    surfaceCard: Color(0xFF2A251E),
    surfaceInk: Color(0xFF110F0B),
    textStrong: Color(0xFFFBFAF7),
    textBody: Color(0xFFF0ECE4),
    textMuted: Color(0xFF9C9182),
    textSubtle: Color(0xFF7C7264),
    borderHairline: Color(0xFF423B31),
    borderInput: Color(0xFF5E554A),
    borderStrong: Color(0xFFF0ECE4),
    accent: Color(0xFF4B7DFF),
    accentHover: Color(0xFF1F5EFF),
    accentTint: Color(0x294B7DFF),
    onAccent: Color(0xFFFFFFFF),
    ring: Color(0x734B7DFF),
    done: Color(0xFF8FE0AF),
    doneBg: Color(0x2912A150),
    working: Color(0xFFF6D28A),
    workingBg: Color(0x29E8A317),
    error: Color(0xFFF3A99F),
    errorBg: Color(0x29E0402F),
    idle: Color(0xFF7C7264),
    chartContext: Color(0xFF9C9182),
    chrome: Color(0xE62A251E),
    chromeBorder: Color(0xFF423B31),
    titleBarFocused: Color(0xFF332D25),
    titleBarBlurred: Color(0xFF2A251E),
  );
}

/// Desktop background tones (Settings > Desktop & Dock).
enum DesktopTone { cream, sand, ink }

extension DesktopToneColor on DesktopTone {
  String get label => switch (this) {
    DesktopTone.cream => 'Cream',
    DesktopTone.sand => 'Sand',
    DesktopTone.ink => 'Ink',
  };

  Color color(IcColors c) => switch (c.preset) {
    IcStylePreset.fluentAcrylic =>
      c.isDark ? const Color(0xFF0A0D14) : const Color(0xFFEDEBE4),
    IcStylePreset.spatialGlass =>
      c.isDark ? const Color(0xFF060913) : const Color(0xFFE2E8F0),
    IcStylePreset.cyberSynaptic => const Color(0xFF04080B),
    IcStylePreset.linearCarbon => const Color(0xFF08090C),
    IcStylePreset.warmPaper => switch (this) {
      DesktopTone.cream =>
        c.isDark ? const Color(0xFF1D1914) : const Color(0xFFF2EFE9),
      DesktopTone.sand =>
        c.isDark ? const Color(0xFF231E17) : const Color(0xFFE6DFD2),
      DesktopTone.ink =>
        c.isDark ? const Color(0xFF0D0B08) : const Color(0xFF2A251E),
    },
  };
}

abstract final class IcRadius {
  static const xs = 6.0;
  static const sm = 8.0;
  static const button = 10.0;
  static const input = 12.0;
  static const card = 14.0;
  static const panel = 20.0;
  static const bubble = 18.0;
  static const pill = 999.0;
}

abstract final class IcShadow {
  static List<BoxShadow> xs(IcColors c) => c.isDark
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

  static List<BoxShadow> sm(IcColors c) => c.isDark
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

  static List<BoxShadow> md(IcColors c) => c.isDark
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

  static List<BoxShadow> lg(IcColors c) => c.isDark
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

  static List<BoxShadow> xl(IcColors c) => c.isDark
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

abstract final class IcMotion {
  static const ease = Cubic(0.2, 0.8, 0.2, 1);
  static const spring = Cubic(0.16, 1.0, 0.3, 1.0);
  static const fast = Duration(milliseconds: 120);
  static const base = Duration(milliseconds: 200);
  static const slow = Duration(milliseconds: 280);
}

/// Reusable WinUI 3 Acrylic / Glassmorphic surface wrapper.
class IcAcrylicBox extends StatelessWidget {
  const IcAcrylicBox({
    super.key,
    required this.child,
    this.radius = IcRadius.card,
    this.color,
    this.borderColor,
    this.shadows,
    this.padding,
    this.sigmaOverride,
  });

  final Widget child;
  final double radius;
  final Color? color;
  final Color? borderColor;
  final List<BoxShadow>? shadows;
  final EdgeInsetsGeometry? padding;
  final double? sigmaOverride;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final sigma = sigmaOverride ?? c.blurSigma;
    final fill = color ?? c.surfaceCard;
    final border =
        borderColor ?? (c.isGlass ? c.specularBorder : c.borderHairline);

    final box = Container(
      padding: padding,
      decoration: BoxDecoration(
        color: fill,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: border),
        boxShadow: shadows,
      ),
      child: child,
    );

    if (sigma <= 0) return box;

    return ClipRRect(
      borderRadius: BorderRadius.circular(radius),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: sigma, sigmaY: sigma),
        child: box,
      ),
    );
  }
}

/// Inter + InterDisplay typography with strict tabular figures (`tnum`).
abstract final class IcType {
  static const family = 'Inter';
  static const display = 'InterDisplay';
  static const _pkg = 'intochat_os_ui';
  static const tabular = [FontFeature.tabularFigures()];

  static TextStyle text(
    double size, {
    FontWeight weight = FontWeight.w400,
    Color? color,
    bool tnum = false,
    double? height,
  }) => TextStyle(
    fontFamily: family,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    height: height,
    letterSpacing: size >= 20 ? -0.2 : 0,
    fontFeatures: tnum ? tabular : null,
  );

  static TextStyle heading(
    double size, {
    FontWeight weight = FontWeight.w700,
    Color? color,
  }) => TextStyle(
    fontFamily: display,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    letterSpacing: -0.4,
    height: 1.15,
  );

  static TextStyle mono(
    double size, {
    FontWeight weight = FontWeight.w500,
    Color? color,
  }) => TextStyle(
    fontFamily: family,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    fontFeatures: tabular,
  );
}

/// Theme access: `IcTheme.of(context)`.
/// Resolves from an explicit [IcTheme] ancestor or falls back to the active
/// [UiColors] extension on [Theme].
class IcTheme extends InheritedWidget {
  const IcTheme({super.key, required this.colors, required super.child});

  final IcColors colors;

  static IcColors? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<IcTheme>()?.colors;

  static IcColors of(BuildContext context) {
    final explicit = maybeOf(context);
    if (explicit != null) return explicit;
    final ui = Theme.of(context).extension<UiColors>();
    if (ui != null) return IcColors.fromUiColors(ui);
    return IcColors.fluentAcrylicDark;
  }

  @override
  bool updateShouldNotify(IcTheme oldWidget) => oldWidget.colors != colors;
}
