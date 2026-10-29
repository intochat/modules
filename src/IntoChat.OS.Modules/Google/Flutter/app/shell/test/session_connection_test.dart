import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/auth/brain_session_gate.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  testWidgets(
    'unreachable session stays outside workspace and retry restores verified session',
    (tester) async {
      var offline = true;
      var shellBuilds = 0;
      await tester.pumpWidget(
        BrainSessionGate(
          readLocalAccount: (_) async => null,
          createClient: (_) => IntoChatOSUiClient(
            baseUri: Uri.parse('http://kernel'),
            httpClient: MockClient((request) async {
              if (offline) throw http.ClientException('network unavailable');
              if (request.url.path == '/auth/check') {
                return http.Response('', 204);
              }
              return http.Response(
                jsonEncode({
                  'principalId': 'alice',
                  'accountId': 'alice-account',
                  'brainId': 'alice-home',
                }),
                200,
              );
            }),
          ),
          builder: (client, _, _) {
            shellBuilds++;
            return MaterialApp(home: Text(client!.workspaceIdentity));
          },
        ),
      );
      await tester.pumpAndSettle();
      expect(shellBuilds, 0);
      expect(find.text('Unable to connect to your account.'), findsOneWidget);
      offline = false;
      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();
      expect(find.text('alice|alice-account'), findsOneWidget);
      expect(shellBuilds, 1);
    },
  );
}
