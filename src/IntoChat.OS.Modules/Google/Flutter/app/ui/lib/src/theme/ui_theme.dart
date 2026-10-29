import 'package:flutter/material.dart';
import 'package:flutter_chat_core/flutter_chat_core.dart';
import 'package:forui/forui.dart';
import 'package:material_ui/material_ui.dart' as material;

import '../lumen/lumen_palette.dart';
import 'ui_colors.dart';
import 'ui_tokens.dart';

/// Text styles for the ambient palette. Colours come from [UiColors.of];
/// families are IntoChat's bundled fonts for the intochat palette and the v1
/// system fonts for midnight, paper and lumen.
@immutable
final class UiTypeSet {
  const UiTypeSet({
    required this.title,
    required this.heading,
    required this.cardTitle,
    required this.metric,
    required this.body,
    required this.bodyMuted,
    required this.meta,
    required this.metaStrong,
    required this.empty,
    required this.control,
    required this.fieldLabel,
    required this.menuItem,
    required this.menuLabel,
    required this.toast,
    required this.rowTitle,
    required this.caption,
  });

  final TextStyle title;
  final TextStyle heading;
  final TextStyle cardTitle;
  final TextStyle metric;
  final TextStyle body;
  final TextStyle bodyMuted;
  final TextStyle meta;
  final TextStyle metaStrong;
  final TextStyle empty;
  final TextStyle control;
  final TextStyle fieldLabel;
  final TextStyle menuItem;
  final TextStyle menuLabel;
  final TextStyle toast;

  /// Figma `.model-option strong`: a 13 px row title.
  final TextStyle rowTitle;

  /// A 12 px compact label (toolbar pickers).
  final TextStyle caption;
}

abstract final class UiType {
  static const fontPackage = 'intochat_os_ui';
  static const sansFamily = 'Schibsted Grotesk';
  static const intochatMonoFamily = 'JetBrains Mono';

  /// Package-qualified family names usable from any package.
  static const sans = 'packages/$fontPackage/$sansFamily';
  static const mono = 'packages/$fontPackage/$intochatMonoFamily';

  /// IntoChat heading treatment: w800, -0.015em tracking, 1.18 line height.
  static TextStyle intochatHeading(TextStyle style) => style.copyWith(
    fontWeight: FontWeight.w800,
    letterSpacing: -0.015 * (style.fontSize ?? 16),
    height: 1.18,
  );

  /// Pins the variable font's `wght` axis to [TextStyle.fontWeight] (default
  /// 400) so bundled variable fonts render the requested weight.
  static TextStyle withWeightAxis(TextStyle style) => style.copyWith(
    fontVariations: [
      FontVariation(
        'wght',
        (style.fontWeight ?? FontWeight.w400).value.toDouble(),
      ),
    ],
  );

  static UiTypeSet of(BuildContext context) {
    final c = UiColors.of(context);
    // Family comes from the theme's text, not from UiColors: colours are
    // lerped during theme animation, the font family is not.
    final intochat =
        Theme.of(context).textTheme.bodyMedium?.fontFamily
            ?.contains(sansFamily) ??
        false;
    TextStyle s(
      TextStyle v1,
      Color color, {
      bool isMono = false,
      bool head = false,
    }) {
      final style = v1.copyWith(
        fontFamily: intochat ? (isMono ? mono : sans) : null,
        color: color,
      );
      if (!intochat) return style;
      return withWeightAxis(head ? intochatHeading(style) : style);
    }

    return UiTypeSet(
      title: s(_title, c.strong),
      heading: s(_heading, c.strong, head: true),
      cardTitle: s(_cardTitle, c.strong),
      metric: s(_metric, c.strong),
      body: s(_body, c.body),
      bodyMuted: s(_bodyMuted, c.muted),
      meta: s(_meta, c.muted, isMono: true),
      metaStrong: s(_metaStrong, c.strong, isMono: true),
      empty: s(_empty, c.strong, head: true),
      control: s(_control, c.strong),
      fieldLabel: s(_fieldLabel, c.strong),
      menuItem: s(_menuItem, c.body),
      menuLabel: s(_menuLabel, c.muted, isMono: true),
      toast: s(_toast, c.body),
      rowTitle: s(_rowTitle, c.strong),
      caption: s(_caption, c.body),
    );
  }

  static const monoFamily = 'Cascadia Mono';
  static const monoFallback = ['Consolas', 'Menlo', 'monospace'];

  static const bodyFamily = 'Segoe UI Variable Text';
  static const bodyFallback = ['Segoe UI', 'Inter', 'Roboto'];
}

/// v1 (midnight) base styles; [UiType.of] recolours them per palette.
final _title = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 15,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.2,
  color: UiColors.midnight.strong,
);

final _heading = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 28,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.8,
  color: UiColors.midnight.strong,
);

final _cardTitle = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 15,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.15,
  color: UiColors.midnight.strong,
);

final _metric = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 17,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.2,
  color: UiColors.midnight.strong,
);

final _body = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 15,
  height: 1.55,
  color: UiColors.midnight.body,
);

final _bodyMuted = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  height: 1.5,
  color: UiColors.midnight.muted,
);

final _meta = TextStyle(
  fontFamily: UiType.monoFamily,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w500,
  letterSpacing: 0.45,
  color: UiColors.midnight.muted,
);

final _metaStrong = TextStyle(
  fontFamily: UiType.monoFamily,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w600,
  letterSpacing: 0.45,
  color: UiColors.midnight.strong,
);

final _empty = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 18,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.3,
  color: UiColors.midnight.strong,
);

final _control = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  fontWeight: FontWeight.w700,
  color: UiColors.midnight.strong,
);

final _fieldLabel = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  fontWeight: FontWeight.w600,
  color: UiColors.midnight.strong,
);

final _menuItem = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

final _menuLabel = TextStyle(
  fontFamily: UiType.monoFamily,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w500,
  letterSpacing: 0.88,
  color: UiColors.midnight.strong,
);

final _rowTitle = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

final _caption = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 12,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.body,
);

final _toast = TextStyle(
  fontFamily: UiType.bodyFamily,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

/// v1 navigation chrome colour; midnight-only, so not a [UiColors] field.
const _midnightNavigation = Color(0xFF101219);

abstract final class UiTheme {
  static const palettes = [
    'intochat',
    'midnight',
    'paper',
    'lumen',
    'fluent-acrylic',
    'spatial-glass',
    'cyber-synaptic',
    'linear-carbon',
  ];

  static String paletteName(String? stored) => switch (stored) {
    'dark' || 'midnight' => 'midnight',
    'light' || 'paper' || 'warm-paper' => 'paper',
    'lumen' => 'lumen',
    'fluent-acrylic' => 'fluent-acrylic',
    'spatial-glass' => 'spatial-glass',
    'cyber-synaptic' => 'cyber-synaptic',
    'linear-carbon' => 'linear-carbon',
    _ => 'intochat',
  };

  static ThemeMode modeFor(String? stored) => switch (paletteName(stored)) {
    'intochat' => ThemeMode.system,
    'paper' => ThemeMode.light,
    _ => ThemeMode.dark,
  };

  /// Light theme for a stored palette. v1 palettes reuse their unchanged
  /// code path and only gain their [UiColors].
  static ThemeData light(String? stored) => switch (paletteName(stored)) {
    'intochat' => intochat(Brightness.light),
    _ => _withColors(workspace(Brightness.light), UiColors.paper),
  };

  /// Dark theme for a stored palette.
  static ThemeData dark(String? stored) => switch (paletteName(stored)) {
    'intochat' => intochat(Brightness.dark),
    'lumen' => _withColors(workspaceDark(stored), UiColors.lumen),
    'fluent-acrylic' => _withColors(
      workspaceDark(stored),
      UiColors.fluentAcrylicDark,
    ),
    'spatial-glass' => _withColors(
      workspaceDark(stored),
      UiColors.spatialGlassDark,
    ),
    'cyber-synaptic' => _withColors(
      workspaceDark(stored),
      UiColors.cyberSynapticDark,
    ),
    'linear-carbon' => _withColors(
      workspaceDark(stored),
      UiColors.linearCarbonDark,
    ),
    _ => _withColors(workspaceDark(stored), UiColors.midnight),
  };

  static ThemeData _withColors(ThemeData base, UiColors colors) =>
      base.copyWith(
        extensions: [
          ...base.extensions.values.where((e) => e is! UiColors),
          colors,
        ],
      );

  /// IntoChat (Figma) theme: Schibsted Grotesk on warm neutrals, blue accent.
  static ThemeData intochat(Brightness brightness) {
    final c = brightness == Brightness.dark
        ? UiColors.intochatDark
        : UiColors.intochatLight;
    final scheme = ColorScheme(
      brightness: brightness,
      primary: c.accent,
      onPrimary: c.onAccent,
      primaryContainer: c.accentTint,
      onPrimaryContainer: c.strong,
      secondary: c.accent,
      onSecondary: c.onAccent,
      error: c.error,
      onError: c.onAccent,
      errorContainer: c.errorBg,
      onErrorContainer: c.error,
      surface: c.surface,
      onSurface: c.body,
      onSurfaceVariant: c.muted,
      surfaceContainerLowest: c.surface,
      surfaceContainerLow: c.surface,
      surfaceContainer: c.recessed,
      surfaceContainerHigh: c.recessed,
      surfaceContainerHighest: c.recessed,
      outline: c.inputBorder,
      outlineVariant: c.hairline,
    );
    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      fontFamily: UiType.sans,
    ).textTheme.apply(bodyColor: c.body, displayColor: c.strong);
    TextStyle? head(TextStyle? t) =>
        t == null ? null : UiType.intochatHeading(t);
    TextStyle? w(TextStyle? t) => t == null ? null : UiType.withWeightAxis(t);
    final textTheme = TextTheme(
      displayLarge: w(head(base.displayLarge)),
      displayMedium: w(head(base.displayMedium)),
      displaySmall: w(head(base.displaySmall)),
      headlineLarge: w(head(base.headlineLarge)),
      headlineMedium: w(head(base.headlineMedium)),
      headlineSmall: w(head(base.headlineSmall)),
      titleLarge: w(base.titleLarge),
      titleMedium: w(base.titleMedium),
      titleSmall: w(base.titleSmall),
      bodyLarge: w(base.bodyLarge?.copyWith(fontSize: 16, height: 1.5)),
      bodyMedium: w(base.bodyMedium?.copyWith(height: 1.5)),
      bodySmall: w(base.bodySmall),
      labelLarge: w(base.labelLarge),
      labelMedium: w(base.labelMedium),
      labelSmall: w(base.labelSmall),
    );
    final border = OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: BorderSide(color: c.inputBorder),
    );
    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      extensions: [const UiTokens(), c],
      colorScheme: scheme,
      fontFamily: UiType.sans,
      scaffoldBackgroundColor: c.page,
      canvasColor: c.page,
      dividerColor: c.hairline,
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: c.page,
        foregroundColor: c.strong,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        shape: Border(bottom: BorderSide(color: c.hairline)),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(48, 44),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontFamily: UiType.sans,
            fontSize: 14,
            fontWeight: FontWeight.w700,
            fontVariations: [FontVariation('wght', 700)],
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(48, 44),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          backgroundColor: c.surface,
          foregroundColor: c.strong,
          side: BorderSide(color: c.inputBorder),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontFamily: UiType.sans,
            fontSize: 14,
            fontWeight: FontWeight.w700,
            fontVariations: [FontVariation('wght', 700)],
          ),
        ),
      ),
      cardTheme: CardThemeData(
        color: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: c.hairline),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.surface,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 16,
        ),
        border: border,
        enabledBorder: border,
        focusedBorder: border.copyWith(
          borderSide: BorderSide(color: c.accent, width: 2),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: c.hairline),
        ),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(
          color: c.strong,
          borderRadius: BorderRadius.circular(8),
        ),
        textStyle: TextStyle(
          fontFamily: UiType.sans,
          color: c.page,
          fontSize: 12,
          fontVariations: const [FontVariation('wght', 400)],
        ),
      ),
    );
  }

  static ThemeData workspaceDark(String? stored) {
    final base = workspace(Brightness.dark);
    if (paletteName(stored) != 'lumen') return base;
    const background = Color(0xFF060A08);
    const surface = Color(0xFF0B120F);
    final scheme = base.colorScheme.copyWith(
      primary: const Color(0xFF7CE0A8),
      onPrimary: const Color(0xFF04170D),
      primaryContainer: const Color(0xFF12301F),
      onPrimaryContainer: const Color(0xFFA5F0C4),
      surface: surface,
      surfaceContainerLow: const Color(0xFF0F1813),
      surfaceContainer: const Color(0xFF131E18),
      surfaceContainerHigh: const Color(0xFF18261E),
      onSurface: const Color(0xFFE3EFE8),
      onSurfaceVariant: const Color(0xFF8CA397),
      outline: const Color(0xFF3F5A4A),
      outlineVariant: const Color(0xFF1D2D24),
    );
    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: background,
      dividerColor: scheme.outlineVariant,
      textTheme: base.textTheme.apply(
        bodyColor: scheme.onSurface,
        displayColor: scheme.onSurface,
      ),
      appBarTheme: base.appBarTheme.copyWith(
        backgroundColor: background,
        foregroundColor: scheme.onSurface,
        shape: Border(bottom: BorderSide(color: scheme.outlineVariant)),
      ),
      inputDecorationTheme: base.inputDecorationTheme.copyWith(
        fillColor: scheme.surfaceContainerLow,
      ),
      dialogTheme: base.dialogTheme.copyWith(
        backgroundColor: scheme.surfaceContainerLow,
      ),
    );
  }

  /// Neutral workspace surfaces with the shared Lumen accent. Legacy graph and
  /// chat tokens remain independent while workspace pages migrate.
  static ThemeData workspace(Brightness brightness) {
    final darkMode = brightness == Brightness.dark;
    final base = darkMode ? baseDark() : baseLight();
    final scheme =
        ColorScheme.fromSeed(
          seedColor: LumenPalette.accent,
          brightness: brightness,
        ).copyWith(
          primary: darkMode ? const Color(0xFF91D3B8) : LumenPalette.accent,
          onPrimary: darkMode ? const Color(0xFF103B2D) : Colors.white,
          primaryContainer: darkMode
              ? const Color(0xFF243B34)
              : LumenPalette.accentSoft,
          onPrimaryContainer: darkMode
              ? const Color(0xFFA5DCC5)
              : LumenPalette.accent,
          surface: darkMode ? const Color(0xFF191A1B) : LumenPalette.surface,
          surfaceContainerLow: darkMode
              ? const Color(0xFF202122)
              : LumenPalette.surface,
          surfaceContainer: darkMode
              ? const Color(0xFF252627)
              : LumenPalette.surfaceMuted,
          surfaceContainerHigh: darkMode
              ? const Color(0xFF2B2C2D)
              : LumenPalette.surfaceMuted,
          onSurface: darkMode ? const Color(0xFFECEBE7) : LumenPalette.ink,
          onSurfaceVariant: darkMode
              ? const Color(0xFFA5A6A3)
              : LumenPalette.muted,
          outline: darkMode ? const Color(0xFF616360) : LumenPalette.lineStrong,
          outlineVariant: darkMode
              ? const Color(0xFF333533)
              : LumenPalette.line,
        );
    final border = OutlineInputBorder(
      borderRadius: BorderRadius.circular(10),
      borderSide: BorderSide(color: scheme.outline),
    );
    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: darkMode
          ? const Color(0xFF171819)
          : LumenPalette.background,
      textTheme:
          ThemeData(
            brightness: brightness,
            fontFamily: UiType.bodyFamily,
            fontFamilyFallback: UiType.bodyFallback,
          ).textTheme.apply(
            bodyColor: scheme.onSurface,
            displayColor: scheme.onSurface,
          ),
      appBarTheme: AppBarTheme(
        backgroundColor: darkMode
            ? const Color(0xFF171819)
            : LumenPalette.background,
        foregroundColor: scheme.onSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        shape: Border(bottom: BorderSide(color: scheme.outlineVariant)),
      ),
      dividerColor: scheme.outlineVariant,
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(48, 44),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontFamily: UiType.bodyFamily,
            fontFamilyFallback: UiType.bodyFallback,
            fontSize: 14,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerLow,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 16,
        ),
        border: border,
        enabledBorder: border,
        focusedBorder: border.copyWith(
          borderSide: BorderSide(color: scheme.primary, width: 2),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(18),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
    );
  }

  /// v1 light base (formerly `light()`).
  static ThemeData baseLight() => ThemeData(
    useMaterial3: true,
    extensions: const [UiTokens()],
    brightness: Brightness.light,
    fontFamily: UiType.bodyFamily,
    fontFamilyFallback: UiType.bodyFallback,
    colorScheme: const ColorScheme.light(
      primary: LumenPalette.accent,
      onPrimary: LumenPalette.surface,
      secondary: LumenPalette.accentSoft,
      onSecondary: LumenPalette.ink,
      surface: LumenPalette.surface,
      onSurface: LumenPalette.ink,
      error: LumenPalette.error,
      outline: LumenPalette.lineStrong,
      outlineVariant: LumenPalette.line,
    ),
    scaffoldBackgroundColor: LumenPalette.background,
    dividerColor: LumenPalette.line,
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: LumenPalette.surface,
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: LumenPalette.line),
      ),
    ),
    tooltipTheme: TooltipThemeData(
      decoration: BoxDecoration(
        color: LumenPalette.ink,
        borderRadius: BorderRadius.circular(8),
      ),
      textStyle: const TextStyle(color: LumenPalette.surface, fontSize: 12),
    ),
  );

  /// v1 dark base (formerly `dark()`).
  static ThemeData baseDark() {
    final scheme = ColorScheme.fromSeed(
      seedColor: UiColors.midnight.working,
      brightness: Brightness.dark,
      surface: UiColors.midnight.page,
    );

    return ThemeData(
      useMaterial3: true,
      extensions: const [UiTokens()],
      brightness: Brightness.dark,
      scaffoldBackgroundColor: UiColors.midnight.page,
      colorScheme: scheme.copyWith(
        primary: UiColors.midnight.working,
        secondary: UiColors.midnight.accent,
        surface: UiColors.midnight.page,
      ),
      dividerColor: UiColors.midnight.hairline,
      navigationRailTheme: NavigationRailThemeData(
        backgroundColor: _midnightNavigation,
        indicatorColor: UiColors.midnight.working.withValues(alpha: 0.14),
        selectedIconTheme: IconThemeData(
          color: UiColors.midnight.working,
          size: 21,
        ),
        unselectedIconTheme: IconThemeData(
          color: UiColors.midnight.muted,
          size: 20,
        ),
        selectedLabelTextStyle: _metaStrong.copyWith(
          color: UiColors.midnight.working,
        ),
        unselectedLabelTextStyle: _meta,
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: _midnightNavigation,
        indicatorColor: UiColors.midnight.working.withValues(alpha: 0.14),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => states.contains(WidgetState.selected)
              ? _metaStrong.copyWith(color: UiColors.midnight.working)
              : _meta,
        ),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(color: UiColors.midnight.surface),
        textStyle: _meta,
      ),
    );
  }
}

/// Installs the approved product control foundation below MaterialApp.
///
/// Localization stays in this bridge so surfaces never need to import Forui.
final class UiThemeScope extends StatelessWidget {
  const UiThemeScope({super.key, required this.child, this.brightness});

  final Widget child;
  final Brightness? brightness;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final data = FThemeData(
      debugLabel: 'IntoChat.OS',
      touch: true,
      colors:
          (theme.brightness == Brightness.light
                  ? FColors.neutralLight
                  : FColors.neutralDark)
              .copyWith(
                background: theme.scaffoldBackgroundColor,
                foreground: colors.onSurface,
                primary: colors.primary,
                primaryForeground: colors.onPrimary,
                secondary: colors.primaryContainer,
                secondaryForeground: colors.onPrimaryContainer,
                muted: colors.surfaceContainer,
                mutedForeground: colors.onSurfaceVariant,
                card: colors.surface,
                border: colors.outlineVariant,
                destructive: colors.error,
                error: colors.error,
              ),
    );
    return Localizations.override(
      context: context,
      delegates: FLocalizations.localizationsDelegates,
      child: material.Theme(
        data: data.toApproximateMaterialTheme(),
        child: material.Material(
          type: material.MaterialType.transparency,
          child: FTheme(data: data, child: child),
        ),
      ),
    );
  }
}

abstract final class UiChatTheme {
  static ChatTheme workspace(ThemeData theme) {
    final c = theme.colorScheme;
    return ChatTheme(
      colors: ChatColors(
        primary: c.primary,
        onPrimary: c.onPrimary,
        surface: theme.scaffoldBackgroundColor,
        onSurface: c.onSurface,
        surfaceContainer: c.surfaceContainer,
        surfaceContainerLow: c.surfaceContainerLow,
        surfaceContainerHigh: c.surfaceContainerHigh,
      ),
      typography: ChatTypography.standard(fontFamily: UiType.bodyFamily)
          .copyWith(
            bodyMedium: (theme.textTheme.bodyMedium ?? _body).copyWith(
              fontSize: 15,
              height: 1.65,
              color: c.onSurface,
            ),
            bodySmall: (theme.textTheme.bodySmall ?? _bodyMuted).copyWith(
              fontSize: 12,
              color: c.onSurfaceVariant,
            ),
            labelSmall: (theme.textTheme.labelSmall ?? _meta).copyWith(
              fontSize: 11,
              color: c.onSurfaceVariant,
            ),
          ),
      shape: BorderRadius.circular(12),
    );
  }

  static ChatTheme light() => ChatTheme(
    colors: const ChatColors(
      primary: LumenPalette.accent,
      onPrimary: LumenPalette.surface,
      surface: LumenPalette.background,
      onSurface: LumenPalette.ink,
      surfaceContainer: LumenPalette.surface,
      surfaceContainerLow: LumenPalette.surfaceMuted,
      surfaceContainerHigh: LumenPalette.accentSoft,
    ),
    typography: ChatTypography.standard(fontFamily: UiType.bodyFamily).copyWith(
      bodyMedium: _body.copyWith(color: LumenPalette.ink),
      bodySmall: _bodyMuted.copyWith(color: LumenPalette.muted),
      labelSmall: _meta.copyWith(color: LumenPalette.muted),
    ),
    shape: BorderRadius.circular(16),
  );

  static ChatTheme dark() {
    final base = ChatTypography.standard(fontFamily: UiType.bodyFamily);
    return ChatTheme(
      colors: ChatColors(
        primary: UiColors.midnight.accent,
        onPrimary: UiColors.midnight.page,
        surface: UiColors.midnight.page,
        onSurface: UiColors.midnight.body,
        surfaceContainer: UiColors.midnight.surface,
        surfaceContainerLow: UiColors.midnight.recessed,
        surfaceContainerHigh: UiColors.midnight.hairline,
      ),
      typography: base.copyWith(
        bodyMedium: _body,
        bodySmall: _bodyMuted,
        labelSmall: _meta,
      ),
      shape: BorderRadius.circular(14),
    );
  }
}
