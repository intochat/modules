// Mirrors IntoChat.OS.Modules.Microsoft.Aspire.SiloState: one silo of the cluster and the build it runs.
final class SiloState {
  const SiloState({
    required this.id,
    required this.sha,
    required this.state,
    this.health,
  });
  final String id, sha, state;
  final String? health;

  factory SiloState.fromJson(Map<String, dynamic> json) => SiloState(
    id: json['id'] as String,
    sha: json['sha'] as String? ?? '',
    state: json['state'] as String? ?? 'Unknown',
    health: (json['health'] as String?)?.isEmpty == true
        ? null
        : json['health'] as String?,
  );

  @override
  bool operator ==(Object other) =>
      other is SiloState &&
      other.id == id &&
      other.sha == sha &&
      other.state == state &&
      other.health == health;

  @override
  int get hashCode => Object.hash(id, sha, state, health);
}

// Mirrors the DeploymentChanged signal: the deployment's stage, the build it stands on, and every silo
// of the Orleans membership.
final class DeploymentChanged {
  const DeploymentChanged({
    required this.stage,
    required this.sha,
    required this.silos,
  });
  final String stage, sha;
  final List<SiloState> silos;

  factory DeploymentChanged.fromJson(Map<String, dynamic> json) =>
      DeploymentChanged(
        stage: (json['stage'] as String? ?? 'steady').toLowerCase(),
        sha: json['sha'] as String? ?? '',
        silos: [
          for (final silo in json['silos'] as List? ?? const [])
            SiloState.fromJson(Map<String, dynamic>.from(silo as Map)),
        ],
      );
}

// One event of GET /brains/{id}/live: the signal's simple type name, the neuron that published it,
// and the signal itself as camelCase JSON. One {"type":"ready"} event follows the retained replay of
// every source: the stream is connected and what comes after it is live.
// The server pulls each source from its signal journal; sequence and replay are the client's watermark.
final class LiveEnvelope {
  const LiveEnvelope({
    required this.type,
    required this.publisher,
    required this.payload,
    this.sequence,
    this.replay = false,
  });
  final String type, publisher;
  final Map<String, dynamic> payload;
  // The retention sequence of the signal when its source retains one; replay marks a retained signal
  // sent on connect rather than a live publish.
  final int? sequence;
  final bool replay;
  bool get isReady => type == 'ready';

  /// The reader fell behind; the server ends the stream and [IntoChatOSUiClient.watchLive] reconnects.
  bool get isResync => type == 'resync';

  factory LiveEnvelope.fromJson(Map<String, dynamic> json) => LiveEnvelope(
    type: json['type'] as String,
    publisher: json['publisher'] as String? ?? '',
    payload: Map<String, dynamic>.from(json['payload'] as Map? ?? const {}),
    sequence: (json['sequence'] as num?)?.toInt(),
    replay: json['replay'] == true,
  );
}

// Mirrors the AppActivity signal: what one app is doing right now. State is an Ino presence name.
final class AppActivity {
  const AppActivity({
    required this.app,
    required this.state,
    required this.detail,
  });
  final String app, state, detail;

  factory AppActivity.fromJson(Map<String, dynamic> json) => AppActivity(
    app: json['app'] as String? ?? '',
    state: json['state'] as String? ?? 'idle',
    detail: json['detail'] as String? ?? '',
  );
}

// A brain the signed-in person is in, as GET /identity/brains lists it: its id and its kind (Personal or Circle).
final class BrainEntry {
  const BrainEntry({required this.id, required this.kind});

  final String id, kind;
  bool get isCircle => kind == 'Circle';

  factory BrainEntry.fromJson(Map<String, dynamic> json) => BrainEntry(
    id: json['id'] as String? ?? '',
    kind: json['kind'] as String? ?? 'Personal',
  );
}

// Something that happened inside one circle brain, from that brain's own live feed: a line spoken (the
// CircleSpoke signal) or the circle changed (CircleChanged). Ready marks one circle's feed connected.
final class CircleActivity {
  const CircleActivity({
    required this.circle,
    required this.kind,
    this.sequence,
    this.replay = false,
  }) : ready = false;

  const CircleActivity.ready(this.circle)
    : kind = '',
      sequence = null,
      replay = false,
      ready = true;

  final String circle, kind;
  final int? sequence;
  final bool replay, ready;

  factory CircleActivity.fromEnvelope(String circle, LiveEnvelope envelope) =>
      envelope.isReady
      ? CircleActivity.ready(circle)
      : CircleActivity(
          circle: circle,
          kind: envelope.type == 'CircleSpoke' ? 'spoke' : 'changed',
          sequence: envelope.sequence,
          replay: envelope.replay,
        );
}
