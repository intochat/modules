@Tags(['golden'])
library;

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'kit_sheet.dart';

void main() {
  if (!Platform.isWindows) {
    test(
      'kit goldens',
      () {},
      skip: 'Goldens are Windows-rendered; Windows-local regression net only.',
    );
    return;
  }
  final cases = <String, ThemeData Function()>{
    'intochat_light': () => UiTheme.light('intochat'),
    'intochat_dark': () => UiTheme.dark('intochat'),
    'midnight': () => UiTheme.dark('midnight'),
    'paper': () => UiTheme.light('paper'),
    'lumen': () => UiTheme.dark('lumen'),
  };
  for (final entry in cases.entries) {
    testWidgets('kit sheet ${entry.key}', (tester) async {
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      // flutter_test draws shadows unblurred by default; goldens need the
      // real Figma elevation.
      // The framework checks the flag before tear-downs, so reset it here.
      debugDisableShadows = false;
      try {
        await tester.pumpWidget(
          MaterialApp(
            debugShowCheckedModeBanner: false,
            theme: entry.value(),
            home: const Material(child: KitSheet()),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.bySemanticsLabel(KitSheet.menuTrigger));
        await tester.pumpAndSettle();
        await expectLater(
          find.byType(KitSheet),
          matchesGoldenFile('kit_${entry.key}.png'),
        );
        // Let the toast's 4 s timer finish before the test ends.
        await tester.pump(const Duration(seconds: 5));
      } finally {
        debugDisableShadows = true;
      }
    });
  }
}
