import 'package:flutter/material.dart';

import '../lumen/lumen_palette.dart';

/// Semantic colour set for a UI palette, registered as a [ThemeExtension].
/// IntoChat values are Figma's tokens verbatim; midnight, paper and lumen map
/// the v1 palettes so existing looks stay unchanged.
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

  /// IntoChat (Figma) light palette.
  static const intochatLight = UiColors(
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

  /// IntoChat (Figma) dark palette; status backgrounds are 16% alpha.
  static const intochatDark = UiColors(
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

  /// Mapped from the v1 midnight palette (v1 product palette). Error is the dark theme's Material
  /// error
  /// (`ColorScheme.fromSeed`); accentHover, status backgrounds (16% alpha)
  /// and accentTint are derived.
  static const midnight = UiColors(
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

  /// Mapped from the v1 light theme ([LumenPalette] values). done=accent,
  /// accentHover and the 16% alpha status backgrounds are derived.
  static const paper = UiColors(
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

  /// Lumen: green on near-black, from `UiTheme.workspaceDark('lumen')`.
  /// subtle, working and the 16% alpha backgrounds are derived; done=accent;
  /// error is the dark base scheme's Material error.
  static const lumen = UiColors(
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

  /// Resolves the colours from [Theme], falling back to [intochatLight] when
  /// no [UiColors] extension is registered.
  static UiColors of(BuildContext context) =>
      Theme.of(context).extension<UiColors>() ?? intochatLight;

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
      other.menuHover == menuHover;

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
    );
  }

  @override
  UiColors lerp(ThemeExtension<UiColors>? other, double t) {
    if (other is! UiColors) return this;
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
    );
  }
}
