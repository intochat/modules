import 'package:intochat_os_ui/src/theme/ui_theme.dart';
import 'package:intochat_os_ui/src/chat/ui_copyable_message.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('workspace primary actions use the workspace font', () {
    final style = UiTheme.workspace(Brightness.light)
        .filledButtonTheme
        .style!
        .textStyle!
        .resolve({})!;
    expect(style.fontFamily, UiType.bodyFamily);
    expect(style.fontFamilyFallback, UiType.bodyFallback);
  });
  testWidgets('copy action reserves space beside message content', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SizedBox(
            width: 340,
            child: UiCopyableMessage(
              copyText: (_) => 'Message',
              child: const SizedBox(
                key: Key('message_body'),
                height: 50,
                width: double.infinity,
              ),
            ),
          ),
        ),
      ),
    );
    final body = tester.getRect(find.byKey(const Key('message_body')));
    final copy = tester.getRect(find.byKey(const Key('ui_copy_message')));
    expect(copy.left, greaterThanOrEqualTo(body.right));
  });
  test('assistant typography inherits workspace font and fallback', () {
    final theme = UiTheme.workspace(Brightness.light).copyWith(
      textTheme: const TextTheme(
        bodyMedium: TextStyle(
          fontFamily: 'Workspace body',
          fontFamilyFallback: ['Body fallback'],
        ),
        bodySmall: TextStyle(
          fontFamily: 'Workspace small',
          fontFamilyFallback: ['Small fallback'],
        ),
        labelSmall: TextStyle(
          fontFamily: 'Workspace label',
          fontFamilyFallback: ['Label fallback'],
        ),
      ),
    );
    final typography = UiChatTheme.workspace(theme).typography;
    expect(typography.bodyMedium.fontFamily, 'Workspace body');
    expect(typography.bodyMedium.fontFamilyFallback, ['Body fallback']);
    expect(typography.bodySmall.fontFamily, 'Workspace small');
    expect(typography.labelSmall.fontFamily, 'Workspace label');
  });
}
