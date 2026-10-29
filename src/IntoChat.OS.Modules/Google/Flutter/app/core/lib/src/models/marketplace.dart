import 'app_manifest.dart';

// Mirrors the Apps module's Marketplace records; the listing is what a publisher released.

// Where a listed app's bundle is read from: a person's brain, or the catalog shipped in the image.
class AppSource {
  const AppSource.brain({
    required String this.accountId,
    required String this.brainId,
  }) : kind = 'brain';
  const AppSource.shipped()
    : kind = 'shipped',
      accountId = null,
      brainId = null;
  final String kind;
  final String? accountId, brainId;
  bool get isShipped => kind == 'shipped';
  factory AppSource.fromJson(Map<String, dynamic> json) =>
      json[r'$source'] == 'shipped'
      ? const AppSource.shipped()
      : AppSource.brain(
          accountId: json['accountId'] as String? ?? '',
          brainId: json['brainId'] as String? ?? '',
        );
  static AppSource read(Object? json) => json is Map
      ? AppSource.fromJson(Map<String, dynamic>.from(json))
      : const AppSource.brain(accountId: '', brainId: '');
}

// Consented is what the person accepted at install or update; a copy recorded
// before consent was asked has none, and its manifest's requirements stand for it.
class AppOrigin {
  const AppOrigin({
    required this.publisher,
    required this.source,
    required this.name,
    required this.version,
    this.publicationDigest,
    this.installedByAccount,
    this.consented,
  });
  final String publisher, name, version;
  final AppSource source;
  final String? publicationDigest, installedByAccount;
  final AppRequirements? consented;
  factory AppOrigin.fromJson(Map<String, dynamic> json) => AppOrigin(
    publisher: json['publisher'] as String? ?? '',
    source: AppSource.read(json['source']),
    name: json['name'] as String? ?? '',
    version: json['version'] as String? ?? '',
    publicationDigest: json['publicationDigest'] as String?,
    installedByAccount: json['installedByAccount'] as String?,
    consented: AppRequirements.read(json['consented']),
  );
}

class MarketplaceListing {
  const MarketplaceListing({
    required this.publisher,
    required this.source,
    required this.name,
    required this.title,
    required this.description,
    required this.version,
    required this.icon,
    this.requires,
  });
  final String publisher;
  final AppSource source;
  final String name, title, description, version;
  final String? icon;
  final AppRequirements? requires;
  factory MarketplaceListing.fromJson(Map<String, dynamic> json) =>
      MarketplaceListing(
        publisher: json['publisher'] as String? ?? '',
        source: AppSource.read(json['source']),
        name: json['name'] as String,
        title: json['title'] as String? ?? json['name'] as String,
        description: json['description'] as String? ?? '',
        version: json['version'] as String? ?? '',
        icon: json['icon'] as String?,
        requires: AppRequirements.read(json['requires']),
      );
}

class MarketplaceEntry {
  const MarketplaceEntry({
    required this.listing,
    required this.installed,
    required this.updateAvailable,
  });
  final MarketplaceListing listing;
  final bool installed, updateAvailable;
  factory MarketplaceEntry.fromJson(Map<String, dynamic> json) =>
      MarketplaceEntry(
        listing: MarketplaceListing.fromJson(
          Map<String, dynamic>.from(json['listing'] as Map),
        ),
        installed: json['installed'] == true,
        updateAvailable: json['updateAvailable'] == true,
      );
}
