import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models/scene_models.dart';
import 'ui_client.dart';

/// The brain's Flame routes under `/brains/{brain}/ui/`: scenes.
// Not final, so the shell's tests can stand in a fake.
class SceneClient {
  SceneClient(this._http, this._base, this.workspace);
  final http.Client _http;
  final Uri _base;
  final String workspace;

  String _ui(String path) =>
      '/brains/${Uri.encodeComponent(workspace)}/ui/$path';

  String _scene(String sceneKey, [String suffix = '']) =>
      _ui('scenes/${Uri.encodeComponent(sceneKey)}$suffix');

  Future<SceneState> read(String sceneKey) async =>
      SceneState.fromJson(await _getJson(_scene(sceneKey)));

  Future<void> report(
    String sceneKey,
    String nodeId,
    String kind,
    double x,
    double y, {
    String? payload,
  }) => _post(_scene(sceneKey, '/report'), {
    'nodeId': nodeId,
    'kind': kind,
    'x': x,
    'y': y,
    'payload': payload,
  });

  Future<Map<String, dynamic>> _getJson(String path) async {
    final response = await _send('GET', path);
    return jsonDecode(response.body) as Map<String, dynamic>;
  }

  Future<void> _post(String path, Map<String, Object?> body) =>
      _send('POST', path, body);

  Future<http.Response> _send(
    String method,
    String path, [
    Map<String, Object?>? body,
  ]) async {
    final request = http.Request(method, _base.resolve(path));
    if (body != null) {
      request.headers['content-type'] = 'application/json';
      request.body = jsonEncode(body);
    }
    final response = await http.Response.fromStream(await _http.send(request));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw UiRequestException(
        method,
        path,
        response.statusCode,
        response.body,
      );
    }
    return response;
  }
}
