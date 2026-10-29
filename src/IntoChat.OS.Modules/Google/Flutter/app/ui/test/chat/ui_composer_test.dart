import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_ui/src/chat/chat_copy.dart';
import 'package:intochat_os_ui/src/chat/chat_run_link.dart';
import 'package:intochat_os_ui/src/chat/ui_composer.dart';

void main() {
  testWidgets('stop_replaces_send_while_running', (tester) async {
    final events = <Map<String, dynamic>>[];
    var stops = 0;
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.light('intochat'),
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: UiComposer(
              state: const {
                'context': 'a',
                'generation': 1,
                'draft': '',
                'followedChat': 'brain/apps/ino-data/transcript',
              },
              dispatch: (event) async => events.add(event),
            ),
          ),
        ),
      ),
    );
    expect(find.byTooltip(ChatCopy.send), findsOneWidget);
    expect(find.byTooltip(ChatCopy.stop), findsNothing);

    ChatRunLinks.publishStop('brain/apps/ino-data/transcript', () => stops++);
    addTearDown(
      () => ChatRunLinks.publishStop('brain/apps/ino-data/transcript', null),
    );
    await tester.pump();
    expect(find.byTooltip(ChatCopy.send), findsNothing);
    await tester.tap(find.byTooltip(ChatCopy.stop));
    expect(stops, 1);

    // Draft sync is unchanged while running: every edit is still dispatched.
    await tester.enterText(find.byType(TextField), 'next');
    await tester.pump();
    expect(events.map((event) => event['action']), ['edit']);

    ChatRunLinks.publishStop('brain/apps/ino-data/transcript', null);
    await tester.pump();
    expect(find.byTooltip(ChatCopy.send), findsOneWidget);
    await tester.tap(find.byTooltip(ChatCopy.send));
    await tester.pump();
    expect(events.map((event) => event['action']), ['edit', 'submit']);
  });

  testWidgets('a following composer resends for the chat retry', (
    tester,
  ) async {
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.light('intochat'),
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: UiComposer(
              state: const {
                'context': 'a',
                'generation': 1,
                'draft': '',
                'followedChat': 'chat-r',
              },
              dispatch: (event) async => events.add(event),
            ),
          ),
        ),
      ),
    );
    expect(ChatRunLinks.followed('chat-r'), isTrue);
    await ChatRunLinks.resend('chat-r')!('Again');
    await tester.pump();
    expect(events.map((event) => (event['action'], event['value'])), [
      ('edit', 'Again'),
      ('submit', 'Again'),
    ]);
    await tester.pumpWidget(const SizedBox());
    expect(ChatRunLinks.followed('chat-r'), isFalse);
  });

  testWidgets('a different draft in progress blocks Retry', (tester) async {
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        theme: UiTheme.light('intochat'),
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: UiComposer(
              state: const {
                'context': 'a',
                'generation': 1,
                'draft': '',
                'followedChat': 'chat-d',
              },
              dispatch: (event) async => events.add(event),
            ),
          ),
        ),
      ),
    );
    await tester.enterText(find.byType(TextField), 'my unsent thought');
    await tester.pump();
    events.clear();
    await ChatRunLinks.resend('chat-d')!('Again');
    await tester.pump();
    expect(events, isEmpty);
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'my unsent thought',
    );
  });
}
