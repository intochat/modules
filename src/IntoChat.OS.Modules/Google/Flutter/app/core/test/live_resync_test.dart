import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:http/http.dart' as http;
import 'package:test/test.dart';

class LiveServer extends http.BaseClient {
  final lives = <String>[];

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final path = request.url.path;
    if (path == '/identity/brains') {
      return _json([
        {'id': 'circle-1', 'kind': 'Circle'},
      ]);
    }
    if (path.endsWith('/live')) {
      lives.add(path);
      final events = lives.length == 1
          ? [
              _spoke(1, replay: true),
              {'type': 'ready'},
              {'type': 'resync'},
              _spoke(2),
            ]
          : [
              _spoke(3, replay: true),
              {'type': 'ready'},
            ];
      return http.StreamedResponse(
        Stream.value(
          utf8.encode(events.map((e) => 'data: ${jsonEncode(e)}\n\n').join()),
        ),
        200,
        headers: {'content-type': 'text/event-stream'},
      );
    }
    return http.StreamedResponse(const Stream.empty(), 404);
  }

  static Map<String, dynamic> _spoke(int sequence, {bool replay = false}) => {
    'type': 'CircleSpoke',
    'publisher': 'circle-1/chat',
    'sequence': sequence,
    if (replay) 'replay': true,
    'payload': <String, dynamic>{},
  };

  static http.StreamedResponse _json(Object value) => http.StreamedResponse(
    Stream.value(utf8.encode(jsonEncode(value))),
    200,
    headers: {'content-type': 'application/json'},
  );
}

void main() {
  test(
    'a resync reconnects the circle feed and replays current state',
    () async {
      final server = LiveServer();
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://localhost'),
        httpClient: server,
      );
      addTearDown(client.close);
      final watch = Stopwatch()..start();
      final activity = await client.watchCircleActivity().toList();
      expect(watch.elapsedMilliseconds, greaterThanOrEqualTo(50));
      expect(server.lives, hasLength(2));
      expect(
        activity.map((a) => a.ready ? 'ready' : '${a.kind}:${a.sequence}'),
        ['spoke:1', 'ready', 'spoke:3', 'ready'],
      );
    },
  );
}
