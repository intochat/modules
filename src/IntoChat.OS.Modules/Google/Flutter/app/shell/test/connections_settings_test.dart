import 'dart:async';

import 'package:intochat_os_flutter_shell/connections/connections_settings.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('changing account ignores an earlier delayed response', (
    tester,
  ) async {
    final delayed = Completer<Object?>();
    Future<Object?> oldRequest(String path, {Map<String, Object?>? body}) =>
        delayed.future;
    Future<Object?> newRequest(
      String path, {
      Map<String, Object?>? body,
    }) async => [
      {
        'connectorId': 'gmail',
        'connectionId': 'new-account',
        'authMethodId': 'google-oauth',
        'status': 'Connected',
      },
    ];
    Widget surface(
      Future<Object?> Function(String, {Map<String, Object?>? body}) request,
    ) => MaterialApp(
      home: Scaffold(body: ConnectionsSettings(request: request)),
    );
    await tester.pumpWidget(surface(oldRequest));
    await tester.pumpWidget(surface(newRequest));
    await tester.pumpAndSettle();
    delayed.complete([
      {
        'connectorId': 'gmail',
        'connectionId': 'old-account',
        'authMethodId': 'google-oauth',
        'status': 'Connected',
      },
    ]);
    await tester.pumpAndSettle();
    expect(find.textContaining('new-account'), findsOneWidget);
    expect(find.textContaining('old-account'), findsNothing);
  });
  test(
    'the four server statuses show by name and anything else is unavailable',
    () {
      expect(connectionStatus('Connected'), 'Connected');
      expect(connectionStatus('Expired'), 'Expired');
      expect(connectionStatus('Failing'), 'Failing');
      expect(connectionStatus('Disconnected'), 'Disconnected');
      expect(connectionStatus('Verified'), 'Unavailable');
      expect(connectionStatus(null), 'Unavailable');
    },
  );
  testWidgets('narrow connections hide tokens and request authorization', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    String? action;
    Map<String, Object?>? actionBody;
    Uri? opened;
    Future<Object?> connections(
      String path, {
      Map<String, Object?>? body,
    }) async {
      if (path.isEmpty) {
        return [
          {
            'connectorId': 'gmail',
            'connectionId': 'mail',
            'authMethodId': 'google-oauth',
            'status': 'Connected',
          },
        ];
      }
      action = path;
      actionBody = body;
      return {
        'url': 'https://example.test/integrations/gmail/login?state=one-use',
      };
    }

    Future<Object?> services(String path, {Map<String, Object?>? body}) async {
      return [
        {
          'id': 'gmail',
          'displayName': 'Google',
          'authMethods': ['google-oauth'],
          'available': true,
          'capabilities': 'Gmail only',
        },
      ];
    }

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: ConnectionsSettings(
              request: connections,
              connectorRequest: services,
              onOpen: (url) async {
                opened = url;
              },
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Connected'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
    await tester.tap(find.text('Add connection'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Connect'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('connect_name')), 'mail');
    await tester.tap(find.text('Sign in with the provider'));
    await tester.pumpAndSettle();
    expect(action, 'mail/login');
    expect(actionBody, {
      'connectorId': 'gmail',
      'authMethodId': 'google-oauth',
    });
    expect(opened?.queryParameters['state'], 'one-use');
    expect(find.text('Verified'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
