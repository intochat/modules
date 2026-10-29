import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter/src/generated/neurons.pb.dart' as pb;
import 'package:fixnum/fixnum.dart';
import 'package:grpc/grpc.dart' show GrpcError;
import 'package:test/test.dart';

void main() {
  test('an envelope decodes into a NeuronSignal with its JSON payload', () {
    final envelope = pb.SignalEnvelope(
      source: 'owner/apps/calculator/surface',
      type: 'SurfaceChanged',
      publisher: 'owner/apps/calculator/surface',
      sequence: Int64(3),
      payload: utf8.encode('{"name":"x","revision":3}'),
      serverTimeMs: Int64(1700000000000),
    );
    final signal = NeuronSignal.fromEnvelope(envelope);
    expect(signal.type, 'SurfaceChanged');
    expect(signal.sequence, 3);
    expect(signal.payload['revision'], 3);
    expect(signal.serverTimeMs, 1700000000000);
  });

  test('a call request carries the arguments as a JSON array', () {
    final request = NeuronChannel.callRequest('owner', 'A.IB', 'k', 'Read', [
      1,
      'two',
    ]);
    expect(utf8.decode(request.arguments), '[1,"two"]');
    expect(request.brainId, 'owner');
  });

  test('a call carries the default 15 s deadline unless it names one', () {
    final channel = NeuronChannel(Uri.parse('http://127.0.0.1:1'));
    addTearDown(channel.shutdown);
    expect(NeuronChannel.defaultCallTimeout, const Duration(seconds: 15));
    expect(channel.callOptions().timeout, const Duration(seconds: 15));
    expect(
      channel.callOptions(timeout: const Duration(seconds: 2)).timeout,
      const Duration(seconds: 2),
    );
  });

  group('NeuronWatchMux', () {
    const a = WatchSourceSpec('C.IA', 'owner/a', ['AChanged']);
    const b = WatchSourceSpec('C.IB', 'owner/b', ['BChanged']);

    test('node watches share one Watch stream', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(() => link, 'owner', debounce: Duration.zero);
      addTearDown(mux.close);
      final seenA = <NeuronSignal>[], seenB = <NeuronSignal>[];
      final subA = mux.watch(a).listen(seenA.add);
      final subB = mux.watch(b).listen(seenB.add);
      await pump();
      expect(link.opened, hasLength(1));
      expect(link.opened.single.map((s) => s.key), ['owner/a', 'owner/b']);
      expect(mux.openStreams, 1);
      link.emit(signal('owner/b', 'BChanged'));
      link.emit(signal('owner/a', 'AChanged'));
      link.emit(signal('owner/a', 'Other'));
      await pump();
      expect(seenA.map((s) => s.type), ['AChanged']);
      expect(seenB.map((s) => s.type), ['BChanged']);
      await subB.cancel();
      await pump();
      expect(
        link.opened,
        hasLength(1),
        reason: 'a leaving source reopens nothing',
      );
      await subA.cancel();
      await pump();
      expect(mux.openStreams, 0);
    });

    test('a reopen drops replays already delivered', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(() => link, 'owner', debounce: Duration.zero);
      addTearDown(mux.close);
      final seen = <NeuronSignal>[];
      mux.watch(a).listen(seen.add);
      await pump();
      link.emit(signal('owner/a', 'AChanged', sequence: 7));
      await pump();
      mux.watch(b).listen((_) {});
      await pump();
      expect(link.opened, hasLength(2));
      link.emit(signal('owner/a', 'AChanged', sequence: 7));
      link.emit(signal('owner/a', 'AChanged'));
      await pump();
      expect(seen.map((s) => s.sequence), [7, 0]);
    });

    test('a broken stream reopens after the retry pause', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(
        () => link,
        'owner',
        debounce: Duration.zero,
        retryAfter: const Duration(milliseconds: 10),
      );
      addTearDown(mux.close);
      mux.watch(a).listen((_) {});
      await pump();
      link.fail();
      await Future<void>.delayed(const Duration(milliseconds: 40));
      expect(link.opened, hasLength(2));
    });

    test('retries back off exponentially with jitter up to 30 s', () {
      final mux = NeuronWatchMux(() => FakeLink(), 'owner', random: Random(1));
      final delays = [for (var i = 0; i < 8; i++) mux.backoffFor(i)];
      expect(delays.first.inMilliseconds, inInclusiveRange(1500, 2500));
      expect(delays[2].inMilliseconds, inInclusiveRange(6000, 10000));
      for (final delay in delays) {
        expect(delay, lessThanOrEqualTo(const Duration(seconds: 30)));
      }
      expect(delays.last.inMilliseconds, greaterThanOrEqualTo(22500));
    });

    test('consecutive breaks wait longer each time', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(
        () => link,
        'owner',
        debounce: Duration.zero,
        retryAfter: const Duration(milliseconds: 20),
        random: Random(1),
      );
      addTearDown(mux.close);
      mux.watch(a).listen((_) {});
      await pump();
      link.fail();
      await Future<void>.delayed(const Duration(milliseconds: 40));
      expect(link.opened, hasLength(2));
      link.fail();
      await Future<void>.delayed(const Duration(milliseconds: 20));
      expect(link.opened, hasLength(2), reason: 'second retry waits ~40 ms');
      await Future<void>.delayed(const Duration(milliseconds: 60));
      expect(link.opened, hasLength(3));
    });

    test(
      'a refusal no retry can fix reaches the listeners and stops',
      () async {
        for (final error in [
          GrpcError.permissionDenied('not yours'),
          GrpcError.unauthenticated('sign in'),
          GrpcError.invalidArgument('too many'),
        ]) {
          final link = FakeLink();
          final mux = NeuronWatchMux(
            () => link,
            'owner',
            debounce: Duration.zero,
            retryAfter: const Duration(milliseconds: 5),
          );
          final errors = <Object>[];
          var done = false;
          mux
              .watch(a)
              .listen((_) {}, onError: errors.add, onDone: () => done = true);
          await pump();
          link.fail(error);
          await Future<void>.delayed(const Duration(milliseconds: 40));
          expect(errors.single, same(error));
          expect(done, isTrue);
          expect(link.opened, hasLength(1));
          await mux.close();
        }
      },
    );

    test(
      'a membership change keeps the old stream until the new one yields',
      () async {
        final link = FakeLink();
        final mux = NeuronWatchMux(
          () => link,
          'owner',
          debounce: Duration.zero,
        );
        addTearDown(mux.close);
        final seen = <NeuronSignal>[];
        mux.watch(a).listen(seen.add);
        await pump();
        mux.watch(b).listen((_) {});
        await pump();
        expect(link.opened, hasLength(2));
        // Published while the new stream has not yet yielded: only the old stream carries it.
        link.emitOn(0, signal('owner/a', 'AChanged'));
        await pump();
        expect(seen, hasLength(1));
        link.emitOn(1, signal('owner/b', 'BChanged'));
        await pump();
        expect(
          link.live,
          1,
          reason: 'the old stream retires once the new one yields',
        );
      },
    );

    test('a change reopens only the chunk it touches', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(() => link, 'owner', debounce: Duration.zero);
      addTearDown(mux.close);
      for (var i = 0; i < 64; i++) {
        mux
            .watch(WatchSourceSpec('C.I', 'owner/n$i', const ['X']))
            .listen((_) {});
      }
      await pump();
      mux.watch(a).listen((_) {});
      await pump();
      expect(link.opened.map((o) => o.length), [64, 1]);
    });

    test('more than 64 source signals split across streams', () async {
      final link = FakeLink();
      final mux = NeuronWatchMux(() => link, 'owner', debounce: Duration.zero);
      addTearDown(mux.close);
      for (var i = 0; i < 65; i++) {
        mux
            .watch(WatchSourceSpec('C.I', 'owner/n$i', const ['X']))
            .listen((_) {});
      }
      await pump();
      expect(link.opened.map((o) => o.length), [64, 1]);
    });
  });
}

Future<void> pump() => Future<void>.delayed(const Duration(milliseconds: 5));

NeuronSignal signal(String source, String type, {int sequence = 0}) =>
    NeuronSignal(
      source: source,
      type: type,
      publisher: source,
      sequence: sequence,
      payload: const {},
      serverTimeMs: 0,
    );

final class FakeLink implements NeuronLink {
  final opened = <List<WatchSourceSpec>>[];
  final _streams = <StreamController<NeuronSignal>>[];

  void emit(NeuronSignal signal) {
    for (final stream in _streams) {
      if (!stream.isClosed) stream.add(signal);
    }
  }

  void fail([Object? error]) =>
      _streams.last.addError(error ?? StateError('broken'));

  int get live => _streams.length;

  final _all = <StreamController<NeuronSignal>>[];
  void emitOn(int index, NeuronSignal signal) {
    final stream = _all[index];
    if (!stream.isClosed) stream.add(signal);
  }

  @override
  Stream<NeuronSignal> watch(String brainId, List<WatchSourceSpec> sources) {
    opened.add(List.of(sources));
    final controller = StreamController<NeuronSignal>();
    controller.onCancel = () {
      _streams.remove(controller);
      return controller.close();
    };
    _streams.add(controller);
    _all.add(controller);
    return controller.stream;
  }

  @override
  Future<Map<String, dynamic>?> call(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments,
  ) async => null;

  @override
  Future<void> shutdown() async {}
}
