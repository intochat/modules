import 'dart:convert';
import 'dart:typed_data';

import 'package:intochat_os_flutter_shell/workspace/home/app_icon.dart';
import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Widget host(Widget child) => MaterialApp(home: Scaffold(body: child));

  test('toolbar icon names resolve to the shell icon set, paths do not', () {
    expect(shellIcon('play'), Icons.play_arrow_rounded);
    expect(shellIcon('mic'), Icons.mic_rounded);
    expect(shellIcon('backspace'), Icons.backspace_outlined);
    expect(shellIcon('icon.svg'), isNull);
  });

  testWidgets('an app without an icon shows its initial', (tester) async {
    await tester.pumpWidget(host(const AppIcon(title: 'customer researcher')));
    expect(find.byKey(const ValueKey('app-icon-fallback')), findsOneWidget);
    expect(find.text('C'), findsOneWidget);
  });

  testWidgets('an svg icon renders as a picture', (tester) async {
    final loads = <String>[];
    await tester.pumpWidget(
      host(
        AppIcon(
          title: 'Researcher',
          path: '/brains/b1/apps/researcher/icon',
          load: (path) async {
            loads.add(path);
            return Uint8List.fromList(
              utf8.encode(
                '<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect width="4" height="4"/></svg>',
              ),
            );
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(loads, ['/brains/b1/apps/researcher/icon']);
    expect(find.byType(SvgPicture), findsOneWidget);
    expect(find.byKey(const ValueKey('app-icon-fallback')), findsNothing);
  });

  testWidgets('a failed load falls back to the initial', (tester) async {
    await tester.pumpWidget(
      host(
        AppIcon(
          title: 'Word count',
          path: '/x',
          load: (_) async => throw StateError('offline'),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('app-icon-fallback')), findsOneWidget);
  });
}
