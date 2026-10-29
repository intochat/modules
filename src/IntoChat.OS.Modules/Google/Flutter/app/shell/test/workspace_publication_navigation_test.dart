import 'package:intochat_os_ui/intochat_os_ui.dart' show kitToastLifetime;
import 'package:flutter_test/flutter_test.dart';

import 'application_fixture.dart';

import 'package:intochat_os_flutter_shell/space/neuron_space.dart';

void main() {
  testWidgets(
    'failed destination Show preserves the source controller and path',
    (tester) async {
      final f = await ApplicationFixture.start(tester);
      final source = f.space;
      final original = source.workspace!.path.toList();
      f.server.failOpen = true;
      tester.widget<NeuronSpace>(find.byType(NeuronSpace)).onActivate!({
        'brainId': 'circle-test',
        'application': 'notes',
      });
      await tester.pumpAndSettle();
      expect(f.space, same(source));
      expect(f.space.workspace!.path, original);
      expect(f.space.view.value.focused, '${f.brainId}/apps/ino');
      expect(find.textContaining('Could not navigate'), findsOneWidget);
      await tester.pump(kitToastLifetime);
      f.server.failOpen = false;
      await source.home();
      await tester.pumpAndSettle();
      expect(source.view.value.focused, f.brainId);
      await f.dispose();
    },
  );
  for (final allowed in [false, true]) {
    testWidgets(
      'publication navigation checks destination membership $allowed',
      (tester) async {
        final f = await ApplicationFixture.start(
          tester,
          server: ApplicationServer()..allowed = allowed,
        );
        tester.widget<NeuronSpace>(find.byType(NeuronSpace)).onActivate!({
          'kind': 'workspace',
          'brainId': 'circle-test',
          'application': 'notes',
          'operation': 'open',
          'input': 'shared',
        });
        await tester.pumpAndSettle();
        if (allowed) {
          expect(f.space.brain, 'circle-test');
          expect(f.space.view.value.focused, 'circle-test/apps/notes');
          expect(
            f.server
                .requestsTo('/brains/circle-test/apps/notes/request')
                .single
                .body,
            {'operation': 'open', 'input': 'shared'},
          );
        } else {
          expect(f.space.brain, f.brainId);
          expect(
            f.server.requests.any(
              (r) => r.path.startsWith('/brains/circle-test/'),
            ),
            isFalse,
          );
          expect(find.textContaining('not a member'), findsOneWidget);
          await tester.pump(kitToastLifetime);
        }
        await f.dispose();
      },
    );
  }
  // A settings activation names a brain: membership is checked before switching, as for any other.
  for (final allowed in [false, true]) {
    testWidgets('settings activation checks brain membership $allowed', (
      tester,
    ) async {
      final f = await ApplicationFixture.start(
        tester,
        server: ApplicationServer()..allowed = allowed,
      );
      tester.widget<NeuronSpace>(find.byType(NeuronSpace)).onActivate!({
        'kind': 'settings',
        'brainId': 'circle-test',
        'tab': 'keys',
      });
      await tester.pumpAndSettle();
      final settings = f.server.requestsTo(
        '/brains/circle-test/apps/settings/request',
      );
      if (allowed) {
        expect(f.space.brain, 'circle-test');
        expect(settings.single.body, {'operation': 'open', 'input': 'keys'});
      } else {
        expect(f.space.brain, f.brainId);
        expect(
          f.server.requests.any(
            (r) => r.path.startsWith('/brains/circle-test/'),
          ),
          isFalse,
        );
        expect(find.textContaining('not a member'), findsOneWidget);
        await tester.pump(kitToastLifetime);
      }
      await f.dispose();
    });
  }
  // Install, update and remove name a brain: membership is checked first; the space does not switch.
  for (final allowed in [false, true]) {
    testWidgets('app activations check brain membership $allowed', (
      tester,
    ) async {
      final f = await ApplicationFixture.start(
        tester,
        server: ApplicationServer()..allowed = allowed,
      );
      final request = {
        'ref': {'publisher': 'os', 'name': 'notes'},
      };
      tester.widget<NeuronSpace>(find.byType(NeuronSpace)).onActivate!({
        'kind': 'install',
        'brainId': 'circle-test',
        'request': request,
      });
      await tester.pumpAndSettle();
      final installs = f.server.requestsTo('/brains/circle-test/apps/install');
      expect(f.space.brain, f.brainId);
      if (allowed) {
        expect(installs.single.body, request);
        expect(find.text('Installed.'), findsOneWidget);
      } else {
        expect(
          f.server.requests.any(
            (r) => r.path.startsWith('/brains/circle-test/'),
          ),
          isFalse,
        );
        expect(find.textContaining('not a member'), findsOneWidget);
      }
      await tester.pump(kitToastLifetime);
      await f.dispose();
    });
  }
}
