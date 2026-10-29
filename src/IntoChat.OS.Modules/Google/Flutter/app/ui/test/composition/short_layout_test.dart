import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'short fixed layouts scroll controls and retain the draft on resize',
    (tester) async {
      tester.view.physicalSize = const Size(360, 500);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final events = <Map<String, dynamic>>[];
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'layout',
              name: 'main',
              onAction: (event) async {
                events.add(event);
              },
              load: (kind, name) async => switch (kind) {
                'layout' => {
                  'mode': 'column',
                  'extents': [40, 0, 152],
                  'children': [
                    {'kind': 'button', 'name': 'new'},
                    {'kind': 'chat', 'name': 'transcript'},
                    {'kind': 'composer', 'name': 'draft'},
                  ],
                },
                'button' => {'label': 'New conversation'},
                'chat' => {
                  'projection': {
                    'source': 'a',
                    'generation': 1,
                    'messages': [],
                    'actions': [
                      {'id': 'stop', 'label': 'Stop'},
                    ],
                  },
                },
                _ => {
                  'context': 'a',
                  'draft': 'Keep this draft',
                  'error': 'The response is running. Your draft is saved.',
                },
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final field = find.byType(TextField);
      final original = tester.state(field);
      tester.view.physicalSize = const Size(360, 100);
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.byTooltip('Send'));
      await tester.pumpAndSettle();
      expect(find.byTooltip('Send').hitTestable(), findsOneWidget);
      await tester.tap(find.byTooltip('Send'));
      await tester.pumpAndSettle();
      expect(events.last['value'], 'Keep this draft');
      tester.view.physicalSize = const Size(360, 500);
      await tester.pumpAndSettle();
      expect(tester.state(field), same(original));
      expect(
        tester.widget<TextField>(field).controller!.text,
        'Keep this draft',
      );
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
