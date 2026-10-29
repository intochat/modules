import 'dart:async';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  _resubscribe();
  testWidgets('a watched view reloads on events and never on a timer', (
    tester,
  ) async {
    var loads = 0;
    final events = StreamController<void>.broadcast();
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'text',
            name: 'hello',
            load: (kind, name) async {
              loads++;
              return {
                'definition': {'markdown': 'load $loads'},
              };
            },
            watch: (kind, name) => events.stream,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(loads, 1);
    expect(find.textContaining('load 1'), findsOneWidget);

    await tester.pump(const Duration(seconds: 5));
    await tester.pump();
    expect(loads, 1, reason: 'no timer when watched');

    events.add(null);
    await tester.pumpAndSettle();
    expect(loads, 2);
    expect(find.textContaining('load 2'), findsOneWidget);

    events.add(null);
    events.add(null);
    await tester.pumpAndSettle();
    expect(loads, lessThanOrEqualTo(4));
    await events.close();
  });
}

void _resubscribe() {
  testWidgets('a watched view resubscribes after its stream ends', (
    tester,
  ) async {
    var loads = 0, subscriptions = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'text',
            name: 'hello',
            load: (kind, name) async {
              loads++;
              return {
                'definition': {'markdown': 'load $loads'},
              };
            },
            // Each subscription delivers one event and ends, as a proxy idle-close would.
            watch: (kind, name) {
              subscriptions++;
              return Stream<void>.fromIterable([null]);
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(subscriptions, 1);
    await tester.pump(const Duration(seconds: 3));
    await tester.pumpAndSettle();
    expect(
      subscriptions,
      greaterThanOrEqualTo(2),
      reason: 'a finished stream is reopened',
    );
    expect(loads, greaterThanOrEqualTo(2));
  });
}
