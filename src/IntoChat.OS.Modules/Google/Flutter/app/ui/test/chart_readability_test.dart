import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('category labels stay separated at narrow and wide widths', (
    tester,
  ) async {
    const labels = [
      'BIRMINGHAM',
      'MANCHESTER',
      'NOTTINGHAM',
      'HUDDERSFIELD',
      'NEWCASTLE UPON TYNE',
    ];
    addTearDown(() => tester.binding.setSurfaceSize(null));
    for (final width in [360.0, 768.0, 1440.0]) {
      await tester.binding.setSurfaceSize(Size(width, 500));
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: UiChart(
              part: UiChartPart(
                title: 'Companies by town',
                chartKind: 'bar',
                points: [
                  for (var i = 0; i < labels.length; i++)
                    UiChartPoint(label: labels[i], value: i + 1),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      for (var i = 1; i < labels.length - 1; i++) {
        expect(
          tester.getRect(find.text(labels[i - 1])).right,
          lessThanOrEqualTo(tester.getRect(find.text(labels[i])).left),
        );
      }
      expect(find.text('NEWCASTLE U\u2026'), findsOneWidget);
      expect(tester.takeException(), isNull);
    }
  });
}
