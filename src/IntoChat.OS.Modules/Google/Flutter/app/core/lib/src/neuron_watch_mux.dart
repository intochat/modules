import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:grpc/grpc.dart' show GrpcError, StatusCode;

import 'neuron_channel.dart';

/// Carries every node watch of one brain on as few `Watch` streams as the server allows
/// ([maxSourceSignals] source signals per stream, `NeuronsService.MaximumWatches`).
///
/// Sources are placed in chunks; adding one reopens only its chunk, after [debounce], and a chunk
/// closes once its last source leaves. The chunk's old stream keeps delivering until the new one
/// yields, so a source with no retained signal loses nothing published during the switch. A chunk that breaks is reopened
/// with exponential backoff and jitter from [retryAfter] up to [maxRetry]; a refusal that a retry
/// cannot fix (permission, authentication, invalid argument) ends its listeners with the error.
/// The server replays each source's retained last signal on every open, so a replay already
/// delivered is dropped and a reopen does not make every view read again.
final class NeuronWatchMux {
  NeuronWatchMux(
    this._link,
    this.brainId, {
    this.debounce = const Duration(milliseconds: 50),
    this.retryAfter = const Duration(seconds: 2),
    this.maxRetry = const Duration(seconds: 30),
    Random? random,
  }) : _random = random ?? Random();

  static const maxSourceSignals = 64;

  static const _fatal = {
    StatusCode.permissionDenied,
    StatusCode.unauthenticated,
    StatusCode.invalidArgument,
  };

  final NeuronLink Function() _link;
  final String brainId;
  final Duration debounce, retryAfter, maxRetry;
  final Random _random;
  final _sources = <String, _Source>{};
  final _chunks = <_Chunk>[];
  final _replayed = <String, int>{};
  bool _closed = false;

  /// How many `Watch` streams are open; a screen's watches share them.
  int get openStreams => _chunks.where((c) => c.stream != null).length;

  /// The source's signals for as long as the returned stream is listened to.
  Stream<NeuronSignal> watch(WatchSourceSpec spec) {
    late final StreamController<NeuronSignal> controller;
    controller = StreamController<NeuronSignal>(
      onListen: () => _add(spec, controller),
      onCancel: () => _remove(spec, controller),
    );
    return controller.stream;
  }

  static int _cost(WatchSourceSpec spec) =>
      spec.types.isEmpty ? 1 : spec.types.length;

  void _add(WatchSourceSpec spec, StreamController<NeuronSignal> listener) {
    final id = _id(spec);
    final existing = _sources[id];
    if (existing != null) {
      existing.listeners.add(listener);
      return;
    }
    final source = _Source(spec)..listeners.add(listener);
    _sources[id] = source;
    final cost = _cost(spec);
    final chunk = _chunks.firstWhere(
      (c) => c.weight + cost <= maxSourceSignals,
      orElse: () {
        final created = _Chunk();
        _chunks.add(created);
        return created;
      },
    );
    chunk.sources[id] = source;
    _schedule(chunk, debounce);
  }

  void _remove(WatchSourceSpec spec, StreamController<NeuronSignal> listener) {
    final id = _id(spec);
    final source = _sources[id];
    if (source == null) return;
    source.listeners.remove(listener);
    if (source.listeners.isNotEmpty) return;
    _sources.remove(id);
    _replayed.removeWhere((key, _) => key.startsWith('${spec.key}\u0000'));
    for (final chunk in _chunks) {
      // A source that leaves stays on the server's watch until the chunk next reopens; nothing is
      // delivered for it. Only an emptied chunk closes.
      if (chunk.sources.remove(id) != null && chunk.sources.isEmpty) {
        _schedule(chunk, debounce);
      }
    }
  }

  void _schedule(_Chunk chunk, Duration after) {
    if (_closed) return;
    chunk.timer?.cancel();
    chunk.timer = Timer(after, () => _open(chunk));
  }

  void _open(_Chunk chunk) {
    chunk.timer = null;
    final previous = chunk.stream;
    chunk.stream = null;
    if (_closed || chunk.sources.isEmpty) {
      unawaited(previous?.cancel());
      _chunks.remove(chunk);
      return;
    }
    final generation = ++chunk.generation;
    var retired = previous == null;
    void retire() {
      if (retired) return;
      retired = true;
      if (identical(chunk.previous, previous)) chunk.previous = null;
      unawaited(previous?.cancel());
    }

    if (previous != null) {
      unawaited(chunk.previous?.cancel());
      chunk.previous = previous;
    }
    chunk.stream = _link()
        .watch(brainId, [for (final s in chunk.sources.values) s.spec])
        .listen(
          (signal) {
            final overlapping = !retired;
            retire();
            chunk.failures = 0;
            _deliver(signal, overlapping: overlapping);
          },
          onError: (Object error) {
            retire();
            _broken(chunk, generation, error);
          },
          onDone: () {
            retire();
            _broken(chunk, generation, null);
          },
          cancelOnError: true,
        );
  }

  void _broken(_Chunk chunk, int generation, Object? error) {
    if (_closed || chunk.generation != generation || chunk.timer != null) {
      return;
    }
    chunk.stream = null;
    if (error is GrpcError && _fatal.contains(error.code)) {
      _refuse(chunk, error);
      return;
    }
    _schedule(chunk, backoffFor(chunk.failures++));
  }

  /// The retry delay after [failures] consecutive breaks: doubling from [retryAfter], capped at
  /// [maxRetry], with up to a quarter of jitter either way (never above the cap).
  Duration backoffFor(int failures) {
    final base = min(
      retryAfter.inMilliseconds * pow(2, min(failures, 16)).toDouble(),
      maxRetry.inMilliseconds.toDouble(),
    );
    final jitter = base * 0.25 * (_random.nextDouble() * 2 - 1);
    return Duration(
      milliseconds: min(
        base + jitter,
        maxRetry.inMilliseconds.toDouble(),
      ).round(),
    );
  }

  void _refuse(_Chunk chunk, GrpcError error) {
    final refused = chunk.sources.values.toList();
    chunk.sources.clear();
    _chunks.remove(chunk);
    for (final source in refused) {
      _sources.remove(_id(source.spec));
      for (final listener in source.listeners.toList()) {
        listener.addError(error);
        unawaited(listener.close());
      }
    }
  }

  // Live signals recently delivered; while a chunk switches streams, the new stream's first signal may be
  // one the old stream already delivered.
  final _recent = <String, DateTime>{};
  static const _recentWindow = Duration(seconds: 2);

  void _deliver(NeuronSignal signal, {bool overlapping = false}) {
    // Live signals carry sequence 0; a retained replay carries its journal sequence.
    if (signal.sequence == 0) {
      final now = DateTime.now();
      _recent.removeWhere((_, at) => now.difference(at) > _recentWindow);
      final key =
          '${signal.source}\u0000${signal.type}\u0000${jsonEncode(signal.payload)}';
      if (overlapping && _recent.containsKey(key)) return;
      _recent[key] = now;
    } else {
      final key = '${signal.source}\u0000${signal.type}';
      if (_replayed[key] == signal.sequence) return;
      _replayed[key] = signal.sequence;
    }
    for (final source in _sources.values) {
      if (source.spec.key != signal.source) continue;
      if (source.spec.types.isNotEmpty &&
          !source.spec.types.contains(signal.type)) {
        continue;
      }
      for (final listener in source.listeners.toList()) {
        listener.add(signal);
      }
    }
  }

  /// Ends every stream; listeners receive done.
  Future<void> close() async {
    _closed = true;
    for (final chunk in _chunks) {
      chunk.timer?.cancel();
      unawaited(chunk.previous?.cancel());
      unawaited(chunk.stream?.cancel());
    }
    _chunks.clear();
    final listeners = [for (final s in _sources.values) ...s.listeners];
    _sources.clear();
    for (final listener in listeners) {
      unawaited(listener.close());
    }
  }

  static String _id(WatchSourceSpec spec) =>
      '${spec.contract}\u0000${spec.key}\u0000${spec.types.join(',')}';
}

final class _Source {
  _Source(this.spec);
  final WatchSourceSpec spec;
  final listeners = <StreamController<NeuronSignal>>{};
}

final class _Chunk {
  final sources = <String, _Source>{};
  StreamSubscription<NeuronSignal>? stream, previous;
  Timer? timer;
  int generation = 0, failures = 0;
  int get weight =>
      sources.values.fold(0, (sum, s) => sum + NeuronWatchMux._cost(s.spec));
}
