import 'package:intochat_os_flutter/intochat_os_flutter.dart';

/// Wire value of the C# `EntryKind` (camel case).
enum EntryKind {
  variable,
  secret;

  static EntryKind parse(Object? value) =>
      value == 'secret' ? EntryKind.secret : EntryKind.variable;
}

/// One row of `GET /brains/{brainId}/entries`. A secret never carries its
/// value; [tail] is its last four characters, and only for long secrets.
final class EntrySummary {
  const EntrySummary({
    required this.name,
    required this.kind,
    this.isSet = true,
    this.value,
    this.tail,
    this.updatedAt,
  });
  final String name;
  final EntryKind kind;
  final bool isSet;
  final String? value, tail;
  final DateTime? updatedAt;

  factory EntrySummary.fromJson(Map<String, dynamic> json) => EntrySummary(
    name: json['name'] as String,
    kind: EntryKind.parse(json['kind']),
    isSet: json['isSet'] != false,
    value: json['value'] as String?,
    tail: json['tail'] as String?,
    updatedAt: DateTime.tryParse(json['updatedAt'] as String? ?? ''),
  );
}

/// API keys are secret entries named `provider:<id>`.
const providerEntryPrefix = 'provider:';

/// The key-based providers, in display order. Mirrors `BrainEntries.KeyedProviderIds`.
const keyProviders = <(String, String)>[
  ('openai', 'OpenAI'),
  ('anthropic', 'Anthropic'),
  ('google', 'Google'),
  ('xai', 'xAI'),
  ('openrouter', 'OpenRouter'),
];

/// Thrown when the signed-in user is not the brain's owner (HTTP 403).
final class SettingsForbidden implements Exception {
  const SettingsForbidden();
  @override
  String toString() => 'Only the brain owner can change these settings.';
}

/// A brain's entries over the signed-in person's session. Entries are
/// owner-only on the server; values go only to `PUT`, never anywhere else.
class EntriesClient {
  EntriesClient(this.client, this.brainId);
  final IntoChatOSUiClient client;
  final String brainId;

  String get _entries => '/brains/${Uri.encodeComponent(brainId)}/entries';

  Future<dynamic> _call(String method, String path, [Object? body]) async {
    try {
      return await client.jsonRequest(method, '$_entries$path', body);
    } on UiRequestException catch (error) {
      if (error.statusCode == 403) throw const SettingsForbidden();
      rethrow;
    }
  }

  Future<List<EntrySummary>> list() async => [
    for (final row in (await _call('GET', '') as List? ?? const []))
      EntrySummary.fromJson(Map<String, dynamic>.from(row as Map)),
  ];

  Future<void> put(String name, EntryKind kind, String value) => _call(
    'PUT',
    '/${Uri.encodeComponent(name)}',
    {'kind': kind.name, 'value': value},
  );

  Future<void> remove(String name) =>
      _call('DELETE', '/${Uri.encodeComponent(name)}');
}
