import 'package:web/web.dart' as web;

Future<String?> readPreferences(String key) async =>
    web.window.localStorage.getItem(key);
Future<void> writePreferences(String key, String value) async =>
    web.window.localStorage.setItem(key, value);
