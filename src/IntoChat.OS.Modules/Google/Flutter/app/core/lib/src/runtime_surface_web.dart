import 'dart:js_interop';

Uri? sameOriginUiBase() {
  final origin = Uri.base.origin;
  if (origin.isEmpty) {
    return null;
  }
  return Uri.parse('$origin/');
}

/// A per-session kernel URL carried in the page address: the test harness and
/// multi-host setups pass it so bundles stay byte-identical and hosts never
/// share a served config file.
String? queryUiBase() {
  final value = Uri.base.queryParameters['uiBase'];
  return value == null || value.isEmpty ? null : value;
}

const _storageKey = 'intochat.os.uiBase';

@JS('localStorage.getItem')
external JSString? _getItem(JSString key);

@JS('localStorage.setItem')
external void _setItem(JSString key, JSString value);

/// The kernel URL this origin was last pointed at (see rememberedUiBase).
/// Storage can be unavailable (private windows, blocked site data); that is
/// the same as nothing remembered.
String? storedUiBase() {
  try {
    return _getItem(_storageKey.toJS)?.toDart;
  } catch (_) {
    return null;
  }
}

void storeUiBase(String value) {
  try {
    _setItem(_storageKey.toJS, value.toJS);
  } catch (_) {
    // Not remembered; the page address still applies to this session.
  }
}
