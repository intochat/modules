// External links open an app in the signed-in account's selected brain.
// App-owned input stays opaque to the connector.
Map<String, dynamic>? workspaceLink(Uri? location) {
  if (location == null) return null;
  final route = location.fragment.startsWith('/')
      ? Uri.tryParse(location.fragment)
      : location;
  if (route == null || route.path != '/open') return null;
  // A repeated key is ambiguous, so it is not a link.
  for (final key in const ['application', 'input', 'publisher', 'release']) {
    if ((route.queryParametersAll[key]?.length ?? 0) > 1) return null;
  }
  final application = route.queryParameters['application'];
  if (application == null) return null;
  final input = route.queryParameters['input'] ?? '';
  if (input.length > 2048) return null;
  final publisher = route.queryParameters['publisher'];
  final release = route.queryParameters['release'];
  if (publisher == null) {
    if (release != null) return null;
    if (!RegExp(r'^[a-z0-9][a-z0-9-]{0,79}$').hasMatch(application)) {
      return null;
    }
    return {
      'kind': 'workspace',
      'application': application,
      'operation': 'open',
      'input': input,
    };
  }
  // Names follow the Apps app's grammar: letter first, "_" allowed, at most 64.
  final name = RegExp(r'^[a-z][a-z0-9_-]{0,63}$');
  if (!name.hasMatch(publisher) || !name.hasMatch(application)) return null;
  if (release != null &&
      !RegExp(r'^[0-9]{1,9}\.[0-9]{1,9}\.[0-9]{1,9}$').hasMatch(release)) {
    return null;
  }
  // A shared app link opens that app's detail in Apps; the input is not carried.
  return {
    'kind': 'workspace',
    'application': 'apps',
    'operation': 'open',
    'input': 'app:$publisher/$application${release == null ? '' : '@$release'}',
  };
}
