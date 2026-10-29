import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/auth/brain_session_gate.dart';
import 'package:intochat_os_flutter_shell/auth/local_account.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  for (final (brightness, expected) in [
    (Brightness.light, UiColors.intochatLight),
    (Brightness.dark, UiColors.intochatDark),
  ]) {
    testWidgets('gate screens use IntoChat colours in $brightness', (
      tester,
    ) async {
      tester.platformDispatcher.platformBrightnessTestValue = brightness;
      addTearDown(tester.platformDispatcher.clearAllTestValues);
      await tester.pumpWidget(
        BrainSessionGate(
          readLocalAccount: (_) async => null,
          createClient: (_) => IntoChatOSUiClient(
            baseUri: Uri.parse('http://localhost'),
            httpClient: MockClient((_) async => http.Response('', 401)),
          ),
          builder: (_, _, _) => const SizedBox(),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('login-submit')), findsOneWidget);
      final context = tester.element(find.byKey(const Key('login-submit')));
      expect(UiColors.of(context), expected);
    });
  }

  testWidgets('failure screen uses IntoChat colours', (tester) async {
    tester.platformDispatcher.platformBrightnessTestValue = Brightness.dark;
    addTearDown(tester.platformDispatcher.clearAllTestValues);
    await tester.pumpWidget(
      BrainSessionGate(
        readLocalAccount: (_) async => const LocalAccount('v', 'p'),
        createClient: (_) => IntoChatOSUiClient(
          baseUri: Uri.parse('http://localhost'),
          httpClient: MockClient(
            (_) async => throw const SocketException('offline'),
          ),
        ),
        builder: (_, _, _) => const SizedBox(),
      ),
    );
    await tester.pumpAndSettle();
    final context = tester.element(find.text('Retry'));
    expect(UiColors.of(context), UiColors.intochatDark);
  });
}
