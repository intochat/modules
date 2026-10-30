import 'package:flutter/material.dart';
import 'package:flutter_chat_core/flutter_chat_core.dart';
import 'package:forui/forui.dart';
import 'package:material_ui/material_ui.dart' as material;

import 'ui_colors.dart';
import 'ui_tokens.dart';

/// Text styles for the ambient palette. Colours come from [UiColors.of];
/// families are IntoChat's bundled `Inter` and `InterDisplay` fonts with
/// strict `FontFeature.tabularFigures()` (`tnum`) on metrics, counters,
/// timestamps, table numeric columns, and `neuron://` URIs.
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

  /// A 13 px row title.
  final TextStyle rowTitle;

  /// A 12 px compact label (toolbar pickers).
  final TextStyle caption;
}

abstract final class UiType {
  static const fontPackage = 'intochat_os_ui';
  static const sansFamily = 'Inter';
  static const displayFamily = 'InterDisplay';
  static const intochatMonoFamily = 'Inter';

  /// Package-qualified family names usable from any package.
  static const sans = 'packages/$fontPackage/$sansFamily';
  static const display = 'packages/$fontPackage/$displayFamily';
  static const mono = 'packages/$fontPackage/$intochatMonoFamily';

  static const tabular = [FontFeature.tabularFigures()];

  /// IntoChat display/heading treatment: `InterDisplay`, w700–w800, -0.4
  /// letter-spacing, 1.15 line height (`>= 17px`).
  static TextStyle intochatHeading(TextStyle style) => style.copyWith(
    fontFamily: display,
    fontWeight: style.fontWeight ?? FontWeight.w700,
    letterSpacing: -0.4,
    height: 1.15,
  );

  /// Pins the variable font's `wght` axis to [TextStyle.fontWeight] (default
  /// 400) so bundled fonts render the requested weight.
  static TextStyle withWeightAxis(TextStyle style) => style.copyWith(
    fontVariations: [
      FontVariation(
        'wght',
        (style.fontWeight ?? FontWeight.w400).value.toDouble(),
      ),
    ],
  );

  /// Unified text builder matching `IcType.text`.
  static TextStyle textStyle(
    double size, {
    FontWeight weight = FontWeight.w400,
    Color? color,
    bool tnum = false,
    double? height,
  }) => TextStyle(
    fontFamily: sans,
    fontSize: size,
    fontWeight: weight,
    color: color,
    height: height,
    letterSpacing: size >= 20 ? -0.2 : 0,
    fontFeatures: tnum ? tabular : null,
  );

  /// Unified display heading builder matching `IcType.heading`.
  static TextStyle displayStyle(
    double size, {
    FontWeight weight = FontWeight.w700,
    Color? color,
  }) => TextStyle(
    fontFamily: display,
    fontSize: size,
    fontWeight: weight,
    color: color,
    letterSpacing: -0.4,
    height: 1.15,
  );

  /// Unified tabular/URI style matching `IcType.mono`.
  static TextStyle tabularStyle(
    double size, {
    FontWeight weight = FontWeight.w500,
    Color? color,
  }) => TextStyle(
    fontFamily: mono,
    fontSize: size,
    fontWeight: weight,
    color: color,
    fontFeatures: tabular,
  );

  static UiTypeSet of(BuildContext context) {
    final c = UiColors.of(context);
    TextStyle s(
      TextStyle baseStyle,
      Color color, {
      bool isMono = false,
      bool head = false,
      bool tnum = false,
    }) {
      final isDisplay = head || (baseStyle.fontSize ?? 14) >= 17 && !tnum;
      final style = baseStyle.copyWith(
        fontFamily: isDisplay ? display : (isMono ? mono : sans),
        color: color,
        fontFeatures: (isMono || tnum) ? tabular : baseStyle.fontFeatures,
      );
      return withWeightAxis(head ? intochatHeading(style) : style);
    }

    return UiTypeSet(
      title: s(_title, c.strong),
      heading: s(_heading, c.strong, head: true),
      cardTitle: s(_cardTitle, c.strong),
      metric: s(_metric, c.strong, tnum: true),
      body: s(_body, c.body),
      bodyMuted: s(_bodyMuted, c.muted),
      meta: s(_meta, c.muted, isMono: true, tnum: true),
      metaStrong: s(_metaStrong, c.strong, isMono: true, tnum: true),
      empty: s(_empty, c.strong, head: true),
      control: s(_control, c.strong),
      fieldLabel: s(_fieldLabel, c.strong),
      menuItem: s(_menuItem, c.body),
      menuLabel: s(_menuLabel, c.muted, isMono: true, tnum: true),
      toast: s(_toast, c.body),
      rowTitle: s(_rowTitle, c.strong),
      caption: s(_caption, c.body),
    );
  }

  static const monoFamily = sans;
  static const monoFallback = ['Cascadia Mono', 'Consolas', 'Menlo', 'monospace'];

  static const bodyFamily = sans;
  static const bodyFallback = ['Segoe UI Variable Text', 'Segoe UI', 'Roboto'];
}

final _title = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 15,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.2,
  color: UiColors.midnight.strong,
);

final _heading = TextStyle(
  fontFamily: UiType.display,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 28,
  fontWeight: FontWeight.w700,
  letterSpacing: -0.4,
  height: 1.15,
  color: UiColors.midnight.strong,
);

final _cardTitle = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 15,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.15,
  color: UiColors.midnight.strong,
);

final _metric = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 17,
  fontWeight: FontWeight.w600,
  letterSpacing: -0.2,
  fontFeatures: UiType.tabular,
  color: UiColors.midnight.strong,
);

final _body = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  height: 1.5,
  color: UiColors.midnight.body,
);

final _bodyMuted = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  height: 1.45,
  color: UiColors.midnight.muted,
);

final _meta = TextStyle(
  fontFamily: UiType.mono,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w500,
  letterSpacing: 0.45,
  fontFeatures: UiType.tabular,
  color: UiColors.midnight.muted,
);

final _metaStrong = TextStyle(
  fontFamily: UiType.mono,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w600,
  letterSpacing: 0.45,
  fontFeatures: UiType.tabular,
  color: UiColors.midnight.strong,
);

final _empty = TextStyle(
  fontFamily: UiType.display,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 18,
  fontWeight: FontWeight.w700,
  letterSpacing: -0.4,
  height: 1.15,
  color: UiColors.midnight.strong,
);

final _control = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 14,
  fontWeight: FontWeight.w700,
  color: UiColors.midnight.strong,
);

final _fieldLabel = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w600,
  color: UiColors.midnight.strong,
);

final _menuItem = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

final _menuLabel = TextStyle(
  fontFamily: UiType.mono,
  fontFamilyFallback: UiType.monoFallback,
  fontSize: 11,
  fontWeight: FontWeight.w500,
  letterSpacing: 0.88,
  fontFeatures: UiType.tabular,
  color: UiColors.midnight.strong,
);

final _rowTitle = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

final _caption = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 12,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.body,
);

final _toast = TextStyle(
  fontFamily: UiType.sans,
  fontFamilyFallback: UiType.bodyFallback,
  fontSize: 13,
  fontWeight: FontWeight.w500,
  color: UiColors.midnight.strong,
);

abstract final class UiTheme {
  static const palettes = [
    'intochat',
    'midnight',
    'paper',
    'lumen',
    'fluent-acrylic',
    'spatial-glass',
    'cyber-synaptic',
    'warm-paper',
    'fluent-warm-mica',
    'linear-carbon',
    'nordic-tactical',
  ];

  static String paletteName(String? stored) => switch (stored) {
    'dark' || 'midnight' => 'midnight',
    'light' || 'paper' => 'paper',
    'intochat' => 'intochat',
    'warm-paper' => 'warm-paper',
    'lumen' => 'lumen',
    'fluent-acrylic' => 'fluent-acrylic',
    'spatial-glass' => 'spatial-glass',
    'cyber-synaptic' => 'cyber-synaptic',
    'fluent-warm-mica' => 'fluent-warm-mica',
    'linear-carbon' => 'linear-carbon',
    'nordic-tactical' => 'nordic-tactical',
    _ => 'intochat',
  };

  static ThemeMode modeFor(String? stored) => switch (paletteName(stored)) {
    'intochat' || 'warm-paper' => ThemeMode.system,
    'paper' => ThemeMode.light,
    _ => ThemeMode.dark,
  };

  /// Light theme for a stored palette, built directly from its [UiColors]
  /// palette so every preset renders its exact identity colors.
  static ThemeData light(String? stored) => switch (paletteName(stored)) {
    'fluent-acrylic' => fromColors(UiColors.fluentAcrylicLight),
    'spatial-glass' => fromColors(UiColors.spatialGlassLight),
    'cyber-synaptic' => fromColors(UiColors.cyberSynapticLight),
    'warm-paper' => intochat(Brightness.light),
    'fluent-warm-mica' => fromColors(UiColors.fluentWarmMicaLight),
    'linear-carbon' => fromColors(UiColors.linearCarbonLight),
    'nordic-tactical' => fromColors(UiColors.nordicTacticalLight),
    'lumen' => fromColors(UiColors.lumen),
    'midnight' => fromColors(UiColors.midnight),
    'paper' => fromColors(UiColors.paper),
    _ => intochat(Brightness.light),
  };

  /// Dark theme for a stored palette, built directly from its [UiColors]
  /// palette (eliminating the legacy `workspaceDark` Lumen green fallback).
  static ThemeData dark(String? stored) => switch (paletteName(stored)) {
    'fluent-acrylic' => fromColors(UiColors.fluentAcrylicDark),
    'spatial-glass' => fromColors(UiColors.spatialGlassDark),
    'cyber-synaptic' => fromColors(UiColors.cyberSynapticDark),
    'warm-paper' => intochat(Brightness.dark),
    'fluent-warm-mica' => fromColors(UiColors.fluentWarmMicaDark),
    'linear-carbon' => fromColors(UiColors.linearCarbonDark),
    'nordic-tactical' => fromColors(UiColors.nordicTacticalDark),
    'lumen' => fromColors(UiColors.lumen),
    'midnight' => fromColors(UiColors.midnight),
    'paper' => fromColors(UiColors.paper),
    _ => intochat(Brightness.dark),
  };

  /// Builds a complete Material 3 [ThemeData] directly from a [UiColors] palette.
  static ThemeData fromColors(UiColors c) {
    final brightness = c.brightness;
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
      titleLarge: w(head(base.titleLarge)),
      titleMedium: w(base.titleMedium),
      titleSmall: w(base.titleSmall),
      bodyLarge: w(base.bodyLarge?.copyWith(fontSize: 15, height: 1.5)),
      bodyMedium: w(base.bodyMedium?.copyWith(fontSize: 14, height: 1.5)),
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
          minimumSize: const Size(36, 36),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontFamily: UiType.sans,
            fontFamilyFallback: UiType.bodyFallback,
            fontSize: 13,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(36, 36),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          backgroundColor: c.surface,
          foregroundColor: c.strong,
          side: BorderSide(color: c.inputBorder),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
          textStyle: const TextStyle(
            fontFamily: UiType.sans,
            fontFamilyFallback: UiType.bodyFallback,
            fontSize: 13,
            fontWeight: FontWeight.w600,
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
          horizontal: 14,
          vertical: 8,
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
        ),
      ),
    );
  }

  /// IntoChat (Warm Ink & Paper) theme.
  static ThemeData intochat(Brightness brightness) => fromColors(
    brightness == Brightness.dark
        ? UiColors.intochatDark
        : UiColors.intochatLight,
  );

  static ThemeData workspaceDark(String? stored) => dark(stored);

  static ThemeData workspace(Brightness brightness) => fromColors(
    brightness == Brightness.dark ? UiColors.midnight : UiColors.paper,
  );

  static ThemeData baseLight() => fromColors(UiColors.intochatLight);

  static ThemeData baseDark() => fromColors(UiColors.midnight);
}

/// Installs the approved product control foundation below MaterialApp.
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
    colors: ChatColors(
      primary: UiColors.paper.accent,
      onPrimary: UiColors.paper.surface,
      surface: UiColors.paper.page,
      onSurface: UiColors.paper.ink,
      surfaceContainer: UiColors.paper.surface,
      surfaceContainerLow: UiColors.paper.recessed,
      surfaceContainerHigh: UiColors.paper.accentTint,
    ),
    typography: ChatTypography.standard(fontFamily: UiType.bodyFamily).copyWith(
      bodyMedium: _body.copyWith(color: UiColors.paper.ink),
      bodySmall: _bodyMuted.copyWith(color: UiColors.paper.muted),
      labelSmall: _meta.copyWith(color: UiColors.paper.muted),
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
