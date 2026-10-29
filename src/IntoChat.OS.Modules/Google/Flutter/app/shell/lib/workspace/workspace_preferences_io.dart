import 'dart:convert';
import 'dart:io';

File _file(String key) {
  final root =
      Platform.environment['LOCALAPPDATA'] ?? Directory.systemTemp.path;
  final name = base64Url.encode(utf8.encode(key));
  return File('$root/IntoChat.OS/preferences/$name.json');
}

Future<String?> readPreferences(String key) async {
  final file = _file(key);
  return await file.exists() ? await file.readAsString() : null;
}

Future<void> writePreferences(String key, String value) async {
  final file = _file(key);
  await file.parent.create(recursive: true);
  await file.writeAsString(value, flush: true);
}
