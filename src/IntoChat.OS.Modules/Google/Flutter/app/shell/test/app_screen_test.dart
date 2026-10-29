import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter_shell/workspace/apps/app_screen.dart';

// The Creator holds no request open: implement answers Building immediately and the screen
// follows the activity feed until the Builder lands.
void main() {
  testWidgets('creates a live app: author, background implement', (
    tester,
  ) async {
    final server = _FakeCreatorServer();
    await tester.pumpWidget(
      MaterialApp(
        home: AppScreen(
          workspaceId: 'workspace-alice',
          request: server.request,
          pollInterval: const Duration(milliseconds: 20),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(
      find.byKey(const ValueKey('describe-app')),
      'Shout back whatever I say.',
    );
    await tester.tap(find.byKey(const ValueKey('author-app')));
    await tester.pumpAndSettle();
    expect(find.text('v0.1.0 · Drafted'), findsOneWidget);
    expect(find.textContaining('## Scenario: It shouts'), findsOneWidget);

    await tester.tap(find.byKey(const ValueKey('build-app')));
    await tester.pump();
    // The implement call answered 202 Building; the screen polls until Live.
    await tester.pump(const Duration(milliseconds: 30));
    await tester.pump(const Duration(milliseconds: 30));
    await tester.pumpAndSettle();
    expect(find.text('v0.2.0 · Live'), findsOneWidget);
    expect(find.text('build finished'), findsOneWidget);
  });

  testWidgets('a deploying app keeps polling and cannot be implemented', (
    tester,
  ) async {
    final server = _FakeCreatorServer(inProgress: 'Deploying');
    await tester.pumpWidget(
      MaterialApp(
        home: AppScreen(
          workspaceId: 'workspace-alice',
          request: server.request,
          pollInterval: const Duration(milliseconds: 20),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const ValueKey('describe-app')),
      'Shout back whatever I say.',
    );
    await tester.tap(find.byKey(const ValueKey('author-app')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('build-app')));
    await tester.pump();
    final implement = tester.widget<FilledButton>(
      find.byKey(const ValueKey('build-app')),
    );
    expect(implement.onPressed, isNull);
    expect(find.byKey(const ValueKey('publish-app')), findsNothing);
    await tester.pump(const Duration(milliseconds: 30));
    await tester.pump(const Duration(milliseconds: 30));
    await tester.pumpAndSettle();
    expect(find.text('v0.2.0 · Live'), findsOneWidget);
  });

  testWidgets('a live version can be published once', (tester) async {
    String? published;
    final calls = <String>[];
    Future<dynamic> request(String method, String path, [Object? body]) async {
      calls.add('$method $path');
      if (method == 'POST' && path.endsWith('/publish')) {
        published = '1.0.0';
        return {'publishedVersion': published};
      }
      return {
        'name': 'shouter',
        'manifest': {'title': 'Shouter', 'description': 'Shouts back.'},
        'spec': 'Shouts back.',
        'version': '1.0.0',
        'status': 'Live',
        'activity': <String>[],
        'versions': [
          {'version': '1.0.0', 'savedAt': '2026-10-05T00:00:00Z'},
        ],
        'publishedVersion': published,
      };
    }

    Future<void> show() async {
      await tester.pumpWidget(
        MaterialApp(
          home: AppScreen(
            key: UniqueKey(),
            workspaceId: 'workspace-alice',
            request: request,
            name: 'shouter',
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    await show();
    await tester.tap(find.byKey(const ValueKey('publish-app')));
    await tester.pumpAndSettle();
    expect(
      calls,
      contains('POST /brains/workspace-alice/apps/shouter/publish'),
    );
    expect(find.text('Published v1.0.0'), findsOneWidget);
    // Already published at this version: nothing more to publish.
    expect(find.byKey(const ValueKey('publish-app')), findsNothing);
  });

  testWidgets('an installed copy says where it came from', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: AppScreen(
          workspaceId: 'workspace-alice',
          name: 'customer-researcher',
          request: (method, path, [body]) async => {
            'name': 'customer-researcher',
            'manifest': {'title': 'Customer Researcher'},
            'spec': '',
            'version': '1.2.0',
            'status': 'Live',
            'origin': {
              'publisher': 'intochat',
              'name': 'customer-researcher',
              'version': '1.2.0',
            },
          },
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.text('Installed from intochat/customer-researcher v1.2.0'),
      findsOneWidget,
    );
  });

  testWidgets('an edited copy of a shipped app saves to source on request', (
    tester,
  ) async {
    var saved = false;
    final calls = <String>[];
    Future<dynamic> request(String method, String path, [Object? body]) async {
      calls.add('$method $path');
      if (method == 'POST' && path.endsWith('/save-to-source')) saved = true;
      final version = saved ? '1.2.2' : '1.2.1';
      return {
        'name': 'greeter',
        'manifest': {'title': 'Greeter'},
        'spec': '',
        'version': version,
        'status': 'Live',
        'origin': {
          'publisher': 'intochat',
          'source': {r'$source': 'shipped'},
          'name': 'greeter',
          'version': saved ? '1.2.2' : '1.2.0',
        },
        'edited': !saved,
      };
    }

    await tester.pumpWidget(
      MaterialApp(
        home: AppScreen(
          workspaceId: 'workspace-alice',
          name: 'greeter',
          request: request,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('save-to-source')));
    await tester.pumpAndSettle();

    expect(
      calls,
      contains('POST /brains/workspace-alice/apps/greeter/save-to-source'),
    );
    expect(find.text('Installed from intochat/greeter v1.2.2'), findsOneWidget);
    expect(find.byKey(const ValueKey('save-to-source')), findsNothing);
  });
}

class _FakeCreatorServer {
  _FakeCreatorServer({this.inProgress = 'Building'});

  final String inProgress;
  final calls = <String>[];
  var _polls = 0;

  Map<String, dynamic> _app(String status, String version) => {
    'name': 'shouter',
    'manifest': {'title': 'Shouter', 'description': 'Shouts back.'},
    'spec': '## Scenario: It shouts\nAsking "hello" answers "HELLO!".',
    'version': version,
    'status': status,
    'activity': status == 'Building'
        ? ['build started']
        : status == 'Live'
        ? ['build started', 'build finished']
        : <String>[],
  };

  Future<dynamic> request(String method, String path, [Object? body]) async {
    calls.add('$method $path');
    const base = '/brains/workspace-alice/apps';
    if (method == 'POST' && path == base) return _app('Drafted', '0.1.0');
    if (method == 'POST' && path == '$base/shouter/implement') {
      _polls = 0;
      return _app(inProgress, '0.1.0');
    }
    if (method == 'GET' && path == '$base/shouter') {
      return ++_polls < 2 ? _app(inProgress, '0.1.0') : _app('Live', '0.2.0');
    }
    throw StateError('Unexpected $method $path');
  }
}
