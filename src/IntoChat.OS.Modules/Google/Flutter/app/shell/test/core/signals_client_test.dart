import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('signals reads one page of the brain journal from a sequence', () async {
    final requests = <Uri>[];
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://brain.test'),
      httpClient: MockClient((request) async {
        requests.add(request.url);
        return http.Response(
          jsonEncode({
            'next': 7,
            'signals': [
              {
                'sequence': 7,
                'type': 'AppChanged',
                'publisher': 'apps/notes',
                'payload': {'Name': 'notes', 'Version': '1', 'Status': 'Live'},
              },
            ],
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
    );

    final page = await client.signals('b 1', since: 3, limit: 50);

    expect(requests.single.path, '/brains/b%201/signals');
    expect(requests.single.query, 'since=3&limit=50');
    expect(page.next, 7);
    expect(page.signals.single.type, 'AppChanged');
    expect(page.signals.single.field('name'), 'notes');
  });
}
