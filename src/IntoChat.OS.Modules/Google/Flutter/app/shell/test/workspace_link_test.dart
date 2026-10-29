import 'dart:async';
import 'dart:convert' show jsonEncode, jsonDecode;

import 'package:intochat_os_flutter_shell/workspace/workspace_app.dart';
import 'package:intochat_os_flutter_shell/workspace/workspace_link.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'application_fixture.dart';
import 'workspace_root_test.dart' show ActivationServer;

void main() {
  final input = jsonEncode({'page': 'profile', 'publisher': 'owner'});
  final route = Uri(
    path: '/open',
    queryParameters: {'application': 'circle', 'input': input},
  );

  test(
    'shared links preserve opaque input and cannot select a brain or operation',
    () {
      for (final uri in [route, Uri.parse('https://host.example/#$route')]) {
        expect(workspaceLink(uri), {
          'kind': 'workspace',
          'application': 'circle',
          'operation': 'open',
          'input': input,
        });
      }
      final hostile = route.replace(
        queryParameters: {
          ...route.queryParameters,
          'brainId': 'owner-brain',
          'operation': 'delete',
          'neuron': 'owner-brain/apps/private',
          'view': 'configuration',
        },
      );
      expect(workspaceLink(hostile), workspaceLink(route));
      for (final application in [
        '',
        '../private',
        'owner/apps/private',
        'a%2Fb',
      ]) {
        expect(
          workspaceLink(
            Uri(path: '/open', queryParameters: {'application': application}),
          ),
          isNull,
        );
      }
      expect(workspaceLink(Uri.parse('/settings')), isNull);
    },
  );

  test('share_links_reject_bad_publisher_and_release', () {
    Map<String, dynamic>? link(Map<String, String> query) =>
        workspaceLink(Uri(path: '/open', queryParameters: query));
    expect(link({'application': 'notes', 'publisher': 'Vlad'}), isNull);
    expect(link({'application': 'notes', 'publisher': '-vlad'}), isNull);
    expect(link({'application': 'notes', 'publisher': 'a' * 65}), isNull);
    expect(
      link({'application': 'notes', 'publisher': 'vlad', 'release': '1.0'}),
      isNull,
    );
    expect(link({'application': 'notes', 'release': '1.0.0'}), isNull);
    expect(link({'application': 'notes', 'input': 'x' * 2049}), isNull);
    expect(link({'application': 'notes', 'input': 'x' * 2048}), isNotNull);
  });

  test('link_grammar_matches_the_apps_rules', () {
    Map<String, dynamic>? parse(String query) =>
        workspaceLink(Uri.parse('https://h/#/open?$query'));
    expect(parse('application=notes&publisher=vlad%0A'), isNull);
    expect(parse('application=notes&publisher=vlad&release=1.2.3%0A'), isNull);
    expect(
      parse('application=notes&publisher=vlad&release=%D9%A1.%D9%A2.%D9%A3'),
      isNull,
    );
    expect(parse('application=notes&application=other'), isNull);
    expect(parse('application=notes&input=a&input=b'), isNull);
    expect(parse('application=notes&publisher=a&publisher=b'), isNull);
    expect(
      parse('application=notes&publisher=a&release=1.0.0&release=2.0.0'),
      isNull,
    );
    expect(parse('application=notes&publisher=1vlad'), isNull);
    expect(parse('application=1notes&publisher=vlad'), isNull);
    expect(parse('application=${'a' * 65}&publisher=vlad'), isNull);
    expect(parse('application=${'a' * 64}&publisher=vlad'), isNotNull);
    expect(parse('application=my_app&publisher=my_pub'), isNotNull);
    expect(
      parse('application=notes&publisher=vlad&release=1234567890.0.0'),
      isNull,
    );
    expect(
      parse('application=notes&publisher=vlad&release=123456789.0.0'),
      isNotNull,
    );
  });

  test('publisher_links_open_the_apps_detail', () {
    Map<String, dynamic>? link(Map<String, String> query) =>
        workspaceLink(Uri(path: '/open', queryParameters: query));
    expect(link({'application': 'notes', 'publisher': 'vlad'}), {
      'kind': 'workspace',
      'application': 'apps',
      'operation': 'open',
      'input': 'app:vlad/notes',
    });
    expect(
      link({'application': 'notes', 'publisher': 'vlad', 'release': '1.2.3'}),
      {
        'kind': 'workspace',
        'application': 'apps',
        'operation': 'open',
        'input': 'app:vlad/notes@1.2.3',
      },
    );
  });

  test('plain_links_are_unchanged', () {
    expect(
      workspaceLink(
        Uri(path: '/open', queryParameters: {'application': 'notes'}),
      ),
      {
        'kind': 'workspace',
        'application': 'notes',
        'operation': 'open',
        'input': '',
      },
    );
  });

  testWidgets('app-authored account action invokes the session gate', (
    tester,
  ) async {
    var switches = 0;
    final fixture = await ApplicationFixture.start(
      tester,
      server: AccountActivationServer(),
      onSwitchAccount: () async {
        switches++;
      },
    );
    await tester.tap(find.text('Switch account'));
    await tester.pumpAndSettle();
    expect(switches, 1);
    expect(
      fixture.server.requests.where((r) => r.path.endsWith('/request')),
      isEmpty,
    );
    await fixture.dispose();
  });

  testWidgets(
    'latest link waits for the recipient root and restores its browser URL',
    (tester) async {
      final server = DelayedActivationServer();
      final navigation = <MethodCall>[];
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.navigation,
        (call) async {
          navigation.add(call);
          return null;
        },
      );
      addTearDown(
        () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          SystemChannels.navigation,
          null,
        ),
      );
      final fixture = await ApplicationFixture.start(tester, server: server);
      Future<void> push(Uri uri) =>
          tester.binding.defaultBinaryMessenger.handlePlatformMessage(
            'flutter/navigation',
            const JSONMethodCodec().encodeMethodCall(
              MethodCall('pushRouteInformation', {
                'location': uri.toString(),
                'state': null,
              }),
            ),
            (_) {},
          );
      await push(
        Uri(
          path: '/open',
          queryParameters: {'application': 'circle', 'input': 'old'},
        ),
      );
      await push(route);
      expect(
        server.requestsTo('/brains/brain-1/apps/navigation/request'),
        isEmpty,
      );
      server.connected.complete();
      await tester.pumpAndSettle();
      final requests = server.requestsTo(
        '/brains/brain-1/apps/navigation/request',
      );
      expect(requests, hasLength(1));
      expect(
        jsonDecode(requests.single.body['input'] as String),
        workspaceLink(route),
      );
      final updates = navigation.where(
        (call) => call.method == 'routeInformationUpdated',
      );
      expect(updates.last.arguments['uri'], route.toString());
      expect(updates.last.arguments['replace'], isTrue);
      await fixture.dispose();
    },
  );

  testWidgets(
    'initial shared link dispatches through recipient app-owned root',
    (tester) async {
      final server = ActivationServer('brain-1');
      final fixture = await ApplicationFixture.start(tester, server: server);
      await tester.pumpWidget(
        WorkspaceApp(
          key: const ValueKey('shared-link'),
          store: fixture.store,
          programmingClient: fixture.client,
          initialLocation: route,
        ),
      );
      await tester.pumpAndSettle();
      final requests = server.requestsTo(
        '/brains/brain-1/apps/navigation/request',
      );
      expect(requests, hasLength(1));
      expect(requests.single.body['operation'], 'activate');
      expect(
        jsonDecode(requests.single.body['input'] as String),
        workspaceLink(route),
      );
      expect(server.requests.where((r) => r.path.contains('owner')), isEmpty);
      await fixture.dispose();
    },
  );

  testWidgets(
    'running shell accepts another shared link through platform navigation',
    (tester) async {
      final server = ActivationServer('brain-1');
      final fixture = await ApplicationFixture.start(tester, server: server);
      final pushed = tester.binding.defaultBinaryMessenger
          .handlePlatformMessage(
            'flutter/navigation',
            const JSONMethodCodec().encodeMethodCall(
              MethodCall('pushRouteInformation', {
                'location': route.toString(),
                'state': null,
              }),
            ),
            (_) {},
          );
      await tester.pumpAndSettle();
      await pushed;
      final requests = server.requestsTo(
        '/brains/brain-1/apps/navigation/request',
      );
      expect(requests, hasLength(1));
      expect(
        jsonDecode(requests.single.body['input'] as String),
        workspaceLink(route),
      );
      await fixture.dispose();
    },
  );
}

class AccountActivationServer extends ActivationServer {
  AccountActivationServer() : super('brain-1');
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (request.url.path.endsWith('/apps/node')) {
      return json({
        'label': 'Switch account',
        'enabled': true,
        'activation': jsonEncode({'kind': 'account'}),
      });
    }
    return super.send(request);
  }
}

class DelayedActivationServer extends ActivationServer {
  DelayedActivationServer() : super('brain-1');
  final connected = Completer<void>();
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (request.url.path.endsWith('/apps/connect')) await connected.future;
    return super.send(request);
  }
}
