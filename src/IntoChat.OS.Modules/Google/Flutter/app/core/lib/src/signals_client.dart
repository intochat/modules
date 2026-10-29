import 'ui_client.dart';

// One journalled signal as GET /brains/{id}/signals returns it. The payload is the signal's own
// JSON; property casing is the server's, so readers go through [field].
final class JournalSignal {
  const JournalSignal({
    required this.sequence,
    required this.type,
    required this.publisher,
    this.payload = const {},
  });

  final int sequence;
  final String type, publisher;
  final Map<String, dynamic> payload;

  Object? field(String name) {
    for (final entry in payload.entries) {
      if (entry.key.toLowerCase() == name.toLowerCase()) return entry.value;
    }
    return null;
  }

  factory JournalSignal.fromJson(Map<String, dynamic> json) => JournalSignal(
    sequence: (json['sequence'] as num).toInt(),
    type: json['type'] as String,
    publisher: json['publisher'] as String? ?? '',
    payload: json['payload'] is Map
        ? Map<String, dynamic>.from(json['payload'] as Map)
        : const {},
  );
}

final class SignalsPage {
  const SignalsPage({required this.next, this.signals = const []});

  // The sequence to pass as `since` for the following page; equals `since` when nothing is left.
  final int next;
  final List<JournalSignal> signals;

  factory SignalsPage.fromJson(Map<String, dynamic> json) => SignalsPage(
    next: (json['next'] as num).toInt(),
    signals: [
      for (final item in (json['signals'] as List? ?? const []))
        JournalSignal.fromJson(Map<String, dynamic>.from(item as Map)),
    ],
  );
}

typedef SignalsLoader = Future<SignalsPage> Function(int since, int limit);

extension BrainSignals on IntoChatOSUiClient {
  Future<SignalsPage> signals(
    String brainId, {
    int since = 0,
    int limit = 200,
  }) async {
    final body = await jsonRequest(
      'GET',
      '/brains/${Uri.encodeComponent(brainId)}/signals?since=$since&limit=$limit',
    );
    return SignalsPage.fromJson(Map<String, dynamic>.from(body as Map));
  }
}
