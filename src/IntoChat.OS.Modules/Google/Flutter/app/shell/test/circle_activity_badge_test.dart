import 'dart:async';
import 'dart:io';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/workspace/home/circle_activity_badge.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget host(
  CircleActivityFeed feed, {
  bool homeShown = true,
  bool reduced = false,
}) => MaterialApp(
  home: MediaQuery(
    data: MediaQueryData(disableAnimations: reduced),
    child: Scaffold(
      body: Center(
        child: CircleActivityBadge(
          feed: feed,
          homeShown: homeShown,
          child: const Icon(Icons.group, size: 48),
        ),
      ),
    ),
  ),
);

CircleActivity touch(
  String kind, [
  String circle = 'club',
  int? sequence,
  bool replay = false,
]) => CircleActivity(
  circle: circle,
  kind: kind,
  sequence: sequence,
  replay: replay,
);

String? shown(WidgetTester tester) {
  final badge = find.byKey(const ValueKey('circle-badge'));
  final scale = tester.widget<AnimatedScale>(
    find.ancestor(of: badge, matching: find.byType(AnimatedScale)),
  );
  if (scale.scale == 0) return null;
  return tester
      .widget<Text>(find.descendant(of: badge, matching: find.byType(Text)))
      .data;
}

void main() {
  late StreamController<CircleActivity> live;
  late List<StreamController<CircleActivity>> connections;
  late List<Completer<void>> pauses;

  CircleActivityFeed feed() {
    connections = [];
    pauses = [];
    return CircleActivityFeed(
      () {
        live = StreamController<CircleActivity>();
        connections.add(live);
        return live.stream;
      },
      pause: (_) {
        final pause = Completer<void>();
        pauses.add(pause);
        return pause.future;
      },
    );
  }

  testWidgets('lines spoken in any circle are counted, changes are not', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    await tester.pumpWidget(host(f));
    expect(shown(tester), isNull);
    live
      ..add(touch('spoke'))
      ..add(touch('spoke', 'other'))
      ..add(touch('changed'));
    await tester.pumpAndSettle();
    expect(shown(tester), '2');
  });

  testWidgets('each circle keeps its own high-water mark', (tester) async {
    final f = feed();
    addTearDown(f.dispose);
    live
      ..add(touch('spoke', 'club', 5, true))
      ..add(touch('spoke', 'other', 2, true));
    await tester.pump();
    expect(f.count, 0);
    live
      ..add(touch('spoke', 'other', 3))
      ..add(touch('spoke', 'club', 5));
    await tester.pump();
    expect(f.count, 1);
  });

  testWidgets('the count survives the badge unmounting, then Home clears it', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.add(touch('spoke'));
    await tester.pump();
    expect(f.count, 1);
    await tester.pumpWidget(host(f));
    expect(shown(tester), '1');
    await tester.pumpWidget(const SizedBox());
    expect(f.count, 0);
    live.add(touch('spoke', 'b'));
    await tester.pump();
    expect(f.count, 1);
    await tester.pumpWidget(host(f));
    expect(shown(tester), '1');
  });

  testWidgets('hiding Home while mounted clears the count', (tester) async {
    final f = feed();
    addTearDown(f.dispose);
    await tester.pumpWidget(host(f));
    live.add(touch('spoke'));
    await tester.pump();
    await tester.pumpWidget(host(f, homeShown: false));
    await tester.pumpAndSettle();
    expect(shown(tester), isNull);
  });

  testWidgets('a cold-start replay seeds the mark and is not counted', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.add(touch('spoke', 'club', 5, true));
    await tester.pump();
    expect(f.count, 0);
    live.add(touch('spoke', 'club', 6));
    await tester.pump();
    expect(f.count, 1);
  });

  testWidgets('a first live touch with nothing retained is counted', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.add(touch('spoke', 'club', 1));
    await tester.pump();
    expect(f.count, 1);
  });

  testWidgets(
    'after a reconnect the replay is not counted but a new touch is, once',
    (tester) async {
      final f = feed();
      addTearDown(f.dispose);
      live.add(touch('spoke', 'club', 5, true));
      live.add(touch('spoke', 'club', 6));
      await tester.pump();
      expect(f.count, 1);
      await live.close();
      await tester.pump();
      expect(pauses, hasLength(1));
      pauses.single.complete();
      await tester.pump();
      expect(connections, hasLength(2));
      live.add(touch('spoke', 'club', 6, true));
      await tester.pump();
      expect(f.count, 1);
      live.add(touch('spoke', 'club', 7));
      await tester.pump();
      expect(f.count, 2);
    },
  );

  testWidgets(
    'a touch missed during the outage is counted once from the replay',
    (tester) async {
      final f = feed();
      addTearDown(f.dispose);
      live.add(touch('spoke', 'club', 5, true));
      await tester.pump();
      await live.close();
      await tester.pump();
      pauses.single.complete();
      await tester.pump();
      live.add(touch('spoke', 'club', 8, true));
      await tester.pump();
      expect(f.count, 1);
    },
  );

  testWidgets('a touch missed after an empty first connection is counted', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.add(const CircleActivity.ready('club'));
    await live.close();
    await tester.pump();
    pauses.single.complete();
    await tester.pump();
    live.add(touch('spoke', 'club', 3, true));
    await tester.pump();
    expect(f.count, 1);
  });

  testWidgets('a refused connect with a socket error does not seed the feed', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.addError(const SocketException('Connection refused'));
    await tester.pump();
    pauses.single.complete();
    await tester.pump();
    live.add(touch('spoke', 'club', 3, true));
    await tester.pump();
    expect(f.count, 0);
  });

  testWidgets('ready seeds the feed without counting anything', (tester) async {
    final f = feed();
    addTearDown(f.dispose);
    live.add(const CircleActivity.ready('club'));
    await tester.pump();
    expect(f.count, 0);
    live.add(touch('spoke', 'club', 1, true));
    await tester.pump();
    expect(f.count, 1);
  });

  testWidgets(
    'a replay after an empty ready connection and an outage is counted once',
    (tester) async {
      final f = feed();
      addTearDown(f.dispose);
      live.add(const CircleActivity.ready('club'));
      await tester.pump();
      live.addError(const SocketException('reset'));
      await tester.pump();
      pauses.single.complete();
      await tester.pump();
      live
        ..add(touch('spoke', 'club', 4, true))
        ..add(const CircleActivity.ready('club'));
      await tester.pump();
      expect(f.count, 1);
      await live.close();
      await tester.pump();
      pauses.last.complete();
      await tester.pump();
      live
        ..add(touch('spoke', 'club', 4, true))
        ..add(const CircleActivity.ready('club'));
      await tester.pump();
      expect(f.count, 1);
    },
  );

  testWidgets('ready resets the backoff', (tester) async {
    final lengths = <Duration>[];
    late StreamController<CircleActivity> current;
    final f = CircleActivityFeed(
      () => (current = StreamController<CircleActivity>()).stream,
      pause: (d) {
        lengths.add(d);
        return Future.value();
      },
    );
    addTearDown(f.dispose);
    current.addError(const SocketException('refused'));
    await tester.pump();
    current.addError(const SocketException('refused'));
    await tester.pump();
    current.add(const CircleActivity.ready('club'));
    await tester.pump();
    current.addError(const SocketException('reset'));
    await tester.pump();
    expect(lengths, [
      const Duration(seconds: 1),
      const Duration(seconds: 2),
      const Duration(seconds: 1),
    ]);
  });

  testWidgets('a connection that never opened does not seed the feed', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    live.addError(TableRequestException(503, 'down'));
    await tester.pump();
    pauses.single.complete();
    await tester.pump();
    live.add(touch('spoke', 'club', 3, true));
    await tester.pump();
    expect(f.count, 0);
  });

  testWidgets('a source with no sequence counts every touch', (tester) async {
    final f = feed();
    addTearDown(f.dispose);
    live
      ..add(touch('spoke'))
      ..add(touch('spoke'));
    await tester.pump();
    expect(f.count, 2);
  });

  testWidgets('a watch that throws is retried with growing pauses', (
    tester,
  ) async {
    var calls = 0;
    final lengths = <Duration>[];
    final f = CircleActivityFeed(
      () {
        calls++;
        if (calls < 3) throw StateError('no client');
        return StreamController<CircleActivity>().stream;
      },
      pause: (d) {
        lengths.add(d);
        return Future.value();
      },
    );
    addTearDown(f.dispose);
    await tester.pump();
    expect(calls, greaterThanOrEqualTo(3));
    expect(lengths.take(2), [
      const Duration(seconds: 1),
      const Duration(seconds: 2),
    ]);
  });

  testWidgets('a stream error also reconnects', (tester) async {
    final f = feed();
    addTearDown(f.dispose);
    live.addError('boom');
    await tester.pump();
    expect(pauses, hasLength(1));
    pauses.single.complete();
    await tester.pump();
    expect(connections, hasLength(2));
  });

  testWidgets('reduced motion shows the count without animating', (
    tester,
  ) async {
    final f = feed();
    addTearDown(f.dispose);
    await tester.pumpWidget(host(f, reduced: true));
    live.add(touch('spoke'));
    await tester.pump();
    await tester.pump();
    expect(shown(tester), '1');
  });

  test('a CircleSpoke envelope is a spoken line of its circle', () {
    final heard = CircleActivity.fromEnvelope(
      'circle-1',
      LiveEnvelope.fromJson({
        'type': 'CircleSpoke',
        'publisher': 'circles.chat/circle-1/chat',
        'payload': {
          'name': 'club',
          'speaker': 'ann',
          'text': 'hi',
          'sequence': 1,
        },
      }),
    );
    expect(
      (heard.circle, heard.kind, heard.ready),
      ('circle-1', 'spoke', false),
    );
    expect(
      CircleActivity.fromEnvelope(
        'circle-1',
        LiveEnvelope.fromJson({'type': 'ready'}),
      ).ready,
      isTrue,
    );
  });
}
