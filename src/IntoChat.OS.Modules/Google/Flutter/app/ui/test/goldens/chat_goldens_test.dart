@Tags(['golden'])
library;

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import '../composition/ino_shape.dart';

void main() {
  if (!Platform.isWindows) {
    test(
      'chat goldens',
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
  // Approvals were dropped from R4 (owner decision 1), so there is no
  // approval card scene.
  final scenes = <String, Future<void> Function(WidgetTester, ThemeData, Size)>{
    'empty': (tester, theme, size) => pumpIno(
      tester,
      inoView(chats: const []),
      theme: theme,
      size: size,
    ),
    'conversation': (tester, theme, size) => pumpIno(
      tester,
      inoView(),
      messages: inoConversation(),
      theme: theme,
      size: size,
    ),
    'running': (tester, theme, size) => pumpIno(
      tester,
      inoView(),
      messages: [
        inoUser('r1', 'Summarise the Havbris notes.'),
        inoAnswer('r1', 'Reading the notes', status: 'running'),
      ],
      running: true,
      theme: theme,
      size: size,
    ),
    'menu': (tester, theme, size) async {
      await pumpIno(
        tester,
        inoView(),
        messages: inoConversation().take(2).toList(),
        theme: theme,
        size: size,
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Chat actions').first);
    },
    'first_run': (tester, theme, size) => pumpIno(
      tester,
      inoView(menu: false, firstRun: inoFirstRun()),
      theme: theme,
      size: size,
    ),
  };
  for (final theme in themes.entries) {
    for (final size in sizes.entries) {
      for (final scene in scenes.entries) {
        final name = '${scene.key}_${size.key}_${theme.key}';
        testWidgets('chat $name', (tester) async {
          // The composer's voice button creates a recorder; tests have no plugin.
          const record = MethodChannel('com.llfbandit.record/messages');
          tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
            record,
            (call) async => null,
          );
          addTearDown(
            () => tester.binding.defaultBinaryMessenger
                .setMockMethodCallHandler(record, null),
          );
          tester.view.physicalSize = size.value;
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          debugDisableShadows = false;
          try {
            await scene.value(tester, theme.value(), size.value);
            await tester.pumpAndSettle();
            await expectLater(
              find.byType(MaterialApp),
              matchesGoldenFile('chat_$name.png'),
            );
          } finally {
            debugDisableShadows = true;
          }
        });
      }
    }
  }
}
