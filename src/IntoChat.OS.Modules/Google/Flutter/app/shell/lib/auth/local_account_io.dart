import 'dart:convert';
import 'dart:io';

import 'local_account.dart';

Future<LocalAccount?> readLocalAccount(
  Uri server, {
  Map<String, String>? environment,
}) async {
  final path =
      (environment ?? Platform.environment)['INTOCHATOS_LOCAL_ACCOUNT_FILE'];
  if (path == null || path.isEmpty) return null;
  if (!['localhost', '127.0.0.1', '::1', '[::1]'].contains(server.host) ||
      !['http', 'https'].contains(server.scheme)) {
    throw StateError('Local accounts require a loopback server.');
  }
  try {
    final value = jsonDecode(await File(path).readAsString());
    if (value is Map<String, dynamic> &&
        value['principalId'] is String &&
        RegExp(r'^[a-z0-9][a-z0-9-]{0,79}$')
            .hasMatch(value['principalId'] as String) &&
        !['owner', 'integrations'].contains(value['principalId']) &&
        value['password'] is String &&
        RegExp(r'^[a-fA-F0-9]{64}$').hasMatch(value['password'] as String)) {
      return LocalAccount(
        value['principalId'] as String,
        value['password'] as String,
      );
    }
  } on Object {
    // Never expose a credential-file parse error (which can contain its content).
  }
  throw StateError('The local account file could not be read.');
}
