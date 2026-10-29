import 'dart:async';
import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/space/neuron_space.dart';
import 'package:intochat_os_flutter_shell/space/space_controller.dart';
import 'package:intochat_os_flutter_shell/workspace/workspace_app.dart';
import 'package:intochat_os_flutter_shell/workspace/workspace_store.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

const assistantSummary = {
  'name': 'assistant',
  'title': 'Assistant',
  'description': 'Chat with your brain',
  'status': 'Live',
  'version': '1.0.0',
  'operations': ['open'],
};

/// GET /brains/{b}/apps/assistant: the view the dock reads its toolbar from.
const assistantView = {
  'name': 'assistant',
  'manifest': {
    'title': 'Assistant',
    'entry': {'kind': 'surface', 'name': 'surface'},
    'description': 'Chat with your brain',
    'icon': null,
    'operations': [
      {'name': 'open', 'description': ''},
      {'name': 'send', 'description': ''},
      {'name': 'new', 'description': ''},
      {'name': 'voice', 'description': ''},
    ],
    'toolbar': [
      {'operation': 'send', 'kind': 'text', 'label': 'Message your brain'},
      {'operation': 'voice', 'icon': 'mic', 'label': 'Voice'},
    ],
  },
};

/// A surface composed entirely from generic declared primitives.
///
/// Also answers an app request the way the kernel does: the request is accepted
/// at once with its id, and the app answers through neurons, never here.
Map<String, dynamic>? applicationResponse(http.BaseRequest request) {
  final segments = request.url.pathSegments;
  if (segments.length < 2 || segments.first != 'brains') return null;
  final root = '${segments[1]}/apps/assistant';
  if (request.url.path.endsWith('/request')) {
    return {'requestId': 'request-1'};
  }
  if (!request.url.path.endsWith('/apps/node')) return null;
  final name = request.url.queryParameters['name'] ?? '';
  return switch (request.url.queryParameters['kind']) {
    'surface' => {
      'definition': {
        'mode': 'column',
        'children': [
          {'kind': 'layout', 'name': '$root/messages'},
          {'kind': 'layout', 'name': '$root/prompts'},
          {'kind': 'textfield', 'name': '$root/draft'},
          {'kind': 'layout', 'name': '$root/footer'},
        ],
        'extents': [-1, 50, 70, 48],
      },
    },
    'layout' =>
      name.endsWith('/messages')
          ? {'mode': 'list', 'children': <Object>[]}
          : name.endsWith('/prompts')
          ? {
              'mode': 'row',
              'children': [
                {'kind': 'button', 'name': '$root/prompt-one'},
                {'kind': 'button', 'name': '$root/prompt-two'},
              ],
            }
          : {
              'mode': 'row',
              'children': [
                {'kind': 'fileinput', 'name': '$root/file'},
                {'kind': 'voiceinput', 'name': '$root/voice'},
                {'kind': 'button', 'name': '$root/send'},
              ],
            },
    'textfield' => {'label': 'Message', 'kind': 'multiline', 'value': ''},
    'voiceinput' => {'label': 'Dictate'},
    'fileinput' => {'label': 'Attach'},
    'button' => {
      'label': name.endsWith('prompt-one')
          ? 'Ask the assistant'
          : name.endsWith('prompt-two')
          ? 'Work with Salesforce'
          : 'Send',
      'enabled': true,
      'action': 'click',
    },
    _ => null,
  };
}

/// One request the fake kernel received, in arrival order.
class RecordedAppRequest {
  RecordedAppRequest(this.path, this.body);

  final String path;
  final Map<String, dynamic> body;
}

class RecordingLink implements NeuronLink {
  final watches = <(String, List<WatchSourceSpec>)>[];
  final events = StreamController<NeuronSignal>.broadcast();
  @override
  Stream<NeuronSignal> watch(String brainId, List<WatchSourceSpec> sources) {
    watches.add((brainId, sources));
    return events.stream;
  }

  @override
  Future<Map<String, dynamic>?> call(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments,
  ) async => null;
  @override
  Future<void> shutdown() async {
    await events.close();
  }
}

class ApplicationServer extends http.BaseClient {
  ApplicationServer({this.brainId = 'brain-1'});
  final String brainId;
  final requests = <RecordedAppRequest>[];
  final events = StreamController<List<int>>.broadcast();
  final paths = <String, List<String>>{};
  int revision = 0;
  bool developerMode = false, allowed = true, failOpen = false;
  String? renderedSurface;
  Map<String, dynamic>? root;
  List<RecordedAppRequest> requestsTo(String path) =>
      requests.where((r) => r.path == path).toList();
  Map<String, dynamic> body(http.BaseRequest request) =>
      request is http.Request && request.body.isNotEmpty
      ? Map<String, dynamic>.from(jsonDecode(request.body) as Map)
      : {};
  http.StreamedResponse json(Object value, {int status = 200}) =>
      http.StreamedResponse(
        Stream.value(utf8.encode(jsonEncode(value))),
        status,
        headers: {'content-type': 'application/json'},
      );
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    requests.add(RecordedAppRequest(request.url.path, body(request)));
    final path = request.url.path;
    if (path.endsWith('/apps/connect')) return json({'root': root});
    if (path == '/session/capabilities') {
      return json({'developerMode': developerMode});
    }
    if (path == '/identity/brains') {
      return request.method == 'POST'
          ? json({'brainId': 'created-brain'})
          : json(
              allowed
                  ? [
                      {'id': 'circle-test', 'kind': 'circle'},
                    ]
                  : [],
            );
    }
    if (path == '/connectors') return json(<Object>[]);
    final brain = request.url.pathSegments.length > 1
        ? request.url.pathSegments[1]
        : brainId;
    final navigation = paths.putIfAbsent(
      brain,
      () => [brain, '$brain/apps/ino'],
    );
    if (path.endsWith('/show')) {
      if (failOpen) return json({'error': 'Unavailable'}, status: 503);
      navigation.add(body(request)['app'] as String);
      revision++;
    }
    if (path.endsWith('/home')) {
      navigation.removeRange(1, navigation.length);
      revision++;
    }
    if (path.endsWith('/back') && navigation.length > 1) {
      navigation.removeLast();
      revision++;
    }
    if (path.endsWith('/close')) {
      navigation.remove(body(request)['app']);
      revision++;
    }
    if (path.contains('/ui/scenes/')) {
      if (path.endsWith('/report')) return json({});
      final focus = navigation.last;
      final nodes = <Object>[
        for (final app in {
          'ino',
          'assistant',
          'files',
          'settings',
          if (focus.contains('/apps/')) focus.split('/apps/').last,
        })
          {
            'id': '$brain/apps/$app',
            'type': 'sphere',
            'transform': {
              'x': 0,
              'y': 0,
              'scale': focus == '$brain/apps/$app' ? 2.2 : 1,
            },
            'inputs': ['tap'],
            'parameters': {
              'icon': 'folder',
              'title': app,
              'neuron': '$brain/apps/$app',
              'focused': '${focus == '$brain/apps/$app'}',
              'state': 'ready',
              if (focus == '$brain/apps/$app')
                'main': jsonEncode({
                  'kind': 'surface',
                  'name': '$brain/apps/$app/surface',
                }),
              if (focus == '$brain/apps/$app')
                'toolbar': jsonEncode(
                  app == 'ino' || app == 'assistant'
                      ? (assistantView['manifest'] as Map)['toolbar']
                      : [],
                ),
            },
          },
      ];
      return json({
        'name': '$brain/space',
        'revision': revision + 1,
        'definition': {
          'nodes': nodes,
          'camera': {'x': 0, 'y': 0, 'zoom': 1},
        },
      });
    }
    if (request.url.queryParameters['kind'] == 'surface') {
      renderedSurface = request.url.queryParameters['name'];
    }
    if (path.endsWith('/apps/assistant') || path.endsWith('/apps/ino')) {
      return json(assistantView);
    }
    if (path.endsWith('/connections')) return json(<Object>[]);
    if (path.endsWith('/csharp')) return json({'files': <Object>[]});
    final response = applicationResponse(request);
    if (response != null) return json(response);
    return json({
      'revision': revision,
      'path': navigation,
      'open': navigation.skip(1).toList(),
    });
  }
}

class ApplicationFixture {
  ApplicationFixture._(
    this.tester,
    this.server,
    this.store,
    this.client,
    this.link,
  );
  final WidgetTester tester;
  final ApplicationServer server;
  final WorkspaceStore store;
  final IntoChatOSUiClient client;
  final RecordingLink link;
  String get brainId => client.defaultBrainId!;
  String? get renderedSurface => server.renderedSurface;
  SpaceController get space =>
      tester.widget<NeuronSpace>(find.byType(NeuronSpace)).controller;
  static Future<ApplicationFixture> start(
    WidgetTester tester, {
    ApplicationServer? server,
    Future<void> Function()? onSwitchAccount,
  }) async {
    final api = server ?? ApplicationServer();
    final store = WorkspaceStore();
    final link = RecordingLink();
    final client =
        IntoChatOSUiClient(baseUri: Uri.parse('http://test'), httpClient: api)
          ..defaultBrainId = api.brainId
          ..link = link;
    await tester.pumpWidget(
      WorkspaceApp(
        store: store,
        programmingClient: client,
        onSwitchAccount: onSwitchAccount,
      ),
    );
    await tester.pumpAndSettle();
    return ApplicationFixture._(tester, api, store, client, link);
  }

  Future<void> openApp(String name) async {
    await space.show('$brainId/apps/$name');
    await tester.pumpAndSettle();
  }

  Future<void> menu(String label) async {
    await tester.tap(find.byTooltip('Profile'));
    await tester.pumpAndSettle();
    await tester.tap(find.text(label));
    await tester.pumpAndSettle();
  }

  Future<void> dispose() async {
    await tester.pumpWidget(const SizedBox());
    await server.events.close();
    client.close();
    store.dispose();
  }
}
