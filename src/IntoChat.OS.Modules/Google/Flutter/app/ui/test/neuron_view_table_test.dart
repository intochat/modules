import 'package:intochat_os_ui/src/composition/neuron_view.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('a table node renders its columns and rows', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: NeuronView(
          kind: 'table',
          name: 'w/apps/dv/grid',
          refreshEvery: const Duration(days: 1),
          load: (_, _) async => {
            'title': 'People',
            'columns': [
              {'id': 'c', 'title': 'Country'},
            ],
            'rows': [
              ['us'],
              ['uk'],
            ],
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Country'), findsOneWidget);
    expect(find.text('uk'), findsOneWidget);
  });

  testWidgets('a chart node renders its title', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'chart',
            name: 'w/apps/dv/pie',
            refreshEvery: const Duration(days: 1),
            load: (_, _) async => {
              'title': 'Share',
              'kind': 'pie',
              'points': [
                {'eventId': '1', 'label': 'us', 'value': 2},
              ],
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('ui_chart_Share')), findsOneWidget);
  });

  Future<void> pumpTable(
    WidgetTester tester,
    Map<String, dynamic> state,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'table',
            name: 'w/apps/dv/grid',
            refreshEvery: const Duration(days: 1),
            load: (_, _) async => state,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('an empty table node renders nothing and does not throw', (
    tester,
  ) async {
    await pumpTable(tester, {'title': 'People', 'columns': [], 'rows': []});
    expect(tester.takeException(), isNull);
    expect(find.byType(DataTable), findsNothing);
  });

  testWidgets('a ragged row is padded or truncated to the column count', (
    tester,
  ) async {
    await pumpTable(tester, {
      'columns': [
        {'id': 'a', 'title': 'A'},
        {'id': 'b', 'title': 'B'},
      ],
      'rows': [
        ['only'],
        ['x', 'y', 'extra'],
      ],
    });
    expect(tester.takeException(), isNull);
    expect(find.text('only'), findsOneWidget);
    expect(find.text('y'), findsOneWidget);
    expect(find.text('extra'), findsNothing);
  });
}
