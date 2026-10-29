import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/auth/brain_session_gate.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  for (final hasSession in [true, false]) {
    testWidgets(
      hasSession
          ? 'shell_opens_desktop_when_identity_session_returns_brain'
          : 'shell_shows_sign_in_when_session_204',
      (tester) async {
        var shellBuilds = 0;
        await tester.pumpWidget(
          BrainSessionGate(
            readLocalAccount: (_) async => null,
            createClient: (_) => IntoChatOSUiClient(
              baseUri: Uri.parse('http://kernel'),
              httpClient: MockClient((request) async {
                if (request.url.path == '/auth/check') {
                  return http.Response('', 204);
                }
                if (request.url.path == '/identity/session' && hasSession) {
                  return http.Response(
                    jsonEncode({
                      'principalId': 'person',
                      'accountId': 'personal-account',
                      'brainId': 'personal-brain',
                    }),
                    200,
                  );
                }
                return http.Response('', 204);
              }),
            ),
            builder: (client, _, _) {
              shellBuilds++;
              return MaterialApp(
                home: Text('Desktop ${client!.defaultBrainId}'),
              );
            },
          ),
        );
        await tester.pumpAndSettle();
        expect(shellBuilds, hasSession ? 1 : 0);
        if (hasSession) {
          expect(find.text('Desktop personal-brain'), findsOneWidget);
        } else {
          expect(find.byKey(const Key('login-username')), findsOneWidget);
        }
      },
    );
  }
}
