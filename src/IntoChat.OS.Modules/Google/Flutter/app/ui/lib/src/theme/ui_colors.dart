import 'dart:ui' show lerpDouble;

import 'package:flutter/material.dart';

import '../design/tokens.dart';
import '../lumen/lumen_palette.dart';

/// Semantic colour set for a UI palette, registered as a [ThemeExtension].
/// Unifies IntoChat v2 (`IcColors`) and OS theme extensions (`UiColors`).
@immutable
class UiColors extends ThemeExtension<UiColors> {
  const UiColors({
    required this.page,
    required this.recessed,
    required this.surface,
    required this.strong,
    required this.body,
    required this.muted,
    required this.subtle,
    required this.hairline,
    required this.inputBorder,
    required this.accent,
    required this.accentHover,
    required this.accentTint,
    required this.onAccent,
    required this.done,
    required this.doneBg,
    required this.working,
    required this.workingBg,
    required this.error,
    required this.errorBg,
    required this.accentCopy,
    required this.accentCopyHover,
    required this.focus,
    required this.userBubble,
    required this.userText,
    required this.assistant,
    required this.assistantText,
    required this.assistantBorder,
    required this.menuHover,
    this.isDark = false,
    this.blurSigma = 0.0,
    this.windowOpacity = 1.0,
    this.specularBorder = const Color(0xFFE4DFD5),
    this.chrome = const Color(0xE6FFFFFF),
    this.chromeBorder = const Color(0xFFE4DFD5),
    this.titleBarFocused = const Color(0xFFF2EFE9),
    this.titleBarBlurred = const Color(0xFFFBFAF7),
    this.borderStrong = const Color(0xFF17140F),
    this.tone1 = const Color(0xFFF2EFE9),
    this.tone2 = const Color(0xFFFBFAF7),
    this.tone3 = const Color(0xFFFFFFFF),
    this.tone4 = const Color(0xFFE6DFD2),
    this.chart1 = const Color(0xFF1F5EFF),
    this.chart2 = const Color(0xFF12A150),
    this.chart3 = const Color(0xFFE8A317),
    this.chart4 = const Color(0xFF9C9182),
    this.chart5 = const Color(0xFF6366F1),
    this.chart6 = const Color(0xFFE0402F),
    this.ambientGlow = const Color(0xFF1F5EFF),
    this.secondaryGlow = const Color(0xFF12A150),
  });

  final Color page;
  final Color recessed;
  final Color surface;
  final Color strong;
  final Color body;
  final Color muted;
  final Color subtle;
  final Color hairline;
  final Color inputBorder;
  final Color accent;
  final Color accentHover;
  final Color accentTint;
  final Color onAccent;
  final Color done;
  final Color doneBg;
  final Color working;
  final Color workingBg;
  final Color error;
  final Color errorBg;
  final Color accentCopy;
  final Color accentCopyHover;
  final Color focus;
  final Color userBubble;
  final Color userText;
  final Color assistant;
  final Color assistantText;
  final Color assistantBorder;
  final Color menuHover;

  // Extended IcColors & Glassmorphic Materiality fields
  final bool isDark;
  final double blurSigma;
  final double windowOpacity;
  final Color specularBorder;
  final Color chrome;
  final Color chromeBorder;
  final Color titleBarFocused;
  final Color titleBarBlurred;
  final Color borderStrong;
  final Color tone1;
  final Color tone2;
  final Color tone3;
  final Color tone4;
  final Color chart1;
  final Color chart2;
  final Color chart3;
  final Color chart4;
  final Color chart5;
  final Color chart6;
  final Color ambientGlow;
  final Color secondaryGlow;

  bool get isGlass => blurSigma > 0 && windowOpacity < 0.99;
  Brightness get brightness => isDark ? Brightness.dark : Brightness.light;

  // IcColors compatibility getters
  Color get surfacePage => page;
  Color get surfaceSunk => recessed;
  Color get surfaceCard => surface;
  Color get surfaceInk => isDark ? recessed : strong;
  Color get textStrong => strong;
  Color get textBody => body;
  Color get textMuted => muted;
  Color get textSubtle => subtle;
  Color get borderHairline => hairline;
  Color get borderInput => inputBorder;
  Color get ring => focus;
  Color get idle => subtle;
  Color get chartContext => chart4;

  /// IntoChat (Warm Ink & Paper) light palette.
  static const intochatLight = UiColors(
    isDark: false,
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFFE4DFD5),
    chrome: Color(0xE6FFFFFF),
    chromeBorder: Color(0xFFE4DFD5),
    titleBarFocused: Color(0xFFF2EFE9),
    titleBarBlurred: Color(0xFFFBFAF7),
    borderStrong: Color(0xFF17140F),
    tone1: Color(0xFFF2EFE9),
    tone2: Color(0xFFFBFAF7),
    tone3: Color(0xFFFFFFFF),
    tone4: Color(0xFFE6DFD2),
    chart1: Color(0xFF1F5EFF),
    chart2: Color(0xFF12A150),
    chart3: Color(0xFFE8A317),
    chart4: Color(0xFF9C9182),
    chart5: Color(0xFF6366F1),
    chart6: Color(0xFFE0402F),
    ambientGlow: Color(0xFF1F5EFF),
    secondaryGlow: Color(0xFF12A150),
    page: Color(0xFFFBFAF7),
    recessed: Color(0xFFF2EFE9),
    surface: Color(0xFFFFFFFF),
    strong: Color(0xFF17140F),
    body: Color(0xFF2A251E),
    muted: Color(0xFF7C7264),
    subtle: Color(0xFF9C9182),
    hairline: Color(0xFFE4DFD5),
    inputBorder: Color(0xFFC2B9AB),
    accent: Color(0xFF1F5EFF),
    accentHover: Color(0xFF1541C7),
    accentTint: Color(0xFFE7EEFF),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF12A150),
    doneBg: Color(0xFFE1F6EA),
    working: Color(0xFFE8A317),
    workingBg: Color(0xFFFCF1DA),
    error: Color(0xFFE0402F),
    errorBg: Color(0xFFFCE7E3),
    accentCopy: Color(0xFF1F5EFF),
    accentCopyHover: Color(0xFF1541C7),
    focus: Color(0x591F5EFF),
    userBubble: Color(0xFF17140F),
    userText: Color(0xFFFBFAF7),
    assistant: Color(0xFFFFFFFF),
    assistantText: Color(0xFF2A251E),
    assistantBorder: Color(0xFFE4DFD5),
    menuHover: Color(0xFFF2EFE9),
  );

  /// IntoChat (Warm Ink & Paper) dark palette; status backgrounds are 16% alpha.
  static const intochatDark = UiColors(
    isDark: true,
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFF423B31),
    chrome: Color(0xE62A251E),
    chromeBorder: Color(0xFF423B31),
    titleBarFocused: Color(0xFF332D25),
    titleBarBlurred: Color(0xFF2A251E),
    borderStrong: Color(0xFFF0ECE4),
    tone1: Color(0xFF110F0B),
    tone2: Color(0xFF17140F),
    tone3: Color(0xFF2A251E),
    tone4: Color(0xFF332D25),
    chart1: Color(0xFF4B7DFF),
    chart2: Color(0xFF8FE0AF),
    chart3: Color(0xFFF6D28A),
    chart4: Color(0xFF9C9182),
    chart5: Color(0xFF818CF8),
    chart6: Color(0xFFF3A99F),
    ambientGlow: Color(0xFF4B7DFF),
    secondaryGlow: Color(0xFF8FE0AF),
    page: Color(0xFF17140F),
    recessed: Color(0xFF110F0B),
    surface: Color(0xFF2A251E),
    strong: Color(0xFFFBFAF7),
    body: Color(0xFFF0ECE4),
    muted: Color(0xFF9C9182),
    subtle: Color(0xFF7C7264),
    hairline: Color(0xFF423B31),
    inputBorder: Color(0xFF5E554A),
    accent: Color(0xFF4B7DFF),
    accentHover: Color(0xFF1F5EFF),
    accentTint: Color(0x294B7DFF),
    onAccent: Color(0xFF17140F),
    done: Color(0xFF8FE0AF),
    doneBg: Color(0x298FE0AF),
    working: Color(0xFFF6D28A),
    workingBg: Color(0x29F6D28A),
    error: Color(0xFFF3A99F),
    errorBg: Color(0x29F3A99F),
    accentCopy: Color(0xFFFBFAF7),
    accentCopyHover: Color(0xFFF0ECE4),
    focus: Color(0x734B7DFF),
    userBubble: Color(0xFFFBFAF7),
    userText: Color(0xFF17140F),
    assistant: Color(0xFF2A251E),
    assistantText: Color(0xFFF0ECE4),
    assistantBorder: Color(0xFF423B31),
    menuHover: Color(0x99423B31),
  );

  /// Mapped from the v1 midnight palette.
  static const midnight = UiColors(
    isDark: true,
    blurSigma: 18.0,
    windowOpacity: 0.94,
    specularBorder: Color(0xFF292E3B),
    chrome: Color(0xE61B1F2A),
    chromeBorder: Color(0xFF292E3B),
    titleBarFocused: Color(0xFF222736),
    titleBarBlurred: Color(0xFF1B1F2A),
    borderStrong: Color(0xFFE9EBF2),
    tone1: Color(0xFF11141B),
    tone2: Color(0xFF141720),
    tone3: Color(0xFF1B1F2A),
    tone4: Color(0xFF292E3B),
    chart1: Color(0xFF7B9BE3),
    chart2: Color(0xFF65C5A0),
    chart3: Color(0xFFE09261),
    chart4: Color(0xFF969CAF),
    chart5: Color(0xFF818CF8),
    chart6: Color(0xFFFFB4AB),
    ambientGlow: Color(0xFF7B9BE3),
    secondaryGlow: Color(0xFF65C5A0),
    page: Color(0xFF141720),
    recessed: Color(0xFF11141B),
    surface: Color(0xFF1B1F2A),
    strong: Color(0xFFE9EBF2),
    body: Color(0xFFE9EBF2),
    muted: Color(0xFF969CAF),
    subtle: Color(0xFF5E6474),
    hairline: Color(0xFF292E3B),
    inputBorder: Color(0xFF363C4C),
    accent: Color(0xFF7B9BE3),
    accentHover: Color(0xFF7B9BE3),
    accentTint: Color(0x297B9BE3),
    onAccent: Color(0xFF141720),
    done: Color(0xFF65C5A0),
    doneBg: Color(0x2965C5A0),
    working: Color(0xFFE09261),
    workingBg: Color(0x29E09261),
    error: Color(0xFFFFB4AB),
    errorBg: Color(0x29FFB4AB),
    accentCopy: Color(0xFF7B9BE3),
    accentCopyHover: Color(0xFF7B9BE3),
    focus: Color(0x597B9BE3),
    userBubble: Color(0xFFE9EBF2),
    userText: Color(0xFF141720),
    assistant: Color(0xFF1B1F2A),
    assistantText: Color(0xFFE9EBF2),
    assistantBorder: Color(0xFF292E3B),
    menuHover: Color(0xFF11141B),
  );

  /// Mapped from the v1 light theme ([LumenPalette] values).
  static const paper = UiColors(
    isDark: false,
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: LumenPalette.line,
    chrome: Color(0xE6FFFCF7),
    chromeBorder: LumenPalette.line,
    titleBarFocused: LumenPalette.surfaceMuted,
    titleBarBlurred: LumenPalette.background,
    borderStrong: LumenPalette.ink,
    tone1: LumenPalette.surfaceMuted,
    tone2: LumenPalette.background,
    tone3: LumenPalette.surface,
    tone4: LumenPalette.line,
    chart1: LumenPalette.accent,
    chart2: Color(0xFF12A150),
    chart3: LumenPalette.warning,
    chart4: LumenPalette.muted,
    chart5: Color(0xFF2563EB),
    chart6: LumenPalette.error,
    ambientGlow: LumenPalette.accent,
    secondaryGlow: Color(0xFF12A150),
    page: LumenPalette.background,
    recessed: LumenPalette.surfaceMuted,
    surface: LumenPalette.surface,
    strong: LumenPalette.ink,
    body: LumenPalette.ink,
    muted: LumenPalette.muted,
    subtle: LumenPalette.faint,
    hairline: LumenPalette.line,
    inputBorder: LumenPalette.lineStrong,
    accent: LumenPalette.accent,
    accentHover: LumenPalette.accent,
    accentTint: LumenPalette.accentSoft,
    onAccent: LumenPalette.surface,
    done: LumenPalette.accent,
    doneBg: LumenPalette.accentSoft,
    working: LumenPalette.warning,
    workingBg: Color(0x29986A37),
    error: LumenPalette.error,
    errorBg: Color(0x29AD4F3F),
    accentCopy: LumenPalette.accent,
    accentCopyHover: LumenPalette.accent,
    focus: Color(0x59397B63),
    userBubble: LumenPalette.ink,
    userText: LumenPalette.background,
    assistant: LumenPalette.surface,
    assistantText: LumenPalette.ink,
    assistantBorder: LumenPalette.line,
    menuHover: LumenPalette.surfaceMuted,
  );

  /// Lumen: green on near-black.
  static const lumen = UiColors(
    isDark: true,
    blurSigma: 16.0,
    windowOpacity: 0.94,
    specularBorder: Color(0xFF1D2D24),
    chrome: Color(0xE60B120F),
    chromeBorder: Color(0xFF1D2D24),
    titleBarFocused: Color(0xFF131E18),
    titleBarBlurred: Color(0xFF0B120F),
    borderStrong: Color(0xFFE3EFE8),
    tone1: Color(0xFF060A08),
    tone2: Color(0xFF0B120F),
    tone3: Color(0xFF0F1813),
    tone4: Color(0xFF18261E),
    chart1: Color(0xFF7CE0A8),
    chart2: Color(0xFF34D399),
    chart3: Color(0xFFD9A867),
    chart4: Color(0xFF8CA397),
    chart5: Color(0xFF38BDF8),
    chart6: Color(0xFFFFB4AB),
    ambientGlow: Color(0xFF7CE0A8),
    secondaryGlow: Color(0xFF34D399),
    page: Color(0xFF060A08),
    recessed: Color(0xFF0F1813),
    surface: Color(0xFF0B120F),
    strong: Color(0xFFE3EFE8),
    body: Color(0xFFE3EFE8),
    muted: Color(0xFF8CA397),
    subtle: Color(0xFF5F776A),
    hairline: Color(0xFF1D2D24),
    inputBorder: Color(0xFF3F5A4A),
    accent: Color(0xFF7CE0A8),
    accentHover: Color(0xFFA5F0C4),
    accentTint: Color(0xFF12301F),
    onAccent: Color(0xFF04170D),
    done: Color(0xFF7CE0A8),
    doneBg: Color(0x297CE0A8),
    working: Color(0xFFD9A867),
    workingBg: Color(0x29D9A867),
    error: Color(0xFFFFB4AB),
    errorBg: Color(0x29FFB4AB),
    accentCopy: Color(0xFF7CE0A8),
    accentCopyHover: Color(0xFF7CE0A8),
    focus: Color(0x597CE0A8),
    userBubble: Color(0xFFE3EFE8),
    userText: Color(0xFF060A08),
    assistant: Color(0xFF0B120F),
    assistantText: Color(0xFFE3EFE8),
    assistantBorder: Color(0xFF1D2D24),
    menuHover: Color(0xFF0F1813),
  );

  /// Fluent Acrylic Glass (Light) — WinUI 3 Mica & Acrylic light identity.
  static const fluentAcrylicLight = UiColors(
    isDark: false,
    blurSigma: 24.0,
    windowOpacity: 0.86,
    specularBorder: Color(0x99FFFFFF),
    chrome: Color(0xCCFFFFFF),
    chromeBorder: Color(0xB3FFFFFF),
    titleBarFocused: Color(0xD9F1EFE9),
    titleBarBlurred: Color(0xCCF8F7F4),
    borderStrong: Color(0xFF12141A),
    tone1: Color(0xDAEDEAE2),
    tone2: Color(0xFFF7F6F2),
    tone3: Color(0xE8FFFFFF),
    tone4: Color(0xFFDDE0E7),
    chart1: Color(0xFF2563EB),
    chart2: Color(0xFF10B981),
    chart3: Color(0xFFF59E0B),
    chart4: Color(0xFF8E94A2),
    chart5: Color(0xFF6366F1),
    chart6: Color(0xFFEF4444),
    ambientGlow: Color(0xFF2563EB),
    secondaryGlow: Color(0xFF0EA5E9),
    page: Color(0xFFF7F6F2),
    recessed: Color(0xDAEDEAE2),
    surface: Color(0xE8FFFFFF),
    strong: Color(0xFF12141A),
    body: Color(0xFF252830),
    muted: Color(0xFF686D7A),
    subtle: Color(0xFF8E94A2),
    hairline: Color(0xFFDDE0E7),
    inputBorder: Color(0xFFB8BECB),
    accent: Color(0xFF2563EB),
    accentHover: Color(0xFF1D4ED8),
    accentTint: Color(0xFFE0EAFF),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF10B981),
    doneBg: Color(0xFFDFF9EE),
    working: Color(0xFFF59E0B),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFEF4444),
    errorBg: Color(0xFFFEE2E2),
    accentCopy: Color(0xFF2563EB),
    accentCopyHover: Color(0xFF1D4ED8),
    focus: Color(0x592563EB),
    userBubble: Color(0xFF12141A),
    userText: Color(0xFFFFFFFF),
    assistant: Color(0xE8FFFFFF),
    assistantText: Color(0xFF252830),
    assistantBorder: Color(0xFFDDE0E7),
    menuHover: Color(0xDAEDEAE2),
  );

  /// Fluent Acrylic Glass (Dark) — Default glassmorphic OS identity.
  static const fluentAcrylicDark = UiColors(
    isDark: true,
    blurSigma: 26.0,
    windowOpacity: 0.82,
    specularBorder: Color(0x2EFFFFFF),
    chrome: Color(0xBF171B26),
    chromeBorder: Color(0x38FFFFFF),
    titleBarFocused: Color(0xD9222736),
    titleBarBlurred: Color(0xBF181C26),
    borderStrong: Color(0xFFF1F5F9),
    tone1: Color(0xFF070910),
    tone2: Color(0xFF0B0E17),
    tone3: Color(0xD9151B2B),
    tone4: Color(0xFF222736),
    chart1: Color(0xFF4F8BFF),
    chart2: Color(0xFF34D399),
    chart3: Color(0xFFFBBF24),
    chart4: Color(0xFF94A3B8),
    chart5: Color(0xFF06B6D4),
    chart6: Color(0xFFF87171),
    ambientGlow: Color(0xFF3B82F6),
    secondaryGlow: Color(0xFF06B6D4),
    page: Color(0xFF0B0E17),
    recessed: Color(0xFF070910),
    surface: Color(0xD9151B2B),
    strong: Color(0xFFF8FAFC),
    body: Color(0xFFE2E8F0),
    muted: Color(0xFF94A3B8),
    subtle: Color(0xFF64748B),
    hairline: Color(0x2894A3B8),
    inputBorder: Color(0x4594A3B8),
    accent: Color(0xFF4F8BFF),
    accentHover: Color(0xFF2B6FFF),
    accentTint: Color(0x334F8BFF),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF34D399),
    doneBg: Color(0x2934D399),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29FBBF24),
    error: Color(0xFFF87171),
    errorBg: Color(0x29F87171),
    accentCopy: Color(0xFF4F8BFF),
    accentCopyHover: Color(0xFF2B6FFF),
    focus: Color(0x734F8BFF),
    userBubble: Color(0xFF4F8BFF),
    userText: Color(0xFFFFFFFF),
    assistant: Color(0xD9151B2B),
    assistantText: Color(0xFFE2E8F0),
    assistantBorder: Color(0x2894A3B8),
    menuHover: Color(0x331E293B),
  );

  /// Spatial Glass (Light) — VisionOS frosted pearl identity.
  static const spatialGlassLight = UiColors(
    isDark: false,
    blurSigma: 34.0,
    windowOpacity: 0.74,
    specularBorder: Color(0xCCFFFFFF),
    chrome: Color(0xB8FFFFFF),
    chromeBorder: Color(0xCCFFFFFF),
    titleBarFocused: Color(0xCCE2E8F0),
    titleBarBlurred: Color(0xB3F1F5F9),
    borderStrong: Color(0xFF0F172A),
    tone1: Color(0xB3E2E8F0),
    tone2: Color(0xCCF1F5F9),
    tone3: Color(0xD9FFFFFF),
    tone4: Color(0xFFCBD5E1),
    chart1: Color(0xFF0284C7),
    chart2: Color(0xFF059669),
    chart3: Color(0xFFD97706),
    chart4: Color(0xFF64748B),
    chart5: Color(0xFF6366F1),
    chart6: Color(0xFFE11D48),
    ambientGlow: Color(0xFF0284C7),
    secondaryGlow: Color(0xFF6366F1),
    page: Color(0xFFF1F5F9),
    recessed: Color(0xFFE2E8F0),
    surface: Color(0xD9FFFFFF),
    strong: Color(0xFF0F172A),
    body: Color(0xFF1E293B),
    muted: Color(0xFF64748B),
    subtle: Color(0xFF94A3B8),
    hairline: Color(0x99CBD5E1),
    inputBorder: Color(0xFF94A3B8),
    accent: Color(0xFF0284C7),
    accentHover: Color(0xFF0369A1),
    accentTint: Color(0xFFE0F2FE),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF059669),
    doneBg: Color(0xFFD1FAE5),
    working: Color(0xFFD97706),
    workingBg: Color(0xFFFEF3C7),
    error: Color(0xFFE11D48),
    errorBg: Color(0xFFFFE4E6),
    accentCopy: Color(0xFF0284C7),
    accentCopyHover: Color(0xFF0369A1),
    focus: Color(0x590284C7),
    userBubble: Color(0xFF0284C7),
    userText: Color(0xFFFFFFFF),
    assistant: Color(0xD9FFFFFF),
    assistantText: Color(0xFF1E293B),
    assistantBorder: Color(0x99CBD5E1),
    menuHover: Color(0xFFE2E8F0),
  );

  /// Spatial Glass (Dark) — Deep violet/azure spatial glass identity.
  static const spatialGlassDark = UiColors(
    isDark: true,
    blurSigma: 36.0,
    windowOpacity: 0.72,
    specularBorder: Color(0x45FFFFFF),
    chrome: Color(0xA6131C2E),
    chromeBorder: Color(0x4DFFFFFF),
    titleBarFocused: Color(0xCC1E293B),
    titleBarBlurred: Color(0xA6131C2E),
    borderStrong: Color(0xFFF8FAFC),
    tone1: Color(0xFF05040B),
    tone2: Color(0xFF090812),
    tone3: Color(0xBF18152E),
    tone4: Color(0xFF242042),
    chart1: Color(0xFF8B5CF6),
    chart2: Color(0xFF34D399),
    chart3: Color(0xFFFBBF24),
    chart4: Color(0xFFA39BBF),
    chart5: Color(0xFF38BDF8),
    chart6: Color(0xFFFB7185),
    ambientGlow: Color(0xFF38BDF8),
    secondaryGlow: Color(0xFF818CF8),
    page: Color(0xFF090812),
    recessed: Color(0xFF05040B),
    surface: Color(0xBF18152E),
    strong: Color(0xFFFAF8FF),
    body: Color(0xFFE9E4F8),
    muted: Color(0xFFA39BBF),
    subtle: Color(0xFF726A90),
    hairline: Color(0x2EA78BFA),
    inputBorder: Color(0x52A78BFA),
    accent: Color(0xFF8B5CF6),
    accentHover: Color(0xFF7C3AED),
    accentTint: Color(0x388B5CF6),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF34D399),
    doneBg: Color(0x2934D399),
    working: Color(0xFFFBBF24),
    workingBg: Color(0x29FBBF24),
    error: Color(0xFFFB7185),
    errorBg: Color(0x29FB7185),
    accentCopy: Color(0xFFA78BFA),
    accentCopyHover: Color(0xFF8B5CF6),
    focus: Color(0x738B5CF6),
    userBubble: Color(0xFF8B5CF6),
    userText: Color(0xFFFFFFFF),
    assistant: Color(0xBF18152E),
    assistantText: Color(0xFFE9E4F8),
    assistantBorder: Color(0x2EA78BFA),
    menuHover: Color(0x388B5CF6),
  );

  /// Cyber Synaptic (Dark) — Obsidian & neural cyan/emerald telemetry identity.
  static const cyberSynapticDark = UiColors(
    isDark: true,
    blurSigma: 28.0,
    windowOpacity: 0.80,
    specularBorder: Color(0x4010B981),
    chrome: Color(0xBF091217),
    chromeBorder: Color(0x4D10B981),
    titleBarFocused: Color(0xD90F1E26),
    titleBarBlurred: Color(0xBF091217),
    borderStrong: Color(0xFF34D399),
    tone1: Color(0xFF030609),
    tone2: Color(0xFF050A0E),
    tone3: Color(0xD90B141D),
    tone4: Color(0xFF132A2E),
    chart1: Color(0xFF06B6D4),
    chart2: Color(0xFF10B981),
    chart3: Color(0xFFF59E0B),
    chart4: Color(0xFF6796A8),
    chart5: Color(0xFF38BDF8),
    chart6: Color(0xFFF43F5E),
    ambientGlow: Color(0xFF10B981),
    secondaryGlow: Color(0xFF06B6D4),
    page: Color(0xFF050A0E),
    recessed: Color(0xFF030609),
    surface: Color(0xD90B141D),
    strong: Color(0xFFECFEFF),
    body: Color(0xFFCFFAFE),
    muted: Color(0xFF6796A8),
    subtle: Color(0xFF456D7D),
    hairline: Color(0x3306B6D4),
    inputBorder: Color(0x5906B6D4),
    accent: Color(0xFF06B6D4),
    accentHover: Color(0xFF22D3EE),
    accentTint: Color(0x2E06B6D4),
    onAccent: Color(0xFF030609),
    done: Color(0xFF10B981),
    doneBg: Color(0x2910B981),
    working: Color(0xFFF59E0B),
    workingBg: Color(0x29F59E0B),
    error: Color(0xFFF43F5E),
    errorBg: Color(0x29F43F5E),
    accentCopy: Color(0xFF22D3EE),
    accentCopyHover: Color(0xFF06B6D4),
    focus: Color(0x7306B6D4),
    userBubble: Color(0xFF06B6D4),
    userText: Color(0xFF030609),
    assistant: Color(0xD90B141D),
    assistantText: Color(0xFFCFFAFE),
    assistantBorder: Color(0x3306B6D4),
    menuHover: Color(0x2E06B6D4),
  );

  /// Linear Carbon (Dark) — Ultra-crisp monochrome studio identity.
  static const linearCarbonDark = UiColors(
    isDark: true,
    blurSigma: 20.0,
    windowOpacity: 0.92,
    specularBorder: Color(0x24FFFFFF),
    chrome: Color(0xD914161D),
    chromeBorder: Color(0xFF262934),
    titleBarFocused: Color(0xFF1B1E27),
    titleBarBlurred: Color(0xFF13151B),
    borderStrong: Color(0xFFFAFAFA),
    tone1: Color(0xFF050506),
    tone2: Color(0xFF09090B),
    tone3: Color(0xFF141417),
    tone4: Color(0xFF1E1E22),
    chart1: Color(0xFF6366F1),
    chart2: Color(0xFF22C55E),
    chart3: Color(0xFFEAB308),
    chart4: Color(0xFFA1A1AA),
    chart5: Color(0xFF818CF8),
    chart6: Color(0xFFEF4444),
    ambientGlow: Color(0xFF5E6AD2),
    secondaryGlow: Color(0xFF818CF8),
    page: Color(0xFF09090B),
    recessed: Color(0xFF050506),
    surface: Color(0xFF141417),
    strong: Color(0xFFFAFAFA),
    body: Color(0xFFE4E4E7),
    muted: Color(0xFFA1A1AA),
    subtle: Color(0xFF71717A),
    hairline: Color(0xFF27272A),
    inputBorder: Color(0xFF3F3F46),
    accent: Color(0xFF6366F1),
    accentHover: Color(0xFF818CF8),
    accentTint: Color(0x2E6366F1),
    onAccent: Color(0xFFFFFFFF),
    done: Color(0xFF22C55E),
    doneBg: Color(0x2922C55E),
    working: Color(0xFFEAB308),
    workingBg: Color(0x29EAB308),
    error: Color(0xFFEF4444),
    errorBg: Color(0x29EF4444),
    accentCopy: Color(0xFF818CF8),
    accentCopyHover: Color(0xFF6366F1),
    focus: Color(0x736366F1),
    userBubble: Color(0xFF6366F1),
    userText: Color(0xFFFFFFFF),
    assistant: Color(0xFF141417),
    assistantText: Color(0xFFE4E4E7),
    assistantBorder: Color(0xFF27272A),
    menuHover: Color(0xFF1E1E22),
  );

  /// Resolves the colours from [Theme], falling back to [IcTheme] or
  /// [intochatLight] when no [UiColors] extension is registered.
  static UiColors of(BuildContext context) =>
      Theme.of(context).extension<UiColors>() ??
      IcTheme.maybeOf(context)?.toUiColors() ??
      intochatLight;

  @override
  bool operator ==(Object other) =>
      other is UiColors &&
      other.page == page &&
      other.recessed == recessed &&
      other.surface == surface &&
      other.strong == strong &&
      other.body == body &&
      other.muted == muted &&
      other.subtle == subtle &&
      other.hairline == hairline &&
      other.inputBorder == inputBorder &&
      other.accent == accent &&
      other.accentHover == accentHover &&
      other.accentTint == accentTint &&
      other.onAccent == onAccent &&
      other.done == done &&
      other.doneBg == doneBg &&
      other.working == working &&
      other.workingBg == workingBg &&
      other.error == error &&
      other.errorBg == errorBg &&
      other.accentCopy == accentCopy &&
      other.accentCopyHover == accentCopyHover &&
      other.focus == focus &&
      other.userBubble == userBubble &&
      other.userText == userText &&
      other.assistant == assistant &&
      other.assistantText == assistantText &&
      other.assistantBorder == assistantBorder &&
      other.menuHover == menuHover &&
      other.isDark == isDark &&
      other.blurSigma == blurSigma &&
      other.windowOpacity == windowOpacity &&
      other.specularBorder == specularBorder &&
      other.chrome == chrome &&
      other.chromeBorder == chromeBorder &&
      other.titleBarFocused == titleBarFocused &&
      other.titleBarBlurred == titleBarBlurred &&
      other.borderStrong == borderStrong &&
      other.tone1 == tone1 &&
      other.tone2 == tone2 &&
      other.tone3 == tone3 &&
      other.tone4 == tone4 &&
      other.chart1 == chart1 &&
      other.chart2 == chart2 &&
      other.chart3 == chart3 &&
      other.chart4 == chart4 &&
      other.chart5 == chart5 &&
      other.chart6 == chart6 &&
      other.ambientGlow == ambientGlow &&
      other.secondaryGlow == secondaryGlow;

  @override
  int get hashCode => Object.hashAll([
    page,
    recessed,
    surface,
    strong,
    body,
    muted,
    subtle,
    hairline,
    inputBorder,
    accent,
    accentHover,
    accentTint,
    onAccent,
    done,
    doneBg,
    working,
    workingBg,
    error,
    errorBg,
    accentCopy,
    accentCopyHover,
    focus,
    userBubble,
    userText,
    assistant,
    assistantText,
    assistantBorder,
    menuHover,
    isDark,
    blurSigma,
    windowOpacity,
    specularBorder,
    chrome,
    chromeBorder,
    titleBarFocused,
    titleBarBlurred,
    borderStrong,
    tone1,
    tone2,
    tone3,
    tone4,
    chart1,
    chart2,
    chart3,
    chart4,
    chart5,
    chart6,
    ambientGlow,
    secondaryGlow,
  ]);

  @override
  UiColors copyWith({
    Color? page,
    Color? recessed,
    Color? surface,
    Color? strong,
    Color? body,
    Color? muted,
    Color? subtle,
    Color? hairline,
    Color? inputBorder,
    Color? accent,
    Color? accentHover,
    Color? accentTint,
    Color? onAccent,
    Color? done,
    Color? doneBg,
    Color? working,
    Color? workingBg,
    Color? error,
    Color? errorBg,
    Color? accentCopy,
    Color? accentCopyHover,
    Color? focus,
    Color? userBubble,
    Color? userText,
    Color? assistant,
    Color? assistantText,
    Color? assistantBorder,
    Color? menuHover,
    bool? isDark,
    double? blurSigma,
    double? windowOpacity,
    Color? specularBorder,
    Color? chrome,
    Color? chromeBorder,
    Color? titleBarFocused,
    Color? titleBarBlurred,
    Color? borderStrong,
    Color? tone1,
    Color? tone2,
    Color? tone3,
    Color? tone4,
    Color? chart1,
    Color? chart2,
    Color? chart3,
    Color? chart4,
    Color? chart5,
    Color? chart6,
    Color? ambientGlow,
    Color? secondaryGlow,
  }) {
    return UiColors(
      page: page ?? this.page,
      recessed: recessed ?? this.recessed,
      surface: surface ?? this.surface,
      strong: strong ?? this.strong,
      body: body ?? this.body,
      muted: muted ?? this.muted,
      subtle: subtle ?? this.subtle,
      hairline: hairline ?? this.hairline,
      inputBorder: inputBorder ?? this.inputBorder,
      accent: accent ?? this.accent,
      accentHover: accentHover ?? this.accentHover,
      accentTint: accentTint ?? this.accentTint,
      onAccent: onAccent ?? this.onAccent,
      done: done ?? this.done,
      doneBg: doneBg ?? this.doneBg,
      working: working ?? this.working,
      workingBg: workingBg ?? this.workingBg,
      error: error ?? this.error,
      errorBg: errorBg ?? this.errorBg,
      accentCopy: accentCopy ?? this.accentCopy,
      accentCopyHover: accentCopyHover ?? this.accentCopyHover,
      focus: focus ?? this.focus,
      userBubble: userBubble ?? this.userBubble,
      userText: userText ?? this.userText,
      assistant: assistant ?? this.assistant,
      assistantText: assistantText ?? this.assistantText,
      assistantBorder: assistantBorder ?? this.assistantBorder,
      menuHover: menuHover ?? this.menuHover,
      isDark: isDark ?? this.isDark,
      blurSigma: blurSigma ?? this.blurSigma,
      windowOpacity: windowOpacity ?? this.windowOpacity,
      specularBorder: specularBorder ?? this.specularBorder,
      chrome: chrome ?? this.chrome,
      chromeBorder: chromeBorder ?? this.chromeBorder,
      titleBarFocused: titleBarFocused ?? this.titleBarFocused,
      titleBarBlurred: titleBarBlurred ?? this.titleBarBlurred,
      borderStrong: borderStrong ?? this.borderStrong,
      tone1: tone1 ?? this.tone1,
      tone2: tone2 ?? this.tone2,
      tone3: tone3 ?? this.tone3,
      tone4: tone4 ?? this.tone4,
      chart1: chart1 ?? this.chart1,
      chart2: chart2 ?? this.chart2,
      chart3: chart3 ?? this.chart3,
      chart4: chart4 ?? this.chart4,
      chart5: chart5 ?? this.chart5,
      chart6: chart6 ?? this.chart6,
      ambientGlow: ambientGlow ?? this.ambientGlow,
      secondaryGlow: secondaryGlow ?? this.secondaryGlow,
    );
  }

  @override
  UiColors lerp(ThemeExtension<UiColors>? other, double t) {
    if (other is! UiColors) return this;
    if (t == 0.0) return this;
    if (t == 1.0) return other;
    return UiColors(
      page: Color.lerp(page, other.page, t)!,
      recessed: Color.lerp(recessed, other.recessed, t)!,
      surface: Color.lerp(surface, other.surface, t)!,
      strong: Color.lerp(strong, other.strong, t)!,
      body: Color.lerp(body, other.body, t)!,
      muted: Color.lerp(muted, other.muted, t)!,
      subtle: Color.lerp(subtle, other.subtle, t)!,
      hairline: Color.lerp(hairline, other.hairline, t)!,
      inputBorder: Color.lerp(inputBorder, other.inputBorder, t)!,
      accent: Color.lerp(accent, other.accent, t)!,
      accentHover: Color.lerp(accentHover, other.accentHover, t)!,
      accentTint: Color.lerp(accentTint, other.accentTint, t)!,
      onAccent: Color.lerp(onAccent, other.onAccent, t)!,
      done: Color.lerp(done, other.done, t)!,
      doneBg: Color.lerp(doneBg, other.doneBg, t)!,
      working: Color.lerp(working, other.working, t)!,
      workingBg: Color.lerp(workingBg, other.workingBg, t)!,
      error: Color.lerp(error, other.error, t)!,
      errorBg: Color.lerp(errorBg, other.errorBg, t)!,
      accentCopy: Color.lerp(accentCopy, other.accentCopy, t)!,
      accentCopyHover: Color.lerp(accentCopyHover, other.accentCopyHover, t)!,
      focus: Color.lerp(focus, other.focus, t)!,
      userBubble: Color.lerp(userBubble, other.userBubble, t)!,
      userText: Color.lerp(userText, other.userText, t)!,
      assistant: Color.lerp(assistant, other.assistant, t)!,
      assistantText: Color.lerp(assistantText, other.assistantText, t)!,
      assistantBorder: Color.lerp(assistantBorder, other.assistantBorder, t)!,
      menuHover: Color.lerp(menuHover, other.menuHover, t)!,
      isDark: t < 0.5 ? isDark : other.isDark,
      blurSigma: lerpDouble(blurSigma, other.blurSigma, t)!,
      windowOpacity: lerpDouble(windowOpacity, other.windowOpacity, t)!,
      specularBorder: Color.lerp(specularBorder, other.specularBorder, t)!,
      chrome: Color.lerp(chrome, other.chrome, t)!,
      chromeBorder: Color.lerp(chromeBorder, other.chromeBorder, t)!,
      titleBarFocused: Color.lerp(titleBarFocused, other.titleBarFocused, t)!,
      titleBarBlurred: Color.lerp(titleBarBlurred, other.titleBarBlurred, t)!,
      borderStrong: Color.lerp(borderStrong, other.borderStrong, t)!,
      tone1: Color.lerp(tone1, other.tone1, t)!,
      tone2: Color.lerp(tone2, other.tone2, t)!,
      tone3: Color.lerp(tone3, other.tone3, t)!,
      tone4: Color.lerp(tone4, other.tone4, t)!,
      chart1: Color.lerp(chart1, other.chart1, t)!,
      chart2: Color.lerp(chart2, other.chart2, t)!,
      chart3: Color.lerp(chart3, other.chart3, t)!,
      chart4: Color.lerp(chart4, other.chart4, t)!,
      chart5: Color.lerp(chart5, other.chart5, t)!,
      chart6: Color.lerp(chart6, other.chart6, t)!,
      ambientGlow: Color.lerp(ambientGlow, other.ambientGlow, t)!,
      secondaryGlow: Color.lerp(secondaryGlow, other.secondaryGlow, t)!,
    );
  }
}
