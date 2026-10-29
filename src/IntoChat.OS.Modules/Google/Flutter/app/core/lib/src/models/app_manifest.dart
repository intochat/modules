// Mirrors IntoChat.OS.Modules.Apps.AppManifest, AppRequirements and AppToolbarEntry as the shell reads them from
// GET /brains/{brain}/apps/{name} under "manifest".

enum AppToolbarKind {
  button,
  toggle,
  slider,
  text;

  // The server validates kinds; an unknown one is wire drift and shows as a button.
  static AppToolbarKind parse(String? value) =>
      values.firstWhere((kind) => kind.name == value, orElse: () => button);
}

class AppToolbarEntry {
  const AppToolbarEntry({
    required this.operation,
    this.kind = AppToolbarKind.button,
    this.icon,
    this.label,
    this.min,
    this.max,
  });
  final String operation;
  final AppToolbarKind kind;
  final String? icon, label;
  final double? min, max;
  factory AppToolbarEntry.fromJson(Map<String, dynamic> json) =>
      AppToolbarEntry(
        operation: json['operation'] as String,
        kind: AppToolbarKind.parse(json['kind'] as String?),
        icon: json['icon'] as String?,
        label: json['label'] as String?,
        min: (json['min'] as num?)?.toDouble(),
        max: (json['max'] as num?)?.toDouble(),
      );
}

// What the app needs (AppManifest.Requires): the install view lists it so installing is one consent.
class AppRequirements {
  const AppRequirements({
    this.semanticTypes = const [],
    this.connectors = const [],
    this.secrets = const [],
    this.capabilities = const [],
  });
  final List<String> semanticTypes, connectors, secrets, capabilities;
  bool get isEmpty =>
      semanticTypes.isEmpty &&
      connectors.isEmpty &&
      secrets.isEmpty &&
      capabilities.isEmpty;
  static List<String> _ids(Object? json) => [
    for (final id in json as List? ?? const []) id as String,
  ];
  factory AppRequirements.fromJson(Map<String, dynamic> json) =>
      AppRequirements(
        semanticTypes: _ids(json['semanticTypes']),
        connectors: _ids(json['connectors']),
        secrets: _ids(json['secrets']),
        capabilities: _ids(json['capabilities']),
      );
  static AppRequirements? read(Object? json) => json is Map
      ? AppRequirements.fromJson(Map<String, dynamic>.from(json))
      : null;
}

class AppManifest {
  const AppManifest({
    required this.title,
    required this.description,
    required this.operations,
    this.icon,
    this.toolbar = const [],
    this.requires,
  });
  final String title, description;
  final List<String> operations;
  final String? icon;
  final List<AppToolbarEntry> toolbar;
  final AppRequirements? requires;
  factory AppManifest.fromJson(Map<String, dynamic> json) => AppManifest(
    title: json['title'] as String? ?? '',
    description: json['description'] as String? ?? '',
    operations: [
      for (final operation in json['operations'] as List? ?? const [])
        Map<String, dynamic>.from(operation as Map)['name'] as String,
    ],
    icon: json['icon'] as String?,
    toolbar: [
      for (final entry in json['toolbar'] as List? ?? const [])
        AppToolbarEntry.fromJson(Map<String, dynamic>.from(entry as Map)),
    ],
    requires: AppRequirements.read(json['requires']),
  );
}
