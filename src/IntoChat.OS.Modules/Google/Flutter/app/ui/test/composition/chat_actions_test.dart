import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('finishing an action preserves a paused reading position', (
    tester,
  ) async {
    var running = true;
    Widget view(int revision) => MaterialApp(
      home: Scaffold(
        body: NeuronView(
          kind: 'chat',
          name: 'transcript',
          revision: revision,
          load: (_, _) async => {
            'projection': {
              'source': 'conversation',
              'generation': 1,
              'messages': [
                {'id': 'reply', 'role': 1, 'text': 'Long response.\n\n' * 100},
              ],
              'actions': [
                if (running) {'id': 'stop/run-1', 'label': 'Stop'},
              ],
            },
          },
        ),
      ),
    );
    await tester.pumpWidget(view(1));
    await tester.pumpAndSettle();
    final scroll = tester
        .widget<SingleChildScrollView>(find.byType(SingleChildScrollView).first)
        .controller!;
    scroll.jumpTo(100);
    await tester.pumpAndSettle();
    running = false;
    await tester.pumpWidget(view(2));
    await tester.pumpAndSettle();
    final current = tester
        .widget<SingleChildScrollView>(find.byType(SingleChildScrollView).first)
        .controller!;
    expect(identical(current, scroll), isTrue);
    expect(current.offset, 100);
  });

  testWidgets('chat actions carry displayed identity without the payload', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 300);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'chat',
            name: 'transcript',
            load: (_, _) async => {
              'projection': {
                'source': 'conversation',
                'generation': 7,
                'version': 19,
                'messages': [
                  {
                    'id': 'reply',
                    'role': 1,
                    'text': 'Working',
                    'status': 'running',
                  },
                ],
                'actions': [
                  {
                    'id': 'stop/run-1',
                    'label': 'Stop',
                    'payload': 'server-only',
                  },
                  {'id': 'disabled', 'label': 'Unavailable', 'enabled': false},
                ],
              },
            },
            onAction: (event) async => events.add(event),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Stop').hitTestable(), findsOneWidget);
    await tester.tap(find.text('Unavailable'));
    expect(events, isEmpty);
    await tester.tap(find.text('Stop'));
    await tester.pumpAndSettle();
    expect(events, [
      {
        'kind': 'chat',
        'name': 'transcript',
        'action': 'stop/run-1',
        'context': 'conversation',
        'revision': 7,
      },
    ]);
    expect(tester.takeException(), isNull);
  });
}
