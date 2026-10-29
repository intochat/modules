/// Mirrors C# `IntoChat.OS.Modules.Flutter.Preferences.PreferenceStore`: where a
/// preference lives. The `local*` stores belong to this device.
enum PreferenceStore {
  notifyAgentFinished(0),
  notifyNeedsApproval(1),
  notifyConnectionErrors(2),
  notifyProductUpdates(3),
  newTabApp(4),
  localDisplayName(10),
  localTheme(11),
  localCompactSpacing(12),
  localReducedMotion(13),
  localDefaultModel(14);

  const PreferenceStore(this.wire);
  final int wire;

  bool get isLocal => wire >= 10;

  /// The store for a wire int or member name; null when unknown.
  static PreferenceStore? parse(Object? value) {
    for (final store in values) {
      if (value == store.wire ||
          (value is String &&
              value.toLowerCase() == store.name.toLowerCase())) {
        return store;
      }
    }
    return null;
  }
}

/// Mirrors C# `PreferenceKind`.
enum PreferenceKind {
  toggle,
  choice,
  text;

  static PreferenceKind parse(Object? value) => switch (value) {
    1 || 'choice' || 'Choice' => choice,
    2 || 'text' || 'Text' => text,
    _ => toggle,
  };
}

/// The device-local preferences a `preference` neuron with a `local*` store
/// edits. The shell implements it over its device store; values are strings:
/// `on`/`off` for toggles, a choice id, or text. Null means "not set".
abstract interface class LocalPreferences {
  String? read(PreferenceStore store);

  Future<void> write(PreferenceStore store, String value);
}
