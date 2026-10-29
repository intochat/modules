import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    'Windows discovery and folder permissions use the selected brain',
    () async {
      final requests = <http.Request>[];
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          requests.add(request);
          return http.Response('{}', 200);
        }),
      );
      await client.workspaceWindowsRequest('brain-one', 'device');
      await client.workspaceWindowsRequest(
        'brain-two',
        'machine/roots/allow',
        body: {'path': 'C:/Samples'},
      );
      expect(requests[0].method, 'GET');
      expect(requests[0].url.path, '/brains/brain-one/windows/device');
      expect(requests[1].method, 'POST');
      expect(
        requests[1].url.path,
        '/brains/brain-two/windows/machine/roots/allow',
      );
      expect(jsonDecode(requests[1].body), {'path': 'C:/Samples'});
    },
  );
}
