import 'dart:convert';
import 'dart:io';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/auth/brain_session_gate.dart';
import 'package:intochat_os_flutter_shell/auth/local_account.dart';
import 'package:intochat_os_flutter_shell/auth/local_account_io.dart' as local;
import 'package:intochat_os_flutter_shell/auth/local_account_web.dart' as web;
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  testWidgets('localStartupEntersVladAndSwitchAccountDoesNotReturnToVlad', (
    tester,
  ) async {
    var localReads = 0;
    var logins = 0;
    await tester.pumpWidget(
      BrainSessionGate(
        readLocalAccount: (uri) async {
          localReads++;
          return const LocalAccount('vlad', 'local-test-password');
        },
        createClient: (_) => IntoChatOSUiClient(
          baseUri: Uri.parse('http://localhost'),
          httpClient: MockClient((request) async {
            if (request.url.path == '/identity/login') logins++;
            return http.Response(
              '{"principalId":"vlad","accountId":"real-account","brainId":"real-brain"}',
              200,
            );
          }),
        ),
        builder: (client, status, switchAccount) => MaterialApp(
          home: Scaffold(
            body: TextButton(
              onPressed: switchAccount,
              child: Text(
                'Account: ${client?.principalId}/${client?.defaultBrainId}',
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Account: vlad/real-brain'), findsOneWidget);
    expect(logins, 1);
    await tester.tap(find.byType(TextButton));
    await tester.pumpAndSettle();
    expect(find.text('Account: vlad/real-brain'), findsNothing);
    expect(localReads, 1);
    expect(logins, 1);
  });

  testWidgets('anUnavailableServerDoesNotEnterOwner', (tester) async {
    var entered = false;
    await tester.pumpWidget(
      BrainSessionGate(
        readLocalAccount: (_) async =>
            const LocalAccount('vlad', 'local-test-password'),
        createClient: (_) => IntoChatOSUiClient(
          baseUri: Uri.parse('http://localhost'),
          httpClient: MockClient(
            (_) async => throw const SocketException('offline'),
          ),
        ),
        builder: (_, _, _) {
          entered = true;
          return const SizedBox();
        },
      ),
    );
    await tester.pumpAndSettle();
    expect(entered, isFalse);
    expect(find.text('Could not open your local account.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });

  test(
    'local file uses explicit opt in and refuses remote before reading',
    () async {
      final folder = await Directory.systemTemp.createTemp(
        'local-account-test-',
      );
      addTearDown(() => folder.delete(recursive: true));
      final file = File('${folder.path}/vlad.json');
      await file.writeAsString(
        jsonEncode({'principalId': 'vlad', 'password': 'A' * 64}),
      );
      final environment = {'INTOCHATOS_LOCAL_ACCOUNT_FILE': file.path};
      final account = await local.readLocalAccount(
        Uri.parse('http://localhost'),
        environment: environment,
      );
      expect(account?.principalId, 'vlad');
      expect(
        await local.readLocalAccount(
          Uri.parse('http://localhost'),
          environment: {},
        ),
        isNull,
      );
      await file.delete();
      await expectLater(
        local.readLocalAccount(
          Uri.parse('https://example.test'),
          environment: environment,
        ),
        throwsStateError,
      );
      expect(
        await web.readLocalAccount(Uri.parse('https://example.test')),
        isNull,
      );
    },
  );
}
