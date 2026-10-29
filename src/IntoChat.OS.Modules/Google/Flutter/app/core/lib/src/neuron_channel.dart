import 'dart:convert';

import 'package:grpc/grpc.dart' show CallOptions;
import 'package:grpc/grpc_connection_interface.dart' show ClientChannelBase;

import 'generated/neurons.pb.dart' as pb;
import 'generated/neurons.pbgrpc.dart' as rpc;
import 'neuron_channel_io.dart'
    if (dart.library.js_interop) 'neuron_channel_web.dart'
    as transport;

/// One signal as the channel delivers it; [payload] is the signal's JSON.
final class NeuronSignal {
  const NeuronSignal({
    required this.source,
    required this.type,
    required this.publisher,
    required this.sequence,
    required this.payload,
    required this.serverTimeMs,
  });
  final String source, type, publisher;
  final int sequence, serverTimeMs;
  final Map<String, dynamic> payload;

  factory NeuronSignal.fromEnvelope(pb.SignalEnvelope e) => NeuronSignal(
    source: e.source,
    type: e.type,
    publisher: e.publisher,
    sequence: e.sequence.toInt(),
    payload: e.payload.isEmpty
        ? const {}
        : jsonDecode(utf8.decode(e.payload)) as Map<String, dynamic>,
    serverTimeMs: e.serverTimeMs.toInt(),
  );
}

/// A source to watch: its contract, its brain-scoped key and the signal types wanted.
final class WatchSourceSpec {
  const WatchSourceSpec(this.contract, this.key, this.types);
  final String contract, key;
  final List<String> types;
}

/// The two operations the OS has: call a contract, watch a source. [NeuronChannel] carries them
/// over gRPC(-Web); a test supplies its own link.
abstract interface class NeuronLink {
  Future<Map<String, dynamic>?> call(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments,
  );
  Stream<NeuronSignal> watch(String brainId, List<WatchSourceSpec> sources);
  Future<void> shutdown();
}

/// The two operations over one gRPC(-Web) connection.
final class NeuronChannel implements NeuronLink {
  NeuronChannel(
    Uri baseUri, {
    String? authorization,
    Map<String, String> Function()? sessionHeaders,
    this.callTimeout = defaultCallTimeout,
  }) : _channel = transport.createNeuronChannel(baseUri),
       _options = CallOptions(
         metadata: authorization == null
             ? const {}
             : {'authorization': authorization},
         providers: [
           if (sessionHeaders != null)
             (metadata, uri) => metadata.addAll(sessionHeaders()),
         ],
       ) {
    _client = rpc.NeuronsClient(_channel, options: _options);
  }

  /// The deadline a [call] gets unless it names its own; `Watch` streams have none.
  static const defaultCallTimeout = Duration(seconds: 15);
  final Duration callTimeout;
  final ClientChannelBase _channel;
  final CallOptions _options;
  late final rpc.NeuronsClient _client;

  static pb.CallRequest callRequest(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments,
  ) => pb.CallRequest(
    brainId: brainId,
    contract: contract,
    key: key,
    method: method,
    arguments: utf8.encode(jsonEncode(arguments)),
  );

  /// The per-call options: [timeout], else [callTimeout].
  CallOptions callOptions({Duration? timeout}) =>
      CallOptions(timeout: timeout ?? callTimeout);

  /// Runs a contract method; null when the method answers nothing. It fails with
  /// `DEADLINE_EXCEEDED` after [timeout] (default [callTimeout]).
  @override
  Future<Map<String, dynamic>?> call(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments, {
    Duration? timeout,
  }) async {
    final reply = await _client.call(
      callRequest(brainId, contract, key, method, arguments),
      options: callOptions(timeout: timeout),
    );
    if (reply.result.isEmpty) return null;
    final decoded = jsonDecode(utf8.decode(reply.result));
    return decoded is Map<String, dynamic> ? decoded : {'value': decoded};
  }

  /// The sources' signals: each one's retained last signal first, then live ones.
  @override
  Stream<NeuronSignal> watch(String brainId, List<WatchSourceSpec> sources) {
    final request = pb.WatchRequest(brainId: brainId)
      ..sources.addAll([
        for (final s in sources)
          pb.WatchSource(
            contract: s.contract,
            key: s.key,
            signalTypes: s.types,
          ),
      ]);
    return _client.watch(request).map(NeuronSignal.fromEnvelope);
  }

  @override
  Future<void> shutdown() => _channel.shutdown();
}
