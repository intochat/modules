import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/workspace/home/infrastructure_strip.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('health is green when healthy or connected, amber while unknown, red when down', () {
    expect(
      infrastructureHealth('Running', 'Healthy'),
      InfrastructureHealth.green,
    );
    expect(infrastructureHealth('Connected', null), InfrastructureHealth.green);
    expect(infrastructureHealth('Starting', null), InfrastructureHealth.amber);
    expect(infrastructureHealth('Running', null), InfrastructureHealth.amber);
    expect(infrastructureHealth('Unknown', null), InfrastructureHealth.amber);
    expect(
      infrastructureHealth('Running', 'Unhealthy'),
      InfrastructureHealth.red,
    );
    expect(infrastructureHealth('Exited', null), InfrastructureHealth.red);
    expect(
      infrastructureHealth('FailedToStart', null),
      InfrastructureHealth.red,
    );
    expect(
      infrastructureHealth('Disconnected', null),
      InfrastructureHealth.red,
    );
    expect(infrastructureHealth('Failing', null), InfrastructureHealth.red);
  });

  test('an Orleans silo status decides its color', () {
    expect(infrastructureHealth('Active', null), InfrastructureHealth.green);
    expect(infrastructureHealth('Dead', null), InfrastructureHealth.red);
    expect(infrastructureHealth('Stopping', null), InfrastructureHealth.red);
    expect(
      infrastructureHealth('ShuttingDown', null),
      InfrastructureHealth.red,
    );
    expect(infrastructureHealth('Joining', null), InfrastructureHealth.amber);
    expect(infrastructureHealth('Created', null), InfrastructureHealth.amber);
  });

  DeploymentChanged cluster(List<SiloState> silos, {String stage = 'steady'}) =>
      DeploymentChanged(stage: stage, sha: 'abc1234567', silos: silos);

  testWidgets(
    'the strip shows a chip per silo, follows the signal without polling, and a tap names the silo',
    (tester) async {
      final signals = StreamController<DeploymentChanged>();
      final tapped = <String>[];
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: InfrastructureStrip(
              watch: () => signals.stream,
              onTap: tapped.add,
            ),
          ),
        ),
      );
      expect(find.byType(ActionChip), findsNothing);

      signals.add(
        cluster([
          const SiloState(
            id: 'silo-1',
            sha: 'a',
            state: 'Running',
            health: 'Healthy',
          ),
          const SiloState(id: 'silo-2', sha: 'a', state: 'Starting'),
        ], stage: 'Steady'),
      );
      await tester.pump();
      expect(find.byKey(const ValueKey('cluster-stage')), findsNothing);
      CircleAvatar dot(String id) => tester.widget(
        find.descendant(
          of: find.byKey(ValueKey('silo-$id')),
          matching: find.byType(CircleAvatar),
        ),
      );
      expect(dot('silo-1').backgroundColor, Colors.green);
      expect(dot('silo-2').backgroundColor, Colors.amber);

      signals.add(
        cluster([
          const SiloState(id: 'silo-1', sha: 'a', state: 'Exited'),
        ], stage: 'rolling'),
      );
      await tester.pump();
      await tester.pumpAndSettle();
      expect(dot('silo-1').backgroundColor, Colors.red);
      expect(find.byKey(const ValueKey('silo-silo-2')), findsNothing);
      expect(find.byKey(const ValueKey('cluster-stage')), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('silo-silo-1')));
      expect(tapped, ['silo-1']);
      await tester.pumpWidget(const SizedBox());
      expect(signals.hasListener, isFalse);
      unawaited(signals.close());
    },
  );

  testWidgets('a state change animates unless motion is reduced', (
    tester,
  ) async {
    Future<int> avatarsDuringSwitch({required bool reduced}) async {
      final signals = StreamController<DeploymentChanged>();
      await tester.pumpWidget(
        MediaQuery(
          data: MediaQueryData(disableAnimations: reduced),
          child: MaterialApp(
            home: Scaffold(
              body: InfrastructureStrip(watch: () => signals.stream),
            ),
          ),
        ),
      );
      signals.add(
        cluster([const SiloState(id: 's', sha: 'a', state: 'Starting')]),
      );
      await tester.pump();
      signals.add(
        cluster([
          const SiloState(
            id: 's',
            sha: 'a',
            state: 'Running',
            health: 'Healthy',
          ),
        ]),
      );
      await tester.pump(const Duration(milliseconds: 100));
      final count = find.byType(CircleAvatar).evaluate().length;
      await tester.pumpWidget(const SizedBox());
      unawaited(signals.close());
      return count;
    }

    expect(await avatarsDuringSwitch(reduced: false), 2);
    expect(await avatarsDuringSwitch(reduced: true), 1);
  });

  testWidgets('a failing feed says so', (tester) async {
    final signals = StreamController<DeploymentChanged>();
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: InfrastructureStrip(watch: () => signals.stream)),
      ),
    );
    signals.addError(StateError('kernel unreachable'));
    await tester.pump();
    expect(find.byKey(const ValueKey('infrastructure-error')), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    unawaited(signals.close());
  });
}
