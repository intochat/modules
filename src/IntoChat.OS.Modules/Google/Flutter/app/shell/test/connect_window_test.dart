import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:intochat_os_flutter_shell/connections/connect_window.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  galleryConnectTests();
  testWidgets('offers only the server catalog and never invents providers', (
    tester,
  ) async {
    Future<Object?> request(String path, {Map<String, Object?>? body}) async =>
        [];
    Future<Object?> catalog(String path, {Map<String, Object?>? body}) async =>
        [
          {
            'id': 'custom',
            'displayName': 'Custom CRM',
            'authMethods': ['token'],
          },
        ];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(request: request, connectorRequest: catalog),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Custom CRM'), findsOneWidget);
    expect(find.text('Salesforce'), findsNothing);
    expect(find.text('Gmail'), findsNothing);
  });
  testWidgets('empty offered catalog disables connecting', (tester) async {
    Future<Object?> request(String path, {Map<String, Object?>? body}) async =>
        [];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(request: request, connectorRequest: request),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.text('No connectors are available for this deployment.'),
      findsOneWidget,
    );
    expect(
      tester
          .widget<UiKitButton>(find.byKey(const Key('connect_submit')))
          .onPressed,
      isNull,
    );
  });
  testWidgets('masks the pasted value and never echoes it', (tester) async {
    final harness = _Harness();
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(
            request: harness.request,
            connectorRequest: harness.catalog,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    final valueField = tester.widget<TextField>(
      find.byKey(const Key('connect_value')),
    );
    expect(valueField.obscureText, isTrue);

    await tester.enterText(find.byKey(const Key('connect_name')), 'mail');
    await tester.enterText(
      find.byKey(const Key('connect_value')),
      'canary-token-9a1f',
    );
    await tester.tap(find.byKey(const Key('connect_submit')));
    await tester.pumpAndSettle();

    final connect = harness.calls.firstWhere(
      (call) => call.$1 == 'mail/connect',
    );
    expect(connect.$2, {
      'connectorId': 'supabase',
      'authMethodId': 'connection-string',
      'value': 'canary-token-9a1f',
    });
    expect(find.textContaining('canary-token-9a1f'), findsNothing);
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('connect_value')))
          .controller!
          .text,
      isEmpty,
    );
    expect(find.textContaining('Connected'), findsWidgets);
  });

  testWidgets('lists status and probes and disconnects', (tester) async {
    final harness = _Harness()
      ..records = [
        {
          'connectorId': 'supabase',
          'connectionId': 'db',
          'authMethodId': 'connection-string',
          'status': 'Expired',
        },
      ];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(
            request: harness.request,
            connectorRequest: harness.catalog,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('supabase · db'), findsOneWidget);
    expect(find.textContaining('Expired'), findsOneWidget);

    await tester.tap(find.byTooltip('Probe connection'));
    await tester.pumpAndSettle();
    expect(harness.calls.where((call) => call.$1 == 'db/probe'), [
      ('db/probe', null),
    ]);

    await tester.tap(find.byTooltip('Disconnect'));
    await tester.pumpAndSettle();
    expect(harness.calls.where((call) => call.$1 == 'db/disconnect'), [
      ('db/disconnect', null),
    ]);
    expect(harness.records, isEmpty);
  });

  testWidgets('encodes a connection id with a space or slash as one segment', (
    tester,
  ) async {
    final harness = _Harness()
      ..records = [
        {
          'connectorId': 'supabase',
          'connectionId': 'work db/2',
          'authMethodId': 'connection-string',
          'status': 'Connected',
        },
      ];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(
            request: harness.request,
            connectorRequest: harness.catalog,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Probe connection'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Disconnect'));
    await tester.pumpAndSettle();
    expect(
      harness.calls.map((call) => call.$1).where((path) => path.isNotEmpty),
      ['work%20db%2F2/probe', 'work%20db%2F2/disconnect'],
    );
  });
}

void galleryConnectTests() {
  test('an activation names its connection, else the connector', () {
    expect(
      connectWindowConnectionId({
        'connector': 'gmail',
        'connection': 'notes-mail',
      }),
      'notes-mail',
    );
    expect(connectWindowConnectionId({'connector': 'gmail'}), 'gmail');
    expect(
      connectWindowConnectionId({'connector': 'gmail', 'connection': 'a/b'}),
      'gmail',
    );
    expect(
      connectWindowConnectionId({'connector': 'gmail', 'connection': 7}),
      'gmail',
    );
  });
  testWidgets('the connect window signs in under the named connection', (
    tester,
  ) async {
    final calls = <String>[];
    Future<Object?> request(String path, {Map<String, Object?>? body}) async {
      calls.add(path);
      return path.endsWith('/login')
          ? {'url': 'https://accounts.google.com/o/oauth2/v2/auth?state=s'}
          : [];
    }

    Future<Object?> catalog(String path, {Map<String, Object?>? body}) async =>
        [
          {
            'id': 'gmail',
            'displayName': 'Gmail',
            'authMethods': ['google-oauth'],
          },
        ];
    final action = <String, dynamic>{
      'kind': 'connections',
      'connector': 'gmail',
      'connection': 'notes-mail',
    };
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ConnectWindow(
            request: request,
            connectorRequest: catalog,
            initialConnectorId: action['connector'] as String?,
            initialConnectionId: connectWindowConnectionId(action),
            onOpen: (url) async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('connect_oauth')));
    await tester.pumpAndSettle();
    expect(calls, contains('notes-mail/login'));
  });

  testWidgets(
    'a gallery Connect preselects its connector and signs in as the user',
    (tester) async {
      final calls = <(String, Map<String, Object?>?)>[];
      final opened = <Uri>[];
      Future<Object?> request(String path, {Map<String, Object?>? body}) async {
        calls.add((path, body));
        return path.endsWith('/login')
            ? {'url': 'https://accounts.google.com/o/oauth2/v2/auth?state=s'}
            : [];
      }

      Future<Object?> catalog(
        String path, {
        Map<String, Object?>? body,
      }) async => [
        {
          'id': 'github',
          'displayName': 'GitHub',
          'authMethods': ['token'],
        },
        {
          'id': 'gmail',
          'displayName': 'Gmail',
          'authMethods': ['google-oauth'],
        },
      ];
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: ConnectWindow(
              request: request,
              connectorRequest: catalog,
              initialConnectorId: 'gmail',
              initialConnectionId: 'gmail',
              onOpen: (url) async => opened.add(url),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('connect_name')))
            .controller!
            .text,
        'gmail',
      );
      await tester.tap(find.byKey(const Key('connect_oauth')));
      await tester.pumpAndSettle();
      final login = calls.singleWhere((call) => call.$1 == 'gmail/login');
      expect(login.$2, {
        'connectorId': 'gmail',
        'authMethodId': 'google-oauth',
      });
      expect(opened.single.host, 'accounts.google.com');
    },
  );
}

final class _Harness {
  List<Map<String, dynamic>> records = [];
  final List<(String, Map<String, Object?>?)> calls = [];

  Future<Object?> catalog(String path, {Map<String, Object?>? body}) async => [
    {
      'id': 'supabase',
      'displayName': 'Supabase database',
      'authMethods': ['connection-string'],
    },
  ];
  Future<Object?> request(String path, {Map<String, Object?>? body}) async {
    calls.add((path, body));
    switch (path) {
      case '':
        return records;
      case 'mail/connect':
        records = [
          {
            'connectorId': body!['connectorId'],
            'connectionId': 'mail',
            'authMethodId': body['authMethodId'],
            'status': 'Connected',
          },
        ];
        return records.single;
      case 'db/disconnect':
        records = [];
        return null;
      default:
        return records.single;
    }
  }
}
