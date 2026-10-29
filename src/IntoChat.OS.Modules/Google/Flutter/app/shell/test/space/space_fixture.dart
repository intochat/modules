import 'dart:async';
import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:http/http.dart' as http;

class SpaceClient extends http.BaseClient {
  late final client = IntoChatOSUiClient(
    baseUri: Uri.parse('http://test'),
    httpClient: this,
  )..defaultBrainId = 'brain';
  WorkspaceSnapshot workspace = WorkspaceSnapshot(
    revision: 0,
    path: ['brain', 'brain/apps/ino'],
    open: ['brain/apps/ino'],
  );
  final changes = StreamController<void>.broadcast();
  final calls = <({String command, String id, int revision, String? key})>[];
  int reads = 0;
  bool conflict = false, failCommands = false, loseNextResponse = false;
  final applied = <String, WorkspaceSnapshot>{};
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final command = request.url.pathSegments.last;
    if (request.method == 'GET') {
      reads++;
      return response();
    }
    final body = jsonDecode((request as http.Request).body) as Map;
    final id = body['operationId'] as String,
        revision = body['expectedRevision'] as int;
    final key = body['app'] as String?;
    calls.add((command: command, id: id, revision: revision, key: key));
    if (applied.containsKey(id)) {
      workspace = applied[id]!;
      return response();
    }
    if (failCommands) {
      return http.StreamedResponse(Stream.value(utf8.encode('{}')), 503);
    }
    if (conflict) {
      conflict = false;
      workspace = WorkspaceSnapshot(
        revision: 7,
        path: workspace.path,
        open: workspace.open,
      );
      return http.StreamedResponse(
        Stream.value(utf8.encode('{"error":"conflict","currentRevision":7}')),
        409,
      );
    }
    final path = switch (command) {
      'show' => [...workspace.path, key!],
      'back' =>
        workspace.path.length > 1
            ? workspace.path.sublist(0, workspace.path.length - 1)
            : workspace.path,
      'home' => ['brain'],
      'close' => workspace.path.where((k) => k != key).toList(),
      _ => workspace.path,
    };
    workspace = WorkspaceSnapshot(
      revision: workspace.revision + 1,
      path: path,
      open: workspace.open,
    );
    applied[id] = workspace;
    if (loseNextResponse) {
      loseNextResponse = false;
      return http.StreamedResponse(Stream.value(utf8.encode('{}')), 503);
    }
    return response();
  }

  http.StreamedResponse response() => http.StreamedResponse(
    Stream.value(
      utf8.encode(
        jsonEncode({
          'revision': workspace.revision,
          'path': workspace.path,
          'open': workspace.open,
        }),
      ),
    ),
    200,
  );
}

class SpaceScenes extends SceneClient {
  SpaceScenes(this.ui)
    : super(http.Client(), Uri.parse('http://test'), 'brain');
  final SpaceClient ui;
  int revision = 1;
  String? error;
  bool side = false, failRead = false;
  String icon = 'folder';
  List<Map<String, dynamic>> toolbar = [];
  final reports = <String>[];
  Future<void> Function()? beforeRead;
  @override
  Future<SceneState> read(String sceneKey) async {
    await beforeRead?.call();
    if (failRead) throw StateError('scene unavailable');
    return scene();
  }

  SceneState scene() {
    final focused = ui.workspace.path.last;
    return SceneState(
      name: 'brain/space',
      revision: revision,
      definition: SceneDefinition(
        camera: SceneCamera(x: focused == 'brain/apps/files' ? 320 : 0),
        nodes: [
          for (final key in {'brain/apps/ino', 'brain/apps/files', 'brain'})
            SceneNode(
              id: key,
              type: 'sphere',
              transform: SceneTransform(
                x: key == 'brain/apps/files' ? 320 : 0,
                scale: key == focused ? 2.2 : 1,
              ),
              inputs: ['tap'],
              parameters: {
                'icon': icon,
                if (key == focused) 'toolbar': jsonEncode(toolbar),
                'neuron': key,
                'focused': '${key == focused}',
                'title': key.split('/').last,
                'state': error == null ? 'ready' : 'error',
                'error': ?error,
                if (key == focused && key != 'brain')
                  'main': jsonEncode({'kind': 'surface', 'name': '$key/main'}),
                if (key == focused && side)
                  'side': jsonEncode({'kind': 'text', 'name': '$key/side'}),
              },
            ),
        ],
      ),
    );
  }

  @override
  Future<void> report(
    String sceneKey,
    String nodeId,
    String kind,
    double x,
    double y, {
    String? payload,
  }) async {
    reports.add(nodeId);
  }
}

Future<Map<String, dynamic>> loadSlot(String kind, String name) async =>
    kind == 'text'
    ? {'markdown': 'Side content'}
    : {
        'definition': {
          'mode': 'column',
          'children': [
            {'kind': 'text', 'name': '$name/content'},
          ],
        },
      };
