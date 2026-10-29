@Tags(['golden'])
library;

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import '../composition/desktop_shape.dart';

void main() {
  if (!Platform.isWindows) {
    test(
      'desktop shape goldens',
      () {},
      skip: 'Goldens are Windows-rendered; Windows-local regression net only.',
    );
    return;
  }
  final themes = <String, ThemeData Function()>{
    'light': () => UiTheme.light('intochat'),
    'dark': () => UiTheme.dark('intochat'),
  };
  final sizes = <String, Size>{
    'wide': const Size(1440, 900),
    'narrow': const Size(390, 844),
  };
  for (final theme in themes.entries) {
    for (final size in sizes.entries) {
      final narrow = size.key == 'narrow';
      final scenes = <String, Map<String, dynamic> Function()>{
        'windows': () => desktopShape(narrow: narrow),
        'favourites': () => desktopShape(narrow: narrow, favourites: 3),
      };
      for (final scene in scenes.entries) {
        final name = '${scene.key}_${size.key}_${theme.key}';
        testWidgets('desktop shape $name', (tester) async {
          tester.view.physicalSize = size.value;
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          debugDisableShadows = false;
          try {
            await pumpShape(
              tester,
              scene.value(),
              theme: theme.value(),
              size: size.value,
            );
            await tester.pumpAndSettle();
            // An overflow is a FlutterError; none may be thrown at 390 px.
            expect(tester.takeException(), isNull);
            await expectLater(
              find.byType(NeuronView),
              matchesGoldenFile('desktop_shape_$name.png'),
            );
          } finally {
            debugDisableShadows = true;
          }
        });
      }
    }
  }
}
