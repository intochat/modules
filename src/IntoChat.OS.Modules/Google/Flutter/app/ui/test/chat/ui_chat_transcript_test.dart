import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_ui/src/chat/chat_app_card.dart';
import 'package:intochat_os_ui/src/chat/chat_copy.dart';
import 'package:intochat_os_ui/src/chat/chat_message_toolbar.dart';
import 'package:intochat_os_ui/src/chat/ui_chat_transcript.dart';

Map<String, dynamic> user(String id, String text) => {
  'id': '$id/user',
  'role': 0,
  'text': text,
};

Map<String, dynamic> assistant(
  String id,
  String text, {
  String status = 'completed',
  String? error,
  String? model,
  Map<String, dynamic>? app,
}) => {
  'id': '$id/assistant',
  'role': 1,
  'text': text,
  'status': status,
  'error': ?error,
  'model': ?model,
  'app': ?app,
};

Future<void> show(
  WidgetTester tester,
  List<Map<String, dynamic>> messages, {
  void Function(String)? onRetry,
  void Function(Map<String, dynamic>)? onOpenApp,
  Size size = const Size(900, 700),
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      theme: UiTheme.light('intochat'),
      home: Scaffold(
        body: UiChatTranscript(
          messages: messages,
          markdown: (text) => Text(text),
          onRetry: onRetry,
          onOpenApp: onOpenApp,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('user_messages_are_bubbles_and_assistant_messages_plain_text', (
    tester,
  ) async {
    await show(tester, [user('r1', 'Hi there'), assistant('r1', 'Hello!')]);
    final c = UiColors.of(tester.element(find.text('Hi there')));
    final bubble = tester.widget<Container>(
      find
          .ancestor(of: find.text('Hi there'), matching: find.byType(Container))
          .first,
    );
    final decoration = bubble.decoration! as BoxDecoration;
    expect(decoration.color, c.userBubble);
    expect(tester.widget<Text>(find.text('Hi there')).style?.color, c.userText);
    // Right-aligned and at most 72 % of the column.
    final rect = tester.getRect(find.byKey(const ValueKey('r1/user/bubble')));
    final column = tester.getRect(
      find.byKey(const ValueKey('transcript-column')),
    );
    expect(rect.right, moreOrLessEquals(column.right, epsilon: 0.5));
    expect(rect.width, lessThanOrEqualTo(column.width * 0.72 + 0.5));
    // The answer is plain text: no "Assistant" label and no card around it.
    expect(find.text('Assistant'), findsNothing);
    expect(find.text('You'), findsNothing);
    expect(
      find.ancestor(of: find.text('Hello!'), matching: find.byType(Card)),
      findsNothing,
    );
  });

  testWidgets('toolbar_copies_retries_and_names_the_model', (tester) async {
    final copied = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'Clipboard.setData') {
          copied.add((call.arguments as Map)['text'] as String);
        }
        return null;
      },
    );
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );
    final retried = <String>[];
    await show(tester, [
      user('r1', 'First'),
      assistant('r1', 'Old answer', model: 'gpt-5-mini'),
      user('r2', 'Second'),
      assistant('r2', 'New answer', model: 'claude-sonnet'),
    ], onRetry: retried.add);
    expect(find.byType(ChatMessageToolbar), findsNWidgets(2));
    expect(find.text('gpt-5-mini'), findsOneWidget);
    expect(find.text('claude-sonnet'), findsOneWidget);
    expect(find.bySemanticsLabel('Answered by claude-sonnet'), findsOneWidget);
    // Retry only on the last answer; it resends the message it answered.
    expect(find.byTooltip(ChatCopy.retry), findsOneWidget);
    await tester.tap(find.byTooltip(ChatCopy.retry));
    expect(retried, ['Second']);
    await tester.tap(find.byTooltip(ChatCopy.copy).last);
    await tester.pump();
    expect(copied, ['New answer']);
    expect(find.text(ChatCopy.copied), findsOneWidget);
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
  });

  testWidgets('failed_turn_shows_int20_copy_not_the_error', (tester) async {
    final retried = <String>[];
    await show(tester, [
      user('r1', 'Question'),
      assistant(
        'r1',
        '',
        status: 'failed',
        error: 'HttpRequestException: 401 sk-secret-key',
      ),
    ], onRetry: retried.add);
    expect(find.text(ChatCopy.failed), findsOneWidget);
    expect(find.textContaining('sk-secret-key'), findsNothing);
    expect(find.textContaining('HttpRequestException'), findsNothing);
    final c = UiColors.of(tester.element(find.text(ChatCopy.failed)));
    expect(
      tester.widget<Text>(find.text(ChatCopy.failed)).style?.color,
      c.error,
    );
    await tester.tap(find.widgetWithText(UiKitButton, ChatCopy.retry));
    expect(retried, ['Question']);
  });

  testWidgets('empty_transcript_shows_int20_copy', (tester) async {
    await show(tester, const []);
    expect(find.text(ChatCopy.empty), findsOneWidget);
    expect(find.text('Start a conversation'), findsNothing);
  });

  testWidgets('jump_to_latest_appears_when_scrolled_up', (tester) async {
    await show(tester, [
      for (var i = 0; i < 40; i++) ...[
        user('r$i', 'Question $i'),
        assistant('r$i', 'Answer $i'),
      ],
    ], size: const Size(600, 400));
    expect(find.byTooltip(ChatCopy.jumpToLatest), findsNothing);
    // Less than a viewport from the end: still no button.
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 200));
    await tester.pumpAndSettle();
    expect(find.byTooltip(ChatCopy.jumpToLatest), findsNothing);
    await tester.drag(find.byType(Scrollable).first, const Offset(0, 1200));
    await tester.pumpAndSettle();
    expect(find.byTooltip(ChatCopy.jumpToLatest), findsOneWidget);
    await tester.tap(find.byTooltip(ChatCopy.jumpToLatest));
    await tester.pumpAndSettle();
    expect(find.text('Answer 39').hitTestable(), findsOneWidget);
    expect(find.byTooltip(ChatCopy.jumpToLatest), findsNothing);
  });

  testWidgets('app_card_install_opens_the_apps_detail', (tester) async {
    final opened = <Map<String, dynamic>>[];
    await show(tester, [
      assistant(
        'r1/assistant',
        'Opened.',
        app: {
          'app': 'ledger',
          'title': 'Ledger',
          'icon': '',
          'installed': false,
          'input': '',
          'publisher': 'vlad',
          'release': '1.2.3',
        },
      ),
    ], onOpenApp: opened.add);
    final card = find.byType(ChatAppCard);
    expect(
      find.descendant(of: card, matching: find.text(ChatCopy.notInstalled)),
      findsOneWidget,
    );
    await tester.tap(find.widgetWithText(UiKitButton, ChatCopy.install));
    expect(opened, [
      {
        'kind': 'workspace',
        'application': 'apps',
        'operation': 'open',
        'input': 'app:vlad/ledger@1.2.3',
      },
    ]);
  });

  testWidgets('app_card_without_publisher_has_no_install', (tester) async {
    await show(tester, [
      assistant(
        'r1/assistant',
        'Opened.',
        app: {
          'app': 'ledger',
          'title': 'Ledger',
          'icon': '',
          'installed': false,
          'input': '',
        },
      ),
    ], onOpenApp: (_) {});
    expect(find.text(ChatCopy.notInstalled), findsOneWidget);
    expect(find.byType(UiKitButton), findsNothing);
  });

  testWidgets('app_card_opens_the_app_and_uninstalled_shows_badge', (
    tester,
  ) async {
    final opened = <Map<String, dynamic>>[];
    await show(tester, [
      assistant(
        'post/link-a',
        "Here's the app from your link.",
        app: {
          'app': 'notes',
          'title': '<b>Notes</b> **bold**',
          'icon': 'file',
          'installed': true,
          'input': 'page=7&x',
        },
      ),
      assistant(
        'post/link-b',
        "Here's the app from your link.",
        app: {
          'app': 'ledger',
          'title': 'Ledger',
          'icon': '',
          'installed': false,
          'input': '',
        },
      ),
    ], onOpenApp: opened.add);
    expect(find.byType(ChatAppCard), findsNWidgets(2));
    // The title is shown as typed, never as markup.
    expect(find.text('<b>Notes</b> **bold**'), findsOneWidget);
    expect(find.widgetWithText(UiKitButton, ChatCopy.open), findsOneWidget);
    await tester.tap(find.widgetWithText(UiKitButton, ChatCopy.open));
    expect(opened, [
      {
        'kind': 'workspace',
        'application': 'notes',
        'operation': 'open',
        'input': 'page=7&x',
      },
    ]);
    final uninstalled = find.ancestor(
      of: find.text('Ledger'),
      matching: find.byType(ChatAppCard),
    );
    expect(
      find.descendant(
        of: uninstalled,
        matching: find.text(ChatCopy.notInstalled),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(of: uninstalled, matching: find.byType(UiKitButton)),
      findsNothing,
    );
  });

  testWidgets('a failed activity shows fixed copy, not its error', (
    tester,
  ) async {
    await show(tester, [
      user('r1', 'Look it up'),
      {
        ...assistant('r1', 'Done.'),
        'activities': [
          {
            'id': 'call',
            'title': 'Query database',
            'input': 'select 1',
            'error': 'NpgsqlException: password=hunter2',
          },
        ],
      },
    ]);
    await tester.tap(find.text('1 activity · 1 failed'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Query database'));
    await tester.pumpAndSettle();
    expect(find.text(ChatCopy.stepFailed), findsOneWidget);
    expect(find.textContaining('hunter2'), findsNothing);
    expect(find.textContaining('NpgsqlException'), findsNothing);
  });
}
