import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/auth/brain_session_gate.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

// 09-04: a visitor with no member session opens the lobby session; the lobby is generic plumbing (a brain id and a
// chromeless flag), never a redirect to a server-supplied page and never a request to /auth/entry.
void main() {
  late List<String> requested;
  late bool signedIn;
  late int lobbyStatus;

  setUp(() {
    requested = [];
    signedIn = false;
    lobbyStatus = 200;
  });

  http.Client server() => MockClient((request) async {
    requested.add(request.url.path);
    switch (request.url.path) {
      case '/auth/check':
        return http.Response('', 204);
      case '/identity/session':
        return signedIn
            ? http.Response(
                jsonEncode({
                  'principalId': 'person',
                  'accountId': 'personal-account',
                  'brainId': 'member-brain',
                }),
                200,
              )
            : http.Response('', 204);
      case '/lobby/session':
        return lobbyStatus == 200
            ? http.Response(
                jsonEncode({'brainId': 'lobby-brain', 'chromeless': true}),
                200,
              )
            : http.Response('', lobbyStatus);
    }
    return http.Response('', 404);
  });

  Widget gate(List<IntoChatOSUiClient> shells) => BrainSessionGate(
    readLocalAccount: (_) async => null,
    createClient: (_) => IntoChatOSUiClient(
      baseUri: Uri.parse('https://kernel.test'),
      httpClient: server(),
    ),
    builder: (client, _, _) {
      shells.add(client!);
      return MaterialApp(home: Text('Shell ${client.defaultBrainId}'));
    },
  );

  testWidgets(
    'no member session and a lobby: the shell opens the lobby chrome-less',
    (tester) async {
      final shells = <IntoChatOSUiClient>[];
      await tester.pumpWidget(gate(shells));
      await tester.pumpAndSettle();
      expect(shells, hasLength(1));
      expect(shells.single.defaultBrainId, 'lobby-brain');
      expect(shells.single.chromeless, isTrue);
      expect(shells.single.principalId, isNull);
      expect(requested, contains('/lobby/session'));
    },
  );

  testWidgets(
    'no lobby on the host: the login screen stays and no entry page is requested',
    (tester) async {
      lobbyStatus = 404;
      final shells = <IntoChatOSUiClient>[];
      await tester.pumpWidget(gate(shells));
      await tester.pumpAndSettle();
      expect(shells, isEmpty);
      expect(find.byKey(const Key('login-username')), findsOneWidget);
    },
  );

  testWidgets(
    'LobbyEnded closes the lobby and opens the member default brain',
    (tester) async {
      final shells = <IntoChatOSUiClient>[];
      await tester.pumpWidget(gate(shells));
      await tester.pumpAndSettle();
      final lobby = shells.single;
      expect(lobby.defaultBrainId, 'lobby-brain');
      signedIn = true;
      await lobby.endLobby();
      await tester.pumpAndSettle();
      expect(shells, hasLength(2));
      expect(shells.last.defaultBrainId, 'member-brain');
      expect(shells.last.chromeless, isFalse);
      expect(identical(shells.last, lobby), isFalse);
    },
  );

  testWidgets('a session that is not a lobby ignores LobbyEnded', (
    tester,
  ) async {
    signedIn = true;
    final shells = <IntoChatOSUiClient>[];
    await tester.pumpWidget(gate(shells));
    await tester.pumpAndSettle();
    expect(shells.single.defaultBrainId, 'member-brain');
    await shells.single.endLobby();
    await tester.pumpAndSettle();
    expect(shells, hasLength(1));
    expect(requested, isNot(contains('/lobby/session')));
  });

  testWidgets('/auth/entry is never requested', (tester) async {
    final shells = <IntoChatOSUiClient>[];
    await tester.pumpWidget(gate(shells));
    await tester.pumpAndSettle();
    lobbyStatus = 404;
    expect(requested, isNot(contains('/auth/entry')));
  });
}
