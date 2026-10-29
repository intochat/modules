import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('palettes list intochat first', () {
    expect(UiTheme.palettes, ['intochat', 'midnight', 'paper', 'lumen']);
  });

  test('stored theme values resolve to a palette and never throw', () {
    expect(UiTheme.paletteName(null), 'intochat');
    expect(UiTheme.paletteName(''), 'intochat');
    expect(UiTheme.paletteName('system'), 'intochat');
    expect(UiTheme.paletteName('nonsense'), 'intochat');
    expect(UiTheme.paletteName('dark'), 'midnight');
    expect(UiTheme.paletteName('light'), 'paper');
    for (final name in UiTheme.palettes) {
      expect(UiTheme.paletteName(name), name);
    }
    for (final stored in [
      null,
      '',
      'system',
      'dark',
      'light',
      'x',
      ...UiTheme.palettes,
    ]) {
      expect(UiTheme.light(stored), isA<ThemeData>());
      expect(UiTheme.dark(stored), isA<ThemeData>());
    }
  });

  test('theme modes per palette', () {
    expect(UiTheme.modeFor('intochat'), ThemeMode.system);
    expect(UiTheme.modeFor(null), ThemeMode.system);
    expect(UiTheme.modeFor('paper'), ThemeMode.light);
    expect(UiTheme.modeFor('light'), ThemeMode.light);
    expect(UiTheme.modeFor('midnight'), ThemeMode.dark);
    expect(UiTheme.modeFor('lumen'), ThemeMode.dark);
  });

  test('intochat themes carry UiColors, accent primary and Schibsted type', () {
    final light = UiTheme.light('intochat');
    final dark = UiTheme.dark('intochat');
    expect(light.extension<UiColors>(), UiColors.intochatLight);
    expect(dark.extension<UiColors>(), UiColors.intochatDark);
    expect(light.extension<UiTokens>(), isNotNull);
    expect(dark.scaffoldBackgroundColor, const Color(0xFF17140F));
    expect(light.scaffoldBackgroundColor, const Color(0xFFFBFAF7));
    expect(light.colorScheme.primary, UiColors.intochatLight.accent);
    expect(dark.colorScheme.primary, UiColors.intochatDark.accent);
    expect(light.brightness, Brightness.light);
    expect(dark.brightness, Brightness.dark);
    expect(
      light.textTheme.bodyMedium!.fontFamily,
      contains('Schibsted Grotesk'),
    );
  });

  test('v1 palettes carry their UiColors', () {
    expect(UiTheme.dark('midnight').extension<UiColors>(), UiColors.midnight);
    expect(UiTheme.light('paper').extension<UiColors>(), UiColors.paper);
    expect(UiTheme.dark('lumen').extension<UiColors>(), UiColors.lumen);
  });

  test('v1 palettes look exactly as before', () {
    // Literals captured from the pre-change code paths
    // (workspaceDark('midnight'), workspace(light), workspaceDark('lumen')).
    void check(ThemeData t, int scaffold, int primary, int surface, int on) {
      expect(t.scaffoldBackgroundColor, Color(scaffold));
      expect(t.colorScheme.primary, Color(primary));
      expect(t.colorScheme.surface, Color(surface));
      expect(t.colorScheme.onSurface, Color(on));
    }

    check(
      UiTheme.dark('midnight'),
      0xFF171819,
      0xFF91D3B8,
      0xFF191A1B,
      0xFFECEBE7,
    );
    check(
      UiTheme.light('paper'),
      0xFFF7F7F2,
      0xFF397B63,
      0xFFFFFFFF,
      0xFF263934,
    );
    check(
      UiTheme.dark('lumen'),
      0xFF060A08,
      0xFF7CE0A8,
      0xFF0B120F,
      0xFFE3EFE8,
    );
  });

  testWidgets('UiType.of uses the ambient UiColors', (tester) async {
    late UiTypeSet type;
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.light('intochat'),
        home: Builder(
          builder: (context) {
            type = UiType.of(context);
            return const SizedBox();
          },
        ),
      ),
    );
    expect(type.body.color, UiColors.intochatLight.body);
    expect(type.bodyMuted.color, UiColors.intochatLight.muted);
    expect(type.title.color, UiColors.intochatLight.strong);
    expect(type.body.fontFamily, contains('Schibsted Grotesk'));
    expect(type.meta.fontFamily, contains('JetBrains Mono'));
    expect(type.heading.fontWeight, FontWeight.w800);
    expect(type.heading.fontVariations, [const FontVariation('wght', 800)]);
    expect(type.meta.fontVariations, [const FontVariation('wght', 500)]);
  });

  test('intochat textTheme pins variable-font weights', () {
    final t = UiTheme.light('intochat').textTheme;
    expect(t.headlineMedium!.fontVariations, [
      const FontVariation('wght', 800),
    ]);
    expect(t.bodyMedium!.fontVariations, [
      FontVariation(
        'wght',
        (t.bodyMedium!.fontWeight ?? FontWeight.w400).value.toDouble(),
      ),
    ]);
  });

  testWidgets('lerped intochat UiColors keep IntoChat fonts', (tester) async {
    late UiTypeSet type;
    final mid = UiColors.intochatLight.lerp(UiColors.intochatDark, 0.5);
    final base = UiTheme.light('intochat');
    await tester.pumpWidget(
      MaterialApp(
        theme: base.copyWith(
          extensions: [
            ...base.extensions.values.where((e) => e is! UiColors),
            mid,
          ],
        ),
        home: Builder(
          builder: (context) {
            type = UiType.of(context);
            return const SizedBox();
          },
        ),
      ),
    );
    expect(type.body.fontFamily, contains('Schibsted Grotesk'));
    expect(type.body.color, mid.body);
  });

  test('lumen is green on near-black and differs from midnight', () {
    final lumen = UiTheme.workspaceDark('lumen');
    final midnight = UiTheme.workspaceDark('midnight');
    expect(lumen.brightness, Brightness.dark);
    expect(lumen.scaffoldBackgroundColor.computeLuminance(), lessThan(0.02));
    final primary = lumen.colorScheme.primary;
    expect(primary.g, greaterThan(primary.r));
    expect(primary.g, greaterThan(primary.b));
    expect(
      midnight.scaffoldBackgroundColor,
      UiTheme.workspace(Brightness.dark).scaffoldBackgroundColor,
    );
    expect(
      lumen.scaffoldBackgroundColor,
      isNot(midnight.scaffoldBackgroundColor),
    );
  });
}
