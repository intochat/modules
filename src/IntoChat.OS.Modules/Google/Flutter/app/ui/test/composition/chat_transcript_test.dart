import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_ui/src/chat/ui_chat_transcript.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'activity summary keeps the final answer visible in a short chat',
    (tester) async {
      tester.view.physicalSize = const Size(360, 320);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: UiChatTranscript(
              messages: [
                {
                  'id': 'answer',
                  'role': 1,
                  'text': 'Six companies are ready.',
                  'status': 'completed',
                  'activities': List.generate(
                    8,
                    (i) => {
                      'id': 'call-$i',
                      'title': 'Lookup $i',
                      'input': 'company $i',
                      'result': 'Verified source $i',
                    },
                  ),
                },
              ],
              markdown: (text) => Text(text),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        find.text('Six companies are ready.').hitTestable(),
        findsOneWidget,
      );
      expect(find.text('Lookup 7'), findsNothing);
      await tester.tap(find.text('8 activities'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Lookup 7'));
      await tester.tap(find.text('Lookup 7'));
      await tester.pumpAndSettle();
      expect(find.text('Verified source 7'), findsOneWidget);
      await tester.ensureVisible(find.text('8 activities'));
      await tester.tap(find.text('8 activities'));
      await tester.pumpAndSettle();
      expect(find.text('Lookup 7'), findsNothing);
      expect(find.text('Verified source 7'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'streaming preserves the reading position until latest is requested',
    (tester) async {
      var text = 'A paragraph in the response.\n\n' * 60;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'chat',
              name: 'reading',
              load: (_, _) async => {
                'projection': {
                  'source': 'agent',
                  'generation': 1,
                  'version': text.length,
                  'messages': [
                    {
                      'id': 'answer',
                      'role': 1,
                      'text': text,
                      'status': 'running',
                    },
                  ],
                },
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final scroll = tester
          .widget<SingleChildScrollView>(
            find.byType(SingleChildScrollView).first,
          )
          .controller!;
      expect(scroll.position.extentAfter, 0);
      await tester.drag(
        find.byType(SingleChildScrollView).first,
        const Offset(0, 1400),
      );
      await tester.pumpAndSettle();
      final reading = scroll.offset;
      expect(find.byTooltip('Jump to latest'), findsOneWidget);
      text += 'More streamed text.\n\n' * 20;
      await tester.pump(const Duration(seconds: 2));
      await tester.pumpAndSettle();
      expect(scroll.offset, reading);
      await tester.tap(find.byTooltip('Jump to latest'));
      await tester.pumpAndSettle();
      expect(scroll.position.extentAfter, 0);
      expect(find.byTooltip('Jump to latest'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    },
  );

  for (final width in [360.0, 768.0, 1280.0]) {
    testWidgets('chat contains long content at width $width', (tester) async {
      tester.view.physicalSize = Size(width, 640);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'chat',
              name: 'conversation',
              load: (_, _) async => {
                'projection': {
                  'source': 'agent',
                  'generation': 1,
                  'version': 1,
                  'messages': [
                    {'id': 'u', 'role': 0, 'text': 'Show the results'},
                    {
                      'id': 'a',
                      'role': 1,
                      'text':
                          '# Results\n\n${'A paragraph of readable text.\n\n' * 30}\n```json\n${'x' * 300}\n```\n\n| Company | Detail |\n|---|---|\n| Example | ${'wide' * 80} |',
                      'status': 'completed',
                      'activities': [
                        {
                          'id': 'call',
                          'title': 'Query database',
                          'input': 'select 1',
                          'result': 'private result details',
                        },
                      ],
                    },
                  ],
                },
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      // Answers are plain text under the Figma restyle: no "Assistant" label.
      expect(find.text('Show the results'), findsOneWidget);
      expect(find.text('private result details'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('1 activity'));
      await tester.tap(find.text('1 activity'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Query database'));
      await tester.tap(find.text('Query database'));
      await tester.pumpAndSettle();
      expect(find.text('private result details'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    });
  }
}
