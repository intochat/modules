{{flutter_js}}
{{flutter_build_config}}

// No serviceWorkerSettings: Flutter's own worker is deprecated and unregisters itself, which would also drop the
// shell's worker (sw.js, same scope). index.html registers sw.js.
_flutter.loader.load();
