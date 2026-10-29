import 'package:intochat_os_flutter_shell/connections/windows_storage_setup.dart';
import 'package:intochat_os_flutter_shell/connections/connect_window.dart';

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'the Windows connection dialog replaces manual credentials at narrow width',
    (tester) async {
      tester.view.physicalSize = const Size(390, 740);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      Future<Object?> connections(
        String path, {
        Map<String, Object?>? body,
      }) async => <Object?>[];
      Future<Object?> connectors(
        String path, {
        Map<String, Object?>? body,
      }) async => [
        {
          'id': 'windows',
          'displayName': 'Windows',
          'authMethods': ['device'],
        },
      ];
      Future<Object?> windows(
        String path, {
        Map<String, Object?>? body,
      }) async => {'machineId': 'machine', 'machineName': 'HOST-PC'};
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: ConnectWindow(
              request: connections,
              connectorRequest: connectors,
              windowsRequest: windows,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Connect HOST-PC'), findsOneWidget);
      expect(find.byKey(const Key('connect_name')), findsNothing);
      expect(find.byKey(const Key('connect_value')), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'changing brain ignores delayed discovery and clears old folder input',
    (tester) async {
      final delayed = Completer<Object?>();
      Future<Object?> connections(
        String path, {
        Map<String, Object?>? body,
      }) async => [];
      Future<Object?> oldWindows(String path, {Map<String, Object?>? body}) =>
          delayed.future;
      Future<Object?> newWindows(
        String path, {
        Map<String, Object?>? body,
      }) async => {'machineId': 'new', 'machineName': 'NEW-PC'};
      Widget surface(ConnectionsRequest windows) => MaterialApp(
        home: Scaffold(
          body: WindowsStorageSetup(
            request: connections,
            windowsRequest: windows,
          ),
        ),
      );
      await tester.pumpWidget(surface(oldWindows));
      await tester.pumpWidget(surface(newWindows));
      await tester.pumpAndSettle();
      delayed.complete({'machineId': 'old', 'machineName': 'OLD-PC'});
      await tester.pumpAndSettle();
      expect(find.text('Connect NEW-PC'), findsOneWidget);
      expect(find.textContaining('OLD-PC'), findsNothing);
    },
  );

  testWidgets('a refused folder keeps its entered path and reports an error', (
    tester,
  ) async {
    Future<Object?> connections(
      String path, {
      Map<String, Object?>? body,
    }) async => [
      {
        'connectorId': 'windows',
        'connectionId': 'machine',
        'status': 'Connected',
      },
    ];
    Future<Object?> windows(String path, {Map<String, Object?>? body}) async {
      if (path == 'device') {
        return {'machineId': 'machine', 'machineName': 'HOST-PC'};
      }
      if (body != null) throw StateError('refused');
      return [];
    }

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: WindowsStorageSetup(
            request: connections,
            windowsRequest: windows,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('windows_folder_path')),
      'C:/missing',
    );
    await tester.tap(find.text('Allow folder'));
    await tester.pumpAndSettle();
    expect(find.textContaining('The folder change failed.'), findsOneWidget);
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('windows_folder_path')))
          .controller!
          .text,
      'C:/missing',
    );
    expect(find.text('No folders allowed yet.'), findsOneWidget);
  });

  testWidgets('discovers host and connects without a machine id field', (
    tester,
  ) async {
    bool connected = false;
    final roots = <String>[];
    final actions = <String>[];
    Future<Object?> connections(
      String path, {
      Map<String, Object?>? body,
    }) async {
      if (path.isEmpty) {
        return connected
            ? [
                {
                  'connectorId': 'windows',
                  'connectionId': 'machine',
                  'status': 'Connected',
                },
              ]
            : [];
      }
      expect(path, 'machine/connect');
      expect(body, {
        'connectorId': 'windows',
        'authMethodId': 'device',
        'value': 'machine',
      });
      connected = true;
      return {'status': 'Connected'};
    }

    Future<Object?> windows(String path, {Map<String, Object?>? body}) async {
      if (path == 'device') {
        return {'machineId': 'machine', 'machineName': 'HOST-PC'};
      }
      if (path.endsWith('/allow')) {
        roots.add(body!['path'] as String);
        actions.add(path);
      }
      if (path.endsWith('/revoke')) {
        roots.remove(body!['path']);
        actions.add(path);
      }
      return [...roots];
    }

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: WindowsStorageSetup(
              request: connections,
              windowsRequest: windows,
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Connect HOST-PC'), findsOneWidget);
    expect(find.byType(TextField), findsNothing);
    await tester.tap(find.text('Connect HOST-PC'));
    await tester.pumpAndSettle();
    expect(find.text('No folders allowed yet.'), findsOneWidget);
    await tester.enterText(
      find.byKey(const Key('windows_folder_path')),
      'C:/task-folder',
    );
    await tester.tap(find.text('Allow folder'));
    await tester.pumpAndSettle();
    expect(find.text('C:/task-folder'), findsOneWidget);
    await tester.tap(find.byTooltip('Revoke C:/task-folder'));
    await tester.pumpAndSettle();
    expect(actions, ['machine/roots/allow', 'machine/roots/revoke']);
    expect(roots, isEmpty);
    expect(tester.takeException(), isNull);
  });
}
