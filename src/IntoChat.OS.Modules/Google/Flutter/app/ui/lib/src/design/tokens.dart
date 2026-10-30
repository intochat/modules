// ignore_for_file: prefer_initializing_formals

import 'dart:ui';

import 'package:flutter/material.dart' show Theme, Brightness;
import 'package:flutter/widgets.dart';

import '../theme/ui_colors.dart';

/// 3D Brain shader modes available across the 7 UI Identities.
enum IcBrainShaderMode {
  volumetricGlass('volumetric-glass', 'Volumetric Glass Spheres'),
  architecturalBlueprint('architectural-blueprint', 'Architectural Blueprint'),
  starMapConstellation('star-map', 'Star-Map Constellation'),
  wireframeHud('wireframe-hud', 'Wireframe HUD');

  const IcBrainShaderMode(this.id, this.label);
  final String id;
  final String label;
}

/// Shell layout & positioning preset paired with a visual identity.
enum BarLayout {
  floatingGlass('floating-glass', 'Floating Glass Dock'),
  splitTop('split-top', 'Top Bar + Floating Dock'),
  classicBottom('classic-bottom', 'Full-Width Bottom Bar'),
  compactPill('compact-pill', 'Compact Pill Bar');

  const BarLayout(this.id, this.label);
  final String id;
  final String label;

  static BarLayout fromId(String? id) {
    for (final b in BarLayout.values) {
      if (b.id == id) return b;
    }
    return BarLayout.floatingGlass;
  }
}

/// Visual identity preset: all 7 UI Identities from `ui_redesign_plan.md`
/// defined in one place with theme-aware 3D `BrainScene3D` aesthetics.
enum IcStylePreset {
  fluentAcrylic(
    'fluent-acrylic',
    'Fluent Acrylic',
    'Warm Mica & WinUI 3 Acrylic glass with specular borders and spring motion.',
    IcBrainShaderMode.volumetricGlass,
    BarLayout.floatingGlass,
  ),
  spatialGlass(
    'spatial-glass',
    'Spatial Glass',
    'Deep translucent VisionOS frosted glass floating over the live 3D brain.',
    IcBrainShaderMode.volumetricGlass,
    BarLayout.splitTop,
  ),
  cyberSynaptic(
    'cyber-synaptic',
    'Cyber Synaptic',
    'Bioluminescent neural void with emerald & cyan synaptic signal glow.',
    IcBrainShaderMode.wireframeHud,
    BarLayout.floatingGlass,
  ),
  warmPaper(
    'warm-paper',
    'Warm Ink & Paper',
    'High-contrast tactile warm paper and ink with crisp 1px hairlines.',
    IcBrainShaderMode.architecturalBlueprint,
    BarLayout.classicBottom,
  ),
  fluentWarmMica(
    'fluent-warm-mica',
    'Fluent Warm Mica',
    'WinUI 3 Warm Espresso & Amber Acrylic with soft specular warmth.',
    IcBrainShaderMode.volumetricGlass,
    BarLayout.floatingGlass,
  ),
  linearCarbon(
    'linear-carbon',
    'Linear Carbon',
    'Hyper-fluid dark carbon with top-lit 1px borders and electric indigo accent.',
    IcBrainShaderMode.starMapConstellation,
    BarLayout.compactPill,
  ),
  nordicTactical(
    'nordic-tactical',
    'Nordic Tactical',
    '2D/3D architectural blueprint with slate & signal cyan telemetry.',
    IcBrainShaderMode.architecturalBlueprint,
    BarLayout.splitTop,
  );

  const IcStylePreset(
    this.id,
    this.label,
    this.subtitle,
    this.defaultShaderMode,
    this.defaultBarLayout,
  );

  final String id;
  final String label;
  final String subtitle;
  final IcBrainShaderMode defaultShaderMode;
  final BarLayout defaultBarLayout;

  String get summary => subtitle;
  String get description => subtitle;

  static IcStylePreset fromId(String? id) {
    for (final p in IcStylePreset.values) {
      if (p.id == id) return p;
    }
    return IcStylePreset.fluentAcrylic;
  }
}

/// Unified colour tokens for IntoChat OS (all 7 UI Identities in one place).
@immutable
class IcColors {
  const IcColors({
    required this.isDark,
    required this.preset,
    required this.surfacePage,
    required this.surfaceElevated,
    required this.surfaceCard,
    required this.surfaceSunk,
    required this.surfaceGlass,
    required this.surfaceGlassStrong,
    required this.surfaceInk,
    required this.textStrong,
    required this.textBody,
    required this.textMuted,
    required this.textSubtle,
    required this.onInk,
    required this.accent,
    required this.accentHover,
    required this.accentTint,
    required this.accentRing,
    required this.onAccent,
    required this.done,
    required this.doneBg,
    required this.working,
    required this.workingBg,
    required this.error,
    required this.errorBg,
    required this.borderHairline,
    required this.borderStrong,
    required this.borderInput,
    required this.glassHighlight,
    required this.glassBlurSigma,
    required this.glowColor,
    this.windowOpacity = 0.88,
    Color? chrome,
    Color? chromeBorder,
    Color? titleBarFocused,
    Color? titleBarBlurred,
    Color? secondaryGlow,
    Color? chartContext,
  }) : _chrome = chrome,
       _chromeBorder = chromeBorder,
       _titleBarFocused = titleBarFocused,
       _titleBarBlurred = titleBarBlurred,
       _secondaryGlow = secondaryGlow,
       _chartContext = chartContext;

  final bool isDark;
  final IcStylePreset preset;

  final Color surfacePage;
  final Color surfaceElevated;
  final Color surfaceCard;
  final Color surfaceSunk;
  final Color surfaceGlass;
  final Color surfaceGlassStrong;
  final Color surfaceInk;

  final Color textStrong;
  final Color textBody;
  final Color textMuted;
  final Color textSubtle;
  final Color onInk;

  final Color accent;
  final Color accentHover;
  final Color accentTint;
  final Color accentRing;
  final Color onAccent;

  final Color done;
  final Color doneBg;
  final Color working;
  final Color workingBg;
  final Color error;
  final Color errorBg;

  final Color borderHairline;
  final Color borderStrong;
  final Color borderInput;
  final Color glassHighlight;
  final double glassBlurSigma;
  final Color glowColor;
  final double windowOpacity;

  final Color? _chrome;
  final Color? _chromeBorder;
  final Color? _titleBarFocused;
  final Color? _titleBarBlurred;
  final Color? _secondaryGlow;
  final Color? _chartContext;

  Brightness get brightness => isDark ? Brightness.dark : Brightness.light;
  bool get isGlass => glassBlurSigma > 0 && windowOpacity < 0.99;
  double get blurSigma => glassBlurSigma;
  Color get specularBorder => glassHighlight;
  Color get titleBarFocused => _titleBarFocused ?? surfaceElevated;
  Color get titleBarBlurred => _titleBarBlurred ?? surfaceSunk;
  Color get chrome => _chrome ?? surfaceGlassStrong;
  Color get chromeBorder => _chromeBorder ?? borderHairline;
  Color get ambientGlow => glowColor;
  Color get secondaryGlow => _secondaryGlow ?? glowColor;
  Color get ring => accentRing;
  Color get idle => textSubtle;
  Color get chartContext => _chartContext ?? textSubtle;

  factory IcColors.fromUiColors(UiColors c) => IcColors(
    isDark: c.isDark,
    preset: c.preset,
    surfacePage: c.page,
    surfaceElevated: c.elevated,
    surfaceCard: c.surface,
    surfaceSunk: c.recessed,
    surfaceGlass: c.elevated,
    surfaceGlassStrong: c.surface,
    surfaceInk: c.ink,
    textStrong: c.strong,
    textBody: c.body,
    textMuted: c.muted,
    textSubtle: c.subtle,
    onInk: c.onInk,
    accent: c.accent,
    accentHover: c.accentHover,
    accentTint: c.accentTint,
    accentRing: c.focus,
    onAccent: c.onAccent,
    done: c.done,
    doneBg: c.doneBg,
    working: c.working,
    workingBg: c.workingBg,
    error: c.error,
    errorBg: c.errorBg,
    borderHairline: c.hairline,
    borderStrong: c.borderStrong,
    borderInput: c.inputBorder,
    glassHighlight: c.specularBorder,
    glassBlurSigma: c.blurSigma,
    glowColor: c.ambientGlow,
    windowOpacity: c.windowOpacity,
    chrome: c.chrome,
    chromeBorder: c.chromeBorder,
    titleBarFocused: c.titleBarFocused,
    titleBarBlurred: c.titleBarBlurred,
    secondaryGlow: c.secondaryGlow,
    chartContext: c.chartContext,
  );

  static IcColors forPreset(IcStylePreset preset, {bool dark = true}) {
    return switch (preset) {
      IcStylePreset.fluentAcrylic => dark ? fluentDark : fluentLight,
      IcStylePreset.spatialGlass => dark ? spatialDark : spatialLight,
      IcStylePreset.cyberSynaptic => dark ? cyberDark : cyberLight,
      IcStylePreset.warmPaper => dark ? warmPaperDark : warmPaperLight,
      IcStylePreset.fluentWarmMica =>
        dark ? fluentWarmMicaDark : fluentWarmMicaLight,
      IcStylePreset.linearCarbon => dark ? linearDark : linearLight,
      IcStylePreset.nordicTactical =>
        dark ? nordicTacticalDark : nordicTacticalLight,
    };
  }

  /// Warm Paper Dark (`IcColors.dark`) and Warm Paper Light (`IcColors.light`).
  static const dark = warmPaperDark;
  static const light = warmPaperLight;

  static const fluentAcrylicDark = fluentDark;
  static const fluentAcrylicLight = fluentLight;
  static const spatialGlassDark = spatialDark;
  static const spatialGlassLight = spatialLight;
  static const cyberSynapticDark = cyberDark;
  static const linearCarbonDark = linearDark;

  static const fluentDark = IcColors(
    isDark: true,
    preset: IcStylePreset.fluentAcrylic,
    surfacePage: Color(0xFF11131A),
    surfaceElevated: Color(0xFF171B26),
    surfaceCard: Color(0xD91B1F2B),
    surfaceSunk: Color(0xCC0B0D13),
    surfaceGlass: Color(0xBF171B26),
    surfaceGlassStrong: Color(0xD91B1F2B),
    surfaceInk: Color(0xFF090B10),
    textStrong: Color(0xFFF8FAFC),
    textBody: Color(0xFFE2E8F0),
    textMuted: Color(0xFF94A3B8),
    textSubtle: Color(0xFF64748B),
    onInk: Color(0xFFF8FAFC),
    accent: Color(0xFF3B82F6),
    accentHover: Color(0xFF2563EB),
    accentTint: Color(0x333B82F6),
    accentRing: Color(0x733B82F6),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFF87171),
    errorBg: Color(0x29EF4444),
    borderHairline: Color(0xFF2A3040),
    borderStrong: Color(0xFFF1F5F9),
    borderInput: Color(0xFF3E475E),
    glassHighlight: Color(0x2EFFFFFF),
    glassBlurSigma: 26.0,
    glowColor: Color(0xFF3B82F6),
    windowOpacity: 0.82,
    chrome: Color(0xBF171B26),
    chromeBorder: Color(0x38FFFFFF),
    titleBarFocused: Color(0xD9222736),
    titleBarBlurred: Color(0xBF181C26),
    secondaryGlow: Color(0xFF06B6D4),
    chartContext: Color(0xFF94A3B8),
  );

  static const fluentLight = IcColors(
    isDark: false,
    preset: IcStylePreset.fluentAcrylic,
    surfacePage: Color(0xFFF7F6F2),
    surfaceElevated: Color(0xCCFFFFFF),
    surfaceCard: Color(0xE8FFFFFF),
    surfaceSunk: Color(0xDAEDEAE2),
    surfaceGlass: Color(0xCCFFFFFF),
    surfaceGlassStrong: Color(0xE8FFFFFF),
    surfaceInk: Color(0xFF14161D),
    textStrong: Color(0xFF12141A),
    textBody: Color(0xFF252830),
    textMuted: Color(0xFF686D7A),
    textSubtle: Color(0xFF8E94A2),
    onInk: Color(0xFFFFFFFF),
    accent: Color(0xFF2563EB),
    accentHover: Color(0xFF1D4ED8),
    accentTint: Color(0xFFE0EAFF),
    accentRing: Color(0x592563EB),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF10B981),
    doneBg: Color(0xFFDFF9EE),
    working: Color(0xFFF59E0B),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFEF4444),
    errorBg: Color(0xFFFEE2E2),
    borderHairline: Color(0xFFDDE0E7),
    borderStrong: Color(0xFF12141A),
    borderInput: Color(0xFFB8BECB),
    glassHighlight: Color(0x99FFFFFF),
    glassBlurSigma: 24.0,
    glowColor: Color(0xFF2563EB),
    windowOpacity: 0.86,
    chrome: Color(0xCCFFFFFF),
    chromeBorder: Color(0xB3FFFFFF),
    titleBarFocused: Color(0xD9F1EFE9),
    titleBarBlurred: Color(0xCCF8F7F4),
    secondaryGlow: Color(0xFF0EA5E9),
    chartContext: Color(0xFF8E94A2),
  );

  static const spatialDark = IcColors(
    isDark: true,
    preset: IcStylePreset.spatialGlass,
    surfacePage: Color(0xB80B101B),
    surfaceElevated: Color(0xA6131C2E),
    surfaceCard: Color(0xC7161F30),
    surfaceSunk: Color(0xB3070A12),
    surfaceGlass: Color(0xA6131C2E),
    surfaceGlassStrong: Color(0xC7161F30),
    surfaceInk: Color(0xFF060911),
    textStrong: Color(0xFFF8FAFC),
    textBody: Color(0xFFE2E8F0),
    textMuted: Color(0xFF94A3B8),
    textSubtle: Color(0xFF64748B),
    onInk: Color(0xFFF8FAFC),
    accent: Color(0xFF38BDF8),
    accentHover: Color(0xFF0284C7),
    accentTint: Color(0x3338BDF8),
    accentRing: Color(0x7338BDF8),
    onAccent: Color(0xFF082F49),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFFB7185),
    errorBg: Color(0x29E11D48),
    borderHairline: Color(0x4094A3B8),
    borderStrong: Color(0xFFF8FAFC),
    borderInput: Color(0x6694A3B8),
    glassHighlight: Color(0x45FFFFFF),
    glassBlurSigma: 36.0,
    glowColor: Color(0xFF38BDF8),
    windowOpacity: 0.72,
    chrome: Color(0xA6131C2E),
    chromeBorder: Color(0x4DFFFFFF),
    titleBarFocused: Color(0xCC1E293B),
    titleBarBlurred: Color(0xA6131C2E),
    secondaryGlow: Color(0xFF818CF8),
    chartContext: Color(0xFF94A3B8),
  );

  static const spatialLight = IcColors(
    isDark: false,
    preset: IcStylePreset.spatialGlass,
    surfacePage: Color(0xCCF1F5F9),
    surfaceElevated: Color(0xB8FFFFFF),
    surfaceCard: Color(0xD9FFFFFF),
    surfaceSunk: Color(0xB3E2E8F0),
    surfaceGlass: Color(0xB8FFFFFF),
    surfaceGlassStrong: Color(0xD9FFFFFF),
    surfaceInk: Color(0xFF0F172A),
    textStrong: Color(0xFF0F172A),
    textBody: Color(0xFF1E293B),
    textMuted: Color(0xFF64748B),
    textSubtle: Color(0xFF94A3B8),
    onInk: Color(0xFFFFFFFF),
    accent: Color(0xFF0284C7),
    accentHover: Color(0xFF0369A1),
    accentTint: Color(0xFFE0F2FE),
    accentRing: Color(0x590284C7),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF059669),
    doneBg: Color(0xFFD1FAE5),
    working: Color(0xFFD97706),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFE11D48),
    errorBg: Color(0xFFFFE4E6),
    borderHairline: Color(0x99CBD5E1),
    borderStrong: Color(0xFF0F172A),
    borderInput: Color(0xFF94A3B8),
    glassHighlight: Color(0xCCFFFFFF),
    glassBlurSigma: 34.0,
    glowColor: Color(0xFF0284C7),
    windowOpacity: 0.74,
    chrome: Color(0xB8FFFFFF),
    chromeBorder: Color(0xCCFFFFFF),
    titleBarFocused: Color(0xCCE2E8F0),
    titleBarBlurred: Color(0xB3F1F5F9),
    secondaryGlow: Color(0xFF6366F1),
    chartContext: Color(0xFF64748B),
  );

  static const cyberDark = IcColors(
    isDark: true,
    preset: IcStylePreset.cyberSynaptic,
    surfacePage: Color(0xFF060B0E),
    surfaceElevated: Color(0xBF091217),
    surfaceCard: Color(0xD90C151B),
    surfaceSunk: Color(0xD9040709),
    surfaceGlass: Color(0xBF091217),
    surfaceGlassStrong: Color(0xD90C151B),
    surfaceInk: Color(0xFF030608),
    textStrong: Color(0xFFECFDF5),
    textBody: Color(0xFFD1FAE5),
    textMuted: Color(0xFF6EE7B7),
    textSubtle: Color(0xFF34D399),
    onInk: Color(0xFFECFDF5),
    accent: Color(0xFF10B981),
    accentHover: Color(0xFF059669),
    accentTint: Color(0x2910B981),
    accentRing: Color(0x7310B981),
    onAccent: Color(0xFF022C22),
    done: Color(0xFF34D399),
    doneBg: Color(0x2910B981),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFF87171),
    errorBg: Color(0x29EF4444),
    borderHairline: Color(0xFF132A2E),
    borderStrong: Color(0xFF34D399),
    borderInput: Color(0xFF1E464B),
    glassHighlight: Color(0x4010B981),
    glassBlurSigma: 28.0,
    glowColor: Color(0xFF10B981),
    windowOpacity: 0.80,
    chrome: Color(0xBF091217),
    chromeBorder: Color(0x4D10B981),
    titleBarFocused: Color(0xD90F1E26),
    titleBarBlurred: Color(0xBF091217),
    secondaryGlow: Color(0xFF06B6D4),
    chartContext: Color(0xFF06B6D4),
  );

  static const cyberLight = fluentLight;

  static const warmPaperLight = IcColors(
    isDark: false,
    preset: IcStylePreset.warmPaper,
    surfacePage: Color(0xFFFBFAF7),
    surfaceElevated: Color(0xFFF5F3EE),
    surfaceCard: Color(0xFFFFFFFF),
    surfaceSunk: Color(0xFFF2EFE9),
    surfaceGlass: Color(0xE6FFFFFF),
    surfaceGlassStrong: Color(0xFFFFFFFF),
    surfaceInk: Color(0xFF17140F),
    textStrong: Color(0xFF17140F),
    textBody: Color(0xFF2A251E),
    textMuted: Color(0xFF7C7264),
    textSubtle: Color(0xFF9C9182),
    onInk: Color(0xFFFBFAF7),
    accent: Color(0xFF1F5EFF),
    accentHover: Color(0xFF1541C7),
    accentTint: Color(0xFFE7EEFF),
    accentRing: Color(0x591F5EFF),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF12A150),
    doneBg: Color(0xFFE1F6EA),
    working: Color(0xFFE8A317),
    workingBg: Color(0xFFFCF1DA),
    error: Color(0xFFE0402F),
    errorBg: Color(0xFFFCE7E3),
    borderHairline: Color(0xFFE4DFD5),
    borderStrong: Color(0xFF17140F),
    borderInput: Color(0xFFC2B9AB),
    glassHighlight: Color(0xFFE4DFD5),
    glassBlurSigma: 0.0,
    glowColor: Color(0xFF1F5EFF),
    windowOpacity: 1.0,
    chrome: Color(0xE6FFFFFF),
    chromeBorder: Color(0xFFE4DFD5),
    titleBarFocused: Color(0xFFF2EFE9),
    titleBarBlurred: Color(0xFFFBFAF7),
    secondaryGlow: Color(0xFF12A150),
    chartContext: Color(0xFF9C9182),
  );

  static const warmPaperDark = IcColors(
    isDark: true,
    preset: IcStylePreset.warmPaper,
    surfacePage: Color(0xFF17140F),
    surfaceElevated: Color(0xFF1F1B15),
    surfaceCard: Color(0xFF2A251E),
    surfaceSunk: Color(0xFF110F0B),
    surfaceGlass: Color(0xE62A251E),
    surfaceGlassStrong: Color(0xFF2A251E),
    surfaceInk: Color(0xFF110F0B),
    textStrong: Color(0xFFFBFAF7),
    textBody: Color(0xFFF0ECE4),
    textMuted: Color(0xFF9C9182),
    textSubtle: Color(0xFF7C7264),
    onInk: Color(0xFF17140F),
    accent: Color(0xFF4B7DFF),
    accentHover: Color(0xFF1F5EFF),
    accentTint: Color(0x294B7DFF),
    accentRing: Color(0x734B7DFF),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF8FE0AF),
    doneBg: Color(0x2912A150),
    working: Color(0xFFF6D28A),
    workingBg: Color(0x29E8A317),
    error: Color(0xFFF3A99F),
    errorBg: Color(0x29E0402F),
    borderHairline: Color(0xFF423B31),
    borderStrong: Color(0xFFF0ECE4),
    borderInput: Color(0xFF5E554A),
    glassHighlight: Color(0xFF423B31),
    glassBlurSigma: 0.0,
    glowColor: Color(0xFF4B7DFF),
    windowOpacity: 1.0,
    chrome: Color(0xE62A251E),
    chromeBorder: Color(0xFF423B31),
    titleBarFocused: Color(0xFF332D25),
    titleBarBlurred: Color(0xFF2A251E),
    secondaryGlow: Color(0xFF8FE0AF),
    chartContext: Color(0xFF9C9182),
  );

  static const fluentWarmMicaDark = IcColors(
    isDark: true,
    preset: IcStylePreset.fluentWarmMica,
    surfacePage: Color(0xFF141210),
    surfaceElevated: Color(0xFF1E1B18),
    surfaceCard: Color(0xFF25211D),
    surfaceSunk: Color(0xFF181512),
    surfaceGlass: Color(0xCC1E1B18),
    surfaceGlassStrong: Color(0xE625211D),
    surfaceInk: Color(0xFFFAF7F2),
    textStrong: Color(0xFFFAF7F2),
    textBody: Color(0xFFE6E0D6),
    textMuted: Color(0xFFA69D90),
    textSubtle: Color(0xFF787065),
    onInk: Color(0xFF141210),
    accent: Color(0xFFF59E0B),
    accentHover: Color(0xFFFBBF24),
    accentTint: Color(0x26F59E0B),
    accentRing: Color(0x66F59E0B),
    onAccent: Color(0xFF141210),
    done: Color(0xFF34D399),
    doneBg: Color(0x2234D399),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x24FBBF24),
    error: Color(0xFFF87171),
    errorBg: Color(0x24F87171),
    borderHairline: Color(0xFF2E2924),
    borderStrong: Color(0xFF423B34),
    borderInput: Color(0xFF38322C),
    glassHighlight: Color(0x22FFFFFF),
    glassBlurSigma: 26.0,
    glowColor: Color(0xFFF59E0B),
    windowOpacity: 0.86,
  );

  static const fluentWarmMicaLight = IcColors(
    isDark: false,
    preset: IcStylePreset.fluentWarmMica,
    surfacePage: Color(0xFFF7F4EF),
    surfaceElevated: Color(0xFFFFFFFF),
    surfaceCard: Color(0xFFFFFFFF),
    surfaceSunk: Color(0xFFEDE8DF),
    surfaceGlass: Color(0xD9FFFFFF),
    surfaceGlassStrong: Color(0xF0FFFFFF),
    surfaceInk: Color(0xFF1C1917),
    textStrong: Color(0xFF1C1917),
    textBody: Color(0xFF292524),
    textMuted: Color(0xFF57534E),
    textSubtle: Color(0xFF78716C),
    onInk: Color(0xFFFFFFFF),
    accent: Color(0xFFD97706),
    accentHover: Color(0xFFB45309),
    accentTint: Color(0x1FD97706),
    accentRing: Color(0x59D97706),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF059669),
    doneBg: Color(0x18059669),
    working: Color(0xFFD97706),
    workingBg: Color(0x18D97706),
    error: Color(0xFFDC2626),
    errorBg: Color(0x18DC2626),
    borderHairline: Color(0xFFE5DFD5),
    borderStrong: Color(0xFFD6CEC2),
    borderInput: Color(0xFFDDD6CA),
    glassHighlight: Color(0xB3FFFFFF),
    glassBlurSigma: 22.0,
    glowColor: Color(0xFFD97706),
    windowOpacity: 0.88,
  );

  static const linearDark = IcColors(
    isDark: true,
    preset: IcStylePreset.linearCarbon,
    surfacePage: Color(0xFF0D0E12),
    surfaceElevated: Color(0xFF111317),
    surfaceCard: Color(0xFF15171E),
    surfaceSunk: Color(0xFF090A0D),
    surfaceGlass: Color(0xCC111317),
    surfaceGlassStrong: Color(0xE615171E),
    surfaceInk: Color(0xFF07080A),
    textStrong: Color(0xFFF4F5F8),
    textBody: Color(0xFFD8DAE0),
    textMuted: Color(0xFF8A8F9E),
    textSubtle: Color(0xFF5C606E),
    onInk: Color(0xFFF4F5F8),
    accent: Color(0xFF5E6AD2),
    accentHover: Color(0xFF4E5AC0),
    accentTint: Color(0x2E5E6AD2),
    accentRing: Color(0x735E6AD2),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF4CB782),
    doneBg: Color(0x294CB782),
    working: Color(0xFFF2C94C),
    workingBg: Color(0x29F2C94C),
    error: Color(0xFFEB5757),
    errorBg: Color(0x29EB5757),
    borderHairline: Color(0xFF232630),
    borderStrong: Color(0xFFF4F5F8),
    borderInput: Color(0xFF343846),
    glassHighlight: Color(0x24FFFFFF),
    glassBlurSigma: 20.0,
    glowColor: Color(0xFF5E6AD2),
    windowOpacity: 0.92,
    chrome: Color(0xD914161D),
    chromeBorder: Color(0xFF262934),
    titleBarFocused: Color(0xFF1B1E27),
    titleBarBlurred: Color(0xFF13151B),
    secondaryGlow: Color(0xFF818CF8),
    chartContext: Color(0xFF8A8F9E),
  );

  static const linearLight = warmPaperLight;

  static const nordicTacticalDark = IcColors(
    isDark: true,
    preset: IcStylePreset.nordicTactical,
    surfacePage: Color(0xFF0F1419),
    surfaceElevated: Color(0xFF151C24),
    surfaceCard: Color(0xFF1B242E),
    surfaceSunk: Color(0xFF0B0F14),
    surfaceGlass: Color(0xCC151C24),
    surfaceGlassStrong: Color(0xE61B242E),
    surfaceInk: Color(0xFFE6EDF3),
    textStrong: Color(0xFFE6EDF3),
    textBody: Color(0xFFC9D1D9),
    textMuted: Color(0xFF8B949E),
    textSubtle: Color(0xFF6E7681),
    onInk: Color(0xFF0F1419),
    accent: Color(0xFF38BDF8),
    accentHover: Color(0xFF7DD3FC),
    accentTint: Color(0x2438BDF8),
    accentRing: Color(0x6638BDF8),
    onAccent: Color(0xFF0F1419),
    done: Color(0xFF3FB950),
    doneBg: Color(0x223FB950),
    working: Color(0xFFD29922),
    workingBg: Color(0x24D29922),
    error: Color(0xFFF85149),
    errorBg: Color(0x24F85149),
    borderHairline: Color(0xFF263240),
    borderStrong: Color(0xFF38485C),
    borderInput: Color(0xFF303E50),
    glassHighlight: Color(0x2438BDF8),
    glassBlurSigma: 18.0,
    glowColor: Color(0xFF38BDF8),
    windowOpacity: 0.88,
  );

  static const nordicTacticalLight = IcColors(
    isDark: false,
    preset: IcStylePreset.nordicTactical,
    surfacePage: Color(0xFFF0F4F8),
    surfaceElevated: Color(0xFFFFFFFF),
    surfaceCard: Color(0xFFFFFFFF),
    surfaceSunk: Color(0xFFE2E8F0),
    surfaceGlass: Color(0xD9FFFFFF),
    surfaceGlassStrong: Color(0xF0FFFFFF),
    surfaceInk: Color(0xFF0F172A),
    textStrong: Color(0xFF0F172A),
    textBody: Color(0xFF1E293B),
    textMuted: Color(0xFF475569),
    textSubtle: Color(0xFF64748B),
    onInk: Color(0xFFFFFFFF),
    accent: Color(0xFF0284C7),
    accentHover: Color(0xFF0369A1),
    accentTint: Color(0x1F0284C7),
    accentRing: Color(0x590284C7),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF16A34A),
    doneBg: Color(0x1816A34A),
    working: Color(0xFFD97706),
    workingBg: Color(0x18D97706),
    error: Color(0xFFDC2626),
    errorBg: Color(0x18DC2626),
    borderHairline: Color(0xFFCBD5E1),
    borderStrong: Color(0xFF94A3B8),
    borderInput: Color(0xFFCBD5E1),
    glassHighlight: Color(0xB3FFFFFF),
    glassBlurSigma: 16.0,
    glowColor: Color(0xFF0284C7),
    windowOpacity: 0.90,
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
    IcStylePreset.fluentWarmMica =>
      c.isDark ? const Color(0xFF120F0C) : const Color(0xFFF3EFE8),
    IcStylePreset.linearCarbon => const Color(0xFF08090C),
    IcStylePreset.nordicTactical =>
      c.isDark ? const Color(0xFF0B0F14) : const Color(0xFFE2E8F0),
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

/// Ambient theme carrier so any widget can call `IcTheme.of(context)`.
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
    return IcColors.fluentDark;
  }

  @override
  bool updateShouldNotify(IcTheme oldWidget) => oldWidget.colors != colors;
}

/// Reusable WinUI 3 / visionOS Acrylic Frosted Glass container.
class IcAcrylicBox extends StatelessWidget {
  const IcAcrylicBox({
    super.key,
    required this.child,
    this.borderRadius,
    this.radius,
    this.padding,
    this.strong = false,
    this.blurSigma,
    this.sigmaOverride,
    this.fillColor,
    this.color,
    this.borderColor,
    this.shadows,
    this.specularTop = false,
  });

  final Widget child;
  final BorderRadius? borderRadius;
  final double? radius;
  final EdgeInsetsGeometry? padding;
  final bool strong;
  final double? blurSigma;
  final double? sigmaOverride;
  final Color? fillColor;
  final Color? color;
  final Color? borderColor;
  final List<BoxShadow>? shadows;
  final bool specularTop;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final effectiveRadius =
        borderRadius ?? BorderRadius.circular(radius ?? IcRadius.card);
    final sigma = sigmaOverride ?? blurSigma ?? c.blurSigma;
    final fill =
        color ?? fillColor ?? (strong ? c.surfaceGlassStrong : c.surfaceCard);
    final border =
        borderColor ?? (c.isGlass ? c.specularBorder : c.borderHairline);

    final box = Container(
      padding: padding,
      decoration: BoxDecoration(
        color: fill,
        borderRadius: effectiveRadius,
        border: Border.all(color: border),
        boxShadow: shadows,
      ),
      child: child,
    );

    if (sigma <= 0) return box;

    return ClipRRect(
      borderRadius: effectiveRadius,
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: sigma, sigmaY: sigma),
        child: box,
      ),
    );
  }
}

/// Spacing scale: 4 · 8 · 12 · 16 · 20 · 24 · 32 · 40 · 48 · 64.
abstract final class IcSpace {
  static const double s4 = 4;
  static const double s8 = 8;
  static const double s12 = 12;
  static const double s16 = 16;
  static const double s20 = 20;
  static const double s24 = 24;
  static const double s32 = 32;
  static const double s40 = 40;
  static const double s48 = 48;
  static const double s64 = 64;
}

/// Corner radii.
abstract final class IcRadius {
  static const double xs = 6.0;
  static const double sm = 8.0;
  static const double button = 10.0;
  static const double input = 12.0;
  static const double md = 12.0;
  static const double card = 14.0;
  static const double lg = 16.0;
  static const double window = 16.0;
  static const double bubble = 18.0;
  static const double panel = 20.0;
  static const double sheet = 20.0;
  static const double pill = 999.0;
}

/// Shadow scale (xs -> xl).
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

typedef IcShadows = IcShadow;

/// Motion tokens.
abstract final class IcMotion {
  static const ease = Cubic(0.2, 0.8, 0.2, 1);
  static const spring = Cubic(0.16, 1.0, 0.3, 1.0);
  static const fast = Duration(milliseconds: 120);
  static const base = Duration(milliseconds: 200);
  static const slow = Duration(milliseconds: 280);
}

/// Inter + InterDisplay typography with strict tabular figures (`tnum`).
abstract final class IcType {
  static const family = 'Inter';
  static const sans = 'Inter';
  static const display = 'InterDisplay';
  static const monoFamily = 'JetBrainsMono';
  static const _pkg = 'intochat_os_ui';
  static const package = 'intochat_os_ui';
  static const tabular = [FontFeature.tabularFigures()];

  static TextStyle text(
    double size, {
    FontWeight weight = FontWeight.w400,
    Color? color,
    bool tnum = false,
    double? height,
    double? letterSpacing,
  }) => TextStyle(
    fontFamily: family,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    height: height,
    letterSpacing: letterSpacing ?? (size >= 20 ? -0.2 : 0),
    fontFeatures: tnum ? tabular : null,
  );

  static TextStyle heading(
    double size, {
    FontWeight weight = FontWeight.w700,
    Color? color,
    double height = 1.15,
    double letterSpacing = -0.4,
  }) => TextStyle(
    fontFamily: display,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    letterSpacing: letterSpacing,
    height: height,
  );

  static TextStyle mono(
    double size, {
    FontWeight weight = FontWeight.w500,
    Color? color,
    double? height,
  }) => TextStyle(
    fontFamily: family,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    color: color,
    height: height,
    fontFeatures: tabular,
  );

  static TextStyle code(
    double size, {
    required Color color,
    FontWeight weight = FontWeight.w500,
    double height = 1.4,
  }) => TextStyle(
    fontFamily: monoFamily,
    package: _pkg,
    fontSize: size,
    fontWeight: weight,
    height: height,
    color: color,
    fontFeatures: tabular,
  );
}
