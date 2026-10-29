import 'dart:convert' show jsonDecode, jsonEncode;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:intochat_os_flutter_shell/workspace/workspace_app.dart'
    show settingsActivation;

import '../application_fixture.dart';

void main() {
  // An older button (an R4 Ino card or a 0.2 Desktop) still hands the shell {kind: settings, tab}: the shell
  // translates it to the Settings app's workspace activation, which the root's receiver opens on that tab.
  testWidgets('legacy_settings_activation_opens_the_settings_app', (
    tester,
  ) async {
    final server = _LegacySettingsServer();
    final fixture = await ApplicationFixture.start(tester, server: server);
    await tester.tap(find.text('Settings'));
    await tester.pumpAndSettle();
    final requests = server.requestsTo(
      '/brains/brain-1/apps/workspace/request',
    );
    expect(requests, hasLength(1));
    expect(requests.single.body['operation'], 'activate');
    expect(jsonDecode(requests.single.body['input'] as String), {
      'kind': 'workspace',
      'brainId': 'brain-1',
      'application': 'settings',
      'operation': 'open',
      'input': 'keys',
    });
    expect(server.requestsTo('/brains/brain-1/apps/settings/request'), isEmpty);
    await fixture.dispose();
  });

  test(
    'a legacy settings activation without a tab leaves the tab to the app',
    () {
      expect(settingsActivation({'kind': 'settings'}), {
        'kind': 'workspace',
        'application': 'settings',
        'operation': 'open',
        'input': '',
      });
    },
  );
}

class _LegacySettingsServer extends ApplicationServer {
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (request.url.path.endsWith('/apps/connect')) {
      requests.add(RecordedAppRequest(request.url.path, body(request)));
      return json({
        'root': {'kind': 'button', 'name': 'brain-1/apps/desktop/settings'},
        'activationApp': 'workspace',
      });
    }
    if (request.url.path.endsWith('/apps/node')) {
      return json({
        'label': 'Settings',
        'enabled': true,
        'activation': jsonEncode({
          'kind': 'settings',
          'brainId': 'brain-1',
          'tab': 'keys',
        }),
      });
    }
    return super.send(request);
  }
}
