// ignore_for_file: prefer_initializing_formals

import 'dart:ui' show lerpDouble;

import 'package:flutter/material.dart';

import '../design/tokens.dart';

/// Semantic colour tokens carried on [ThemeData.extensions].
///
/// Unified with [IcColors] (`tokens.dart`) so all 7 UI Identities (`fluent-acrylic`,
/// `spatial-glass`, `cyber-synaptic`, `warm-paper`, `fluent-warm-mica`,
/// `linear-carbon`, `nordic-tactical`) are defined in one place (`tokens.dart`).
final class UiColors extends ThemeExtension<UiColors> {
  const UiColors({
    required this.page,
    required this.elevated,
    required this.surface,
    required this.recessed,
    required this.ink,
    required this.strong,
    required this.body,
    required this.muted,
    required this.subtle,
    required this.onInk,
    required this.accent,
    required this.accentHover,
    required this.accentTint,
    required this.accentBorder,
    required this.onAccent,
    Color? accentCopy,
    Color? accentCopyHover,
    Color? focus,
    required this.done,
    required this.doneBg,
    required this.working,
    required this.workingBg,
    required this.error,
    required this.errorBg,
    required this.hairline,
    required this.borderStrong,
    required this.inputBorder,
    Color? userBubble,
    Color? userText,
    Color? assistant,
    Color? assistantText,
    Color? assistantBorder,
    Color? menuHover,
    this.preset = IcStylePreset.warmPaper,
    this.blurSigma = 0.0,
    this.windowOpacity = 1.0,
    Color? specularBorder,
    Color? chrome,
    Color? chromeBorder,
    Color? titleBarFocused,
    Color? titleBarBlurred,
    Color? ambientGlow,
    Color? secondaryGlow,
    Color? chartContext,
  }) : _accentCopy = accentCopy,
       _accentCopyHover = accentCopyHover,
       _focus = focus,
       _userBubble = userBubble,
       _userText = userText,
       _assistant = assistant,
       _assistantText = assistantText,
       _assistantBorder = assistantBorder,
       _menuHover = menuHover,
       _specularBorder = specularBorder,
       _chrome = chrome,
       _chromeBorder = chromeBorder,
       _titleBarFocused = titleBarFocused,
       _titleBarBlurred = titleBarBlurred,
       _ambientGlow = ambientGlow,
       _secondaryGlow = secondaryGlow,
       _chartContext = chartContext;

  /// Creates a [UiColors] extension directly from an [IcColors] token set.
  factory UiColors.fromIc(
    IcColors ic, {
    Color? accentCopy,
    Color? accentCopyHover,
    Color? userBubble,
    Color? userText,
    Color? assistant,
    Color? assistantText,
    Color? assistantBorder,
    Color? menuHover,
  }) => UiColors(
    preset: ic.preset,
    page: ic.surfacePage,
    elevated: ic.surfaceElevated,
    surface: ic.surfaceCard,
    recessed: ic.surfaceSunk,
    ink: ic.surfaceInk,
    strong: ic.textStrong,
    body: ic.textBody,
    muted: ic.textMuted,
    subtle: ic.textSubtle,
    onInk: ic.onInk,
    accent: ic.accent,
    accentHover: ic.accentHover,
    accentTint: ic.accentTint,
    accentBorder: ic.accentRing,
    onAccent: ic.onAccent,
    accentCopy: accentCopy,
    accentCopyHover: accentCopyHover,
    focus: ic.accentRing,
    done: ic.done,
    doneBg: ic.doneBg,
    working: ic.working,
    workingBg: ic.workingBg,
    error: ic.error,
    errorBg: ic.errorBg,
    hairline: ic.borderHairline,
    borderStrong: ic.borderStrong,
    inputBorder: ic.borderInput,
    userBubble: userBubble,
    userText: userText,
    assistant: assistant,
    assistantText: assistantText,
    assistantBorder: assistantBorder,
    menuHover: menuHover,
    blurSigma: ic.glassBlurSigma,
    windowOpacity: ic.windowOpacity,
    specularBorder: ic.specularBorder,
    chrome: ic.chrome,
    chromeBorder: ic.chromeBorder,
    titleBarFocused: ic.titleBarFocused,
    titleBarBlurred: ic.titleBarBlurred,
    ambientGlow: ic.ambientGlow,
    secondaryGlow: ic.secondaryGlow,
    chartContext: ic.chartContext,
  );

  final IcStylePreset preset;
  final Color page;
  final Color elevated;
  final Color surface;
  final Color recessed;
  final Color ink;

  final Color strong;
  final Color body;
  final Color muted;
  final Color subtle;
  final Color onInk;

  final Color accent;
  final Color accentHover;
  final Color accentTint;
  final Color accentBorder;
  final Color onAccent;
  final Color? _accentCopy;
  final Color? _accentCopyHover;
  final Color? _focus;

  /// Text-contrast accent (`--color-accent-text`). Defaults to [accent].
  Color get accentCopy => _accentCopy ?? accent;

  /// Hover partner for [accentCopy]. Defaults to [accentHover].
  Color get accentCopyHover => _accentCopyHover ?? accentHover;

  /// Translucent focus ring fill (`--color-accent-focus`).
  Color get focus => _focus ?? accent.withValues(alpha: 0.35);

  final Color done;
  final Color doneBg;
  final Color working;
  final Color workingBg;
  final Color error;
  final Color errorBg;

  final Color hairline;
  final Color borderStrong;
  final Color inputBorder;

  final Color? _userBubble;
  final Color? _userText;
  final Color? _assistant;
  final Color? _assistantText;
  final Color? _assistantBorder;
  final Color? _menuHover;

  Color get userBubble => _userBubble ?? strong;
  Color get userText => _userText ?? page;
  Color get assistant => _assistant ?? surface;
  Color get assistantText => _assistantText ?? body;
  Color get assistantBorder => _assistantBorder ?? hairline;
  Color get menuHover => _menuHover ?? recessed;

  final double blurSigma;
  final double windowOpacity;
  final Color? _specularBorder;
  final Color? _chrome;
  final Color? _chromeBorder;
  final Color? _titleBarFocused;
  final Color? _titleBarBlurred;
  final Color? _ambientGlow;
  final Color? _secondaryGlow;
  final Color? _chartContext;

  bool get isGlass => blurSigma > 0 && windowOpacity < 0.99;
  Color get specularBorder => _specularBorder ?? hairline;
  Color get chrome => _chrome ?? elevated;
  Color get chromeBorder => _chromeBorder ?? hairline;
  Color get titleBarFocused => _titleBarFocused ?? recessed;
  Color get titleBarBlurred => _titleBarBlurred ?? page;
  Color get ambientGlow => _ambientGlow ?? accent;
  Color get secondaryGlow => _secondaryGlow ?? done;
  Color get chartContext => _chartContext ?? subtle;

  /// Inferred brightness from [page] luminance.
  Brightness get brightness =>
      page.computeLuminance() < 0.5 ? Brightness.dark : Brightness.light;

  bool get isDark => brightness == Brightness.dark;

  static UiColors of(BuildContext context) =>
      Theme.of(context).extension<UiColors>() ?? intochatLight;

  /// Warm Paper Light (Figma `:root`).
  static const intochatLight = UiColors(
    preset: IcStylePreset.warmPaper,
    page: Color(0xFFFBFAF7),
    elevated: Color(0xFFF5F3EE),
    surface: Color(0xFFFFFFFF),
    recessed: Color(0xFFF2EFE9),
    ink: Color(0xFF17140F),
    strong: Color(0xFF17140F),
    body: Color(0xFF2A251E),
    muted: Color(0xFF7C7264),
    subtle: Color(0xFF9C9182),
    onInk: Color(0xFFFBFAF7),
    accent: Color(0xFF1F5EFF),
    accentHover: Color(0xFF1541C7),
    accentTint: Color(0xFFE7EEFF),
    accentBorder: Color(0x591F5EFF),
    onAccent: Color(0xFFFFFFFF),
    accentCopy: Color(0xFF1F5EFF),
    accentCopyHover: Color(0xFF1541C7),
    focus: Color(0x591F5EFF),
    done: Color(0xFF12A150),
    doneBg: Color(0xFFE1F6EA),
    working: Color(0xFFE8A317),
    workingBg: Color(0xFFFCF1DA),
    error: Color(0xFFE0402F),
    errorBg: Color(0xFFFCE7E3),
    hairline: Color(0xFFE4DFD5),
    borderStrong: Color(0xFF17140F),
    inputBorder: Color(0xFFC2B9AB),
    userBubble: Color(0xFF17140F),
    userText: Color(0xFFFBFAF7),
    assistant: Color(0xFFFFFFFF),
    assistantText: Color(0xFF2A251E),
    assistantBorder: Color(0xFFE4DFD5),
    menuHover: Color(0xFFF2EFE9),
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFFE4DFD5),
    chrome: Color(0xE6FFFFFF),
    chromeBorder: Color(0xFFE4DFD5),
    titleBarFocused: Color(0xFFF2EFE9),
    titleBarBlurred: Color(0xFFFBFAF7),
    ambientGlow: Color(0xFF1F5EFF),
    secondaryGlow: Color(0xFF12A150),
    chartContext: Color(0xFF9C9182),
  );

  /// Warm Paper Dark (`[data-theme="dark"]`).
  static const intochatDark = UiColors(
    preset: IcStylePreset.warmPaper,
    page: Color(0xFF17140F),
    elevated: Color(0xFF1F1B15),
    surface: Color(0xFF2A251E),
    recessed: Color(0xFF110F0B),
    ink: Color(0xFF110F0B),
    strong: Color(0xFFFBFAF7),
    body: Color(0xFFF0ECE4),
    muted: Color(0xFF9C9182),
    subtle: Color(0xFF7C7264),
    onInk: Color(0xFF17140F),
    accent: Color(0xFF4B7DFF),
    accentHover: Color(0xFF1F5EFF),
    accentTint: Color(0x294B7DFF),
    accentBorder: Color(0x734B7DFF),
    onAccent: Color(0xFF17140F),
    accentCopy: Color(0xFFFBFAF7),
    accentCopyHover: Color(0xFFF0ECE4),
    focus: Color(0x734B7DFF),
    done: Color(0xFF8FE0AF),
    doneBg: Color(0x298FE0AF),
    working: Color(0xFFF6D28A),
    workingBg: Color(0x29F6D28A),
    error: Color(0xFFF3A99F),
    errorBg: Color(0x29F3A99F),
    hairline: Color(0xFF423B31),
    borderStrong: Color(0xFFF0ECE4),
    inputBorder: Color(0xFF5E554A),
    userBubble: Color(0xFFFBFAF7),
    userText: Color(0xFF17140F),
    assistant: Color(0xFF2A251E),
    assistantText: Color(0xFFF0ECE4),
    assistantBorder: Color(0xFF423B31),
    menuHover: Color(0x99423B31),
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFF423B31),
    chrome: Color(0xE62A251E),
    chromeBorder: Color(0xFF423B31),
    titleBarFocused: Color(0xFF332D25),
    titleBarBlurred: Color(0xFF2A251E),
    ambientGlow: Color(0xFF4B7DFF),
    secondaryGlow: Color(0xFF8FE0AF),
    chartContext: Color(0xFF9C9182),
  );

  // --- All 7 UI Identities derived directly from IcColors in tokens.dart ---
  static final fluentAcrylicDark = UiColors.fromIc(IcColors.fluentDark);
  static final fluentAcrylicLight = UiColors.fromIc(IcColors.fluentLight);
  static final spatialGlassDark = UiColors.fromIc(IcColors.spatialDark);
  static final spatialGlassLight = UiColors.fromIc(IcColors.spatialLight);
  static final cyberSynapticDark = UiColors.fromIc(IcColors.cyberDark);
  static final cyberSynapticLight = UiColors.fromIc(IcColors.cyberLight);
  static const warmPaperLight = intochatLight;
  static const warmPaperDark = intochatDark;
  static final fluentWarmMicaDark = UiColors.fromIc(IcColors.fluentWarmMicaDark);
  static final fluentWarmMicaLight = UiColors.fromIc(IcColors.fluentWarmMicaLight);
  static final linearCarbonDark = UiColors.fromIc(IcColors.linearDark);
  static final linearCarbonLight = UiColors.fromIc(IcColors.linearLight);
  static final nordicTacticalDark = UiColors.fromIc(IcColors.nordicTacticalDark);
  static final nordicTacticalLight = UiColors.fromIc(IcColors.nordicTacticalLight);

  // --- Legacy v1 workspace palettes kept for backward-compat preferences ---
  static const midnight = UiColors(
    page: Color(0xFF141720),
    elevated: Color(0xE61B1F2A),
    surface: Color(0xFF1B1F2A),
    recessed: Color(0xFF11141B),
    ink: Color(0xFF11141B),
    strong: Color(0xFFE9EBF2),
    body: Color(0xFFE9EBF2),
    muted: Color(0xFF969CAF),
    subtle: Color(0xFF5E6474),
    onInk: Color(0xFF141720),
    accent: Color(0xFF7B9BE3),
    accentHover: Color(0xFF7B9BE3),
    accentTint: Color(0x297B9BE3),
    accentBorder: Color(0x597B9BE3),
    onAccent: Color(0xFF141720),
    accentCopy: Color(0xFF7B9BE3),
    accentCopyHover: Color(0xFF7B9BE3),
    focus: Color(0x597B9BE3),
    done: Color(0xFF65C5A0),
    doneBg: Color(0x2965C5A0),
    working: Color(0xFFE09261),
    workingBg: Color(0x29E09261),
    error: Color(0xFFFFB4AB),
    errorBg: Color(0x29FFB4AB),
    hairline: Color(0xFF292E3B),
    borderStrong: Color(0xFFE9EBF2),
    inputBorder: Color(0xFF363C4C),
    userBubble: Color(0xFFE9EBF2),
    userText: Color(0xFF141720),
    assistant: Color(0xFF1B1F2A),
    assistantText: Color(0xFFE9EBF2),
    assistantBorder: Color(0xFF292E3B),
    menuHover: Color(0xFF11141B),
    blurSigma: 18.0,
    windowOpacity: 0.94,
    specularBorder: Color(0xFF292E3B),
    chrome: Color(0xE61B1F2A),
    chromeBorder: Color(0xFF292E3B),
    titleBarFocused: Color(0xFF222736),
    titleBarBlurred: Color(0xFF1B1F2A),
    ambientGlow: Color(0xFF7B9BE3),
    secondaryGlow: Color(0xFF65C5A0),
    chartContext: Color(0xFF969CAF),
  );

  static const paper = UiColors(
    page: Color(0xFFF7F7F2),
    elevated: Color(0xE6FFFCF7),
    surface: Color(0xFFFFFFFF),
    recessed: Color(0xFFF0F3EC),
    ink: Color(0xFF263934),
    strong: Color(0xFF263934),
    body: Color(0xFF263934),
    muted: Color(0xFF687970),
    subtle: Color(0xFF88958D),
    onInk: Color(0xFFFFFFFF),
    accent: Color(0xFF397B63),
    accentHover: Color(0xFF397B63),
    accentTint: Color(0xFFE4EEE5),
    accentBorder: Color(0xFFB9CBC0),
    onAccent: Color(0xFFFFFFFF),
    accentCopy: Color(0xFF397B63),
    accentCopyHover: Color(0xFF397B63),
    focus: Color(0x59397B63),
    done: Color(0xFF397B63),
    doneBg: Color(0xFFE4EEE5),
    working: Color(0xFF986A37),
    workingBg: Color(0x29986A37),
    error: Color(0xFFAD4F3F),
    errorBg: Color(0x29AD4F3F),
    hairline: Color(0xFFDBE2DA),
    borderStrong: Color(0xFF263934),
    inputBorder: Color(0xFFB9CBC0),
    userBubble: Color(0xFF263934),
    userText: Color(0xFFF7F7F2),
    assistant: Color(0xFFFFFFFF),
    assistantText: Color(0xFF263934),
    assistantBorder: Color(0xFFDBE2DA),
    menuHover: Color(0xFFF0F3EC),
    blurSigma: 0.0,
    windowOpacity: 1.0,
    specularBorder: Color(0xFFDBE2DA),
    chrome: Color(0xE6FFFCF7),
    chromeBorder: Color(0xFFDBE2DA),
    titleBarFocused: Color(0xFFF0F3EC),
    titleBarBlurred: Color(0xFFF7F7F2),
    ambientGlow: Color(0xFF397B63),
    secondaryGlow: Color(0xFF12A150),
    chartContext: Color(0xFF687970),
  );

  static const lumen = UiColors(
    page: Color(0xFF060A08),
    elevated: Color(0xE60B120F),
    surface: Color(0xFF0B120F),
    recessed: Color(0xFF0F1813),
    ink: Color(0xFF0F1813),
    strong: Color(0xFFE3EFE8),
    body: Color(0xFFE3EFE8),
    muted: Color(0xFF8CA397),
    subtle: Color(0xFF5F776A),
    onInk: Color(0xFF060A08),
    accent: Color(0xFF7CE0A8),
    accentHover: Color(0xFFA5F0C4),
    accentTint: Color(0xFF12301F),
    accentBorder: Color(0x597CE0A8),
    onAccent: Color(0xFF04170D),
    accentCopy: Color(0xFF7CE0A8),
    accentCopyHover: Color(0xFF7CE0A8),
    focus: Color(0x597CE0A8),
    done: Color(0xFF7CE0A8),
    doneBg: Color(0x297CE0A8),
    working: Color(0xFFD9A867),
    workingBg: Color(0x29D9A867),
    error: Color(0xFFFFB4AB),
    errorBg: Color(0x29FFB4AB),
    hairline: Color(0xFF1D2D24),
    borderStrong: Color(0xFFE3EFE8),
    inputBorder: Color(0xFF3F5A4A),
    userBubble: Color(0xFFE3EFE8),
    userText: Color(0xFF060A08),
    assistant: Color(0xFF0B120F),
    assistantText: Color(0xFFE3EFE8),
    assistantBorder: Color(0xFF1D2D24),
    menuHover: Color(0xFF0F1813),
    blurSigma: 16.0,
    windowOpacity: 0.94,
    specularBorder: Color(0xFF1D2D24),
    chrome: Color(0xE60B120F),
    chromeBorder: Color(0xFF1D2D24),
    titleBarFocused: Color(0xFF131E18),
    titleBarBlurred: Color(0xFF0B120F),
    ambientGlow: Color(0xFF7CE0A8),
    secondaryGlow: Color(0xFF34D399),
    chartContext: Color(0xFF8CA397),
  );

  @override
  UiColors copyWith({
    IcStylePreset? preset,
    Color? page,
    Color? elevated,
    Color? surface,
    Color? recessed,
    Color? ink,
    Color? strong,
    Color? body,
    Color? muted,
    Color? subtle,
    Color? onInk,
    Color? accent,
    Color? accentHover,
    Color? accentTint,
    Color? accentBorder,
    Color? onAccent,
    Color? accentCopy,
    Color? accentCopyHover,
    Color? focus,
    Color? done,
    Color? doneBg,
    Color? working,
    Color? workingBg,
    Color? error,
    Color? errorBg,
    Color? hairline,
    Color? borderStrong,
    Color? inputBorder,
    Color? userBubble,
    Color? userText,
    Color? assistant,
    Color? assistantText,
    Color? assistantBorder,
    Color? menuHover,
    double? blurSigma,
    double? windowOpacity,
    Color? specularBorder,
    Color? chrome,
    Color? chromeBorder,
    Color? titleBarFocused,
    Color? titleBarBlurred,
    Color? ambientGlow,
    Color? secondaryGlow,
    Color? chartContext,
  }) => UiColors(
    preset: preset ?? this.preset,
    page: page ?? this.page,
    elevated: elevated ?? this.elevated,
    surface: surface ?? this.surface,
    recessed: recessed ?? this.recessed,
    ink: ink ?? this.ink,
    strong: strong ?? this.strong,
    body: body ?? this.body,
    muted: muted ?? this.muted,
    subtle: subtle ?? this.subtle,
    onInk: onInk ?? this.onInk,
    accent: accent ?? this.accent,
    accentHover: accentHover ?? this.accentHover,
    accentTint: accentTint ?? this.accentTint,
    accentBorder: accentBorder ?? this.accentBorder,
    onAccent: onAccent ?? this.onAccent,
    accentCopy: accentCopy ?? _accentCopy,
    accentCopyHover: accentCopyHover ?? _accentCopyHover,
    focus: focus ?? _focus,
    done: done ?? this.done,
    doneBg: doneBg ?? this.doneBg,
    working: working ?? this.working,
    workingBg: workingBg ?? this.workingBg,
    error: error ?? this.error,
    errorBg: errorBg ?? this.errorBg,
    hairline: hairline ?? this.hairline,
    borderStrong: borderStrong ?? this.borderStrong,
    inputBorder: inputBorder ?? this.inputBorder,
    userBubble: userBubble ?? _userBubble,
    userText: userText ?? _userText,
    assistant: assistant ?? _assistant,
    assistantText: assistantText ?? _assistantText,
    assistantBorder: assistantBorder ?? _assistantBorder,
    menuHover: menuHover ?? _menuHover,
    blurSigma: blurSigma ?? this.blurSigma,
    windowOpacity: windowOpacity ?? this.windowOpacity,
    specularBorder: specularBorder ?? _specularBorder,
    chrome: chrome ?? _chrome,
    chromeBorder: chromeBorder ?? _chromeBorder,
    titleBarFocused: titleBarFocused ?? _titleBarFocused,
    titleBarBlurred: titleBarBlurred ?? _titleBarBlurred,
    ambientGlow: ambientGlow ?? _ambientGlow,
    secondaryGlow: secondaryGlow ?? _secondaryGlow,
    chartContext: chartContext ?? _chartContext,
  );

  @override
  UiColors lerp(ThemeExtension<UiColors>? other, double t) {
    if (other is! UiColors) return this;
    Color c(Color a, Color b) => Color.lerp(a, b, t)!;
    return UiColors(
      preset: t < 0.5 ? preset : other.preset,
      page: c(page, other.page),
      elevated: c(elevated, other.elevated),
      surface: c(surface, other.surface),
      recessed: c(recessed, other.recessed),
      ink: c(ink, other.ink),
      strong: c(strong, other.strong),
      body: c(body, other.body),
      muted: c(muted, other.muted),
      subtle: c(subtle, other.subtle),
      onInk: c(onInk, other.onInk),
      accent: c(accent, other.accent),
      accentHover: c(accentHover, other.accentHover),
      accentTint: c(accentTint, other.accentTint),
      accentBorder: c(accentBorder, other.accentBorder),
      onAccent: c(onAccent, other.onAccent),
      accentCopy: c(accentCopy, other.accentCopy),
      accentCopyHover: c(accentCopyHover, other.accentCopyHover),
      focus: c(focus, other.focus),
      done: c(done, other.done),
      doneBg: c(doneBg, other.doneBg),
      working: c(working, other.working),
      workingBg: c(workingBg, other.workingBg),
      error: c(error, other.error),
      errorBg: c(errorBg, other.errorBg),
      hairline: c(hairline, other.hairline),
      borderStrong: c(borderStrong, other.borderStrong),
      inputBorder: c(inputBorder, other.inputBorder),
      userBubble: c(userBubble, other.userBubble),
      userText: c(userText, other.userText),
      assistant: c(assistant, other.assistant),
      assistantText: c(assistantText, other.assistantText),
      assistantBorder: c(assistantBorder, other.assistantBorder),
      menuHover: c(menuHover, other.menuHover),
      blurSigma: lerpDouble(blurSigma, other.blurSigma, t)!,
      windowOpacity: lerpDouble(windowOpacity, other.windowOpacity, t)!,
      specularBorder: c(specularBorder, other.specularBorder),
      chrome: c(chrome, other.chrome),
      chromeBorder: c(chromeBorder, other.chromeBorder),
      titleBarFocused: c(titleBarFocused, other.titleBarFocused),
      titleBarBlurred: c(titleBarBlurred, other.titleBarBlurred),
      ambientGlow: c(ambientGlow, other.ambientGlow),
      secondaryGlow: c(secondaryGlow, other.secondaryGlow),
      chartContext: c(chartContext, other.chartContext),
    );
  }

  @override
  bool operator ==(Object other) =>
      other is UiColors &&
      other.preset == preset &&
      other.page == page &&
      other.elevated == elevated &&
      other.surface == surface &&
      other.recessed == recessed &&
      other.ink == ink &&
      other.strong == strong &&
      other.body == body &&
      other.muted == muted &&
      other.subtle == subtle &&
      other.onInk == onInk &&
      other.accent == accent &&
      other.accentHover == accentHover &&
      other.accentTint == accentTint &&
      other.accentBorder == accentBorder &&
      other.onAccent == onAccent &&
      other.accentCopy == accentCopy &&
      other.accentCopyHover == accentCopyHover &&
      other.focus == focus &&
      other.done == done &&
      other.doneBg == doneBg &&
      other.working == working &&
      other.workingBg == workingBg &&
      other.error == error &&
      other.errorBg == errorBg &&
      other.hairline == hairline &&
      other.borderStrong == borderStrong &&
      other.inputBorder == inputBorder &&
      other.userBubble == userBubble &&
      other.userText == userText &&
      other.assistant == assistant &&
      other.assistantText == assistantText &&
      other.assistantBorder == assistantBorder &&
      other.menuHover == menuHover;

  @override
  int get hashCode => Object.hashAll([
    preset,
    page,
    elevated,
    surface,
    recessed,
    ink,
    strong,
    body,
    muted,
    subtle,
    onInk,
    accent,
    accentHover,
    accentTint,
    accentBorder,
    onAccent,
    accentCopy,
    accentCopyHover,
    focus,
    done,
    doneBg,
    working,
    workingBg,
    error,
    errorBg,
    hairline,
    borderStrong,
    inputBorder,
    userBubble,
    userText,
    assistant,
    assistantText,
    assistantBorder,
    menuHover,
  ]);
}
