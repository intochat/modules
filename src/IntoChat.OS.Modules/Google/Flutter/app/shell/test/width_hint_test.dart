import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter_shell/workspace/width_hint.dart';

void main() {
  testWidgets(
    'reports narrow once at start and again only when 900 px is crossed',
    (tester) async {
      final reports = <bool>[];
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      Future<void> width(double value) async {
        tester.view.physicalSize = Size(value, 700);
        await tester.pumpAndSettle();
      }

      await width(1200);
      await tester.pumpWidget(
        MaterialApp(
          home: WidthHint(onNarrow: reports.add, child: const Text('desktop')),
        ),
      );
      await tester.pumpAndSettle();
      expect(reports, [false]);
      expect(find.text('desktop'), findsOneWidget);

      await width(950);
      expect(reports, [false]);
      await width(899);
      expect(reports, [false, true]);
      await width(600);
      expect(reports, [false, true]);
      await width(900);
      expect(reports, [false, true, false]);
    },
  );
}
