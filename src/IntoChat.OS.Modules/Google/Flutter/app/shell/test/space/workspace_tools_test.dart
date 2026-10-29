import 'package:intochat_os_flutter_shell/workspace/compute_usage_panel.dart';
import 'package:intochat_os_flutter_shell/workspace/workspace_app.dart';
import 'package:intochat_os_flutter_shell/connections/connect_window.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../application_fixture.dart';

void main() {
  testWidgets(
    'settings opens the Settings app and profile keeps preferences reachable',
    (tester) async {
      final fixture = await ApplicationFixture.start(tester);
      const path = '/brains/brain-1/apps/settings/request';
      await tester.tap(find.byTooltip('Settings'));
      await tester.pumpAndSettle();
      expect(fixture.server.requestsTo(path).single.body, {
        'operation': 'open',
        'input': '',
      });
      await fixture.menu('Workspace configuration');
      expect(fixture.server.requestsTo(path), hasLength(2));
      await fixture.dispose();
    },
  );
  test('a settings activation opens the Settings app on its tab', () {
    expect(
      settingsActivation({'kind': 'settings', 'brainId': 'b', 'tab': 'keys'}),
      {
        'kind': 'workspace',
        'brainId': 'b',
        'application': 'settings',
        'operation': 'open',
        'input': 'keys',
      },
    );
    expect(settingsActivation({'kind': 'settings'}), {
      'kind': 'workspace',
      'application': 'settings',
      'operation': 'open',
      'input': '',
    });
  });
  testWidgets(
    'connections and compute remain reachable from the toolbar menu',
    (tester) async {
      final fixture = await ApplicationFixture.start(tester);
      await fixture.menu('Connections');
      expect(find.byType(ConnectWindow), findsOneWidget);
      Navigator.of(tester.element(find.byType(ConnectWindow))).pop();
      await tester.pumpAndSettle();
      await fixture.menu('Compute');
      expect(find.byType(ComputeUsagePanel), findsOneWidget);
      await fixture.dispose();
    },
  );
  testWidgets('switch account invokes the authenticated shell callback', (
    tester,
  ) async {
    var switches = 0;
    final fixture = await ApplicationFixture.start(
      tester,
      onSwitchAccount: () async {
        switches++;
      },
    );
    await fixture.menu('Switch account');
    expect(switches, 1);
    await fixture.dispose();
  });
  testWidgets('new workspace uses the server allocated brain', (tester) async {
    final fixture = await ApplicationFixture.start(tester);
    await fixture.menu('New workspace');
    expect(fixture.space.brain, 'created-brain');
    expect(fixture.server.requestsTo('/identity/brains').single.body, isEmpty);
    await fixture.dispose();
  });
}
