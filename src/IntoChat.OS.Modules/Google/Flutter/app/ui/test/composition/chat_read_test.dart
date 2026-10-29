import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final fail in [false, true]) {
    testWidgets('explicit read waits for server acknowledgement (fail=$fail)', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(320, 300);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      var unread = ['answer-1', 'answer-2'];
      final events = <Map<String, dynamic>>[];
      Widget view(int revision) => MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'chat',
            name: 'transcript',
            revision: revision,
            load: (_, _) async => {
              'unreadMessageIds': unread,
              'projection': {
                'source': 'conversation',
                'generation': 7,
                'version': 19,
                'messages': [
                  for (final id in ['answer-1', 'answer-2'])
                    {'id': id, 'role': 1, 'text': id, 'status': 'completed'},
                ],
              },
            },
            onAction: (event) async {
              events.add(event);
              if (fail) throw StateError('Outdated transcript');
            },
          ),
        ),
      );
      await tester.pumpWidget(view(1));
      await tester.pumpAndSettle();
      await tester.pumpWidget(view(2));
      await tester.pumpAndSettle();
      expect(events, isEmpty);
      expect(find.text('2 unread'), findsOneWidget);
      await tester.tap(find.text('Mark read').hitTestable());
      await tester.pumpAndSettle();
      expect(events, [
        {
          'kind': 'chat',
          'name': 'transcript',
          'context': 'conversation',
          'revision': 7,
          'read': {
            'version': 19,
            'messageIds': ['answer-1', 'answer-2'],
          },
        },
      ]);
      expect(find.text('2 unread'), findsOneWidget);
      expect(find.text('Mark read'), findsOneWidget);
      unread = [];
      await tester.pumpWidget(view(3));
      await tester.pumpAndSettle();
      expect(find.text('2 unread'), findsNothing);
      expect(find.text('Mark read'), findsNothing);
      expect(events, hasLength(1));
      expect(tester.takeException(), isNull);
    });
  }
}
