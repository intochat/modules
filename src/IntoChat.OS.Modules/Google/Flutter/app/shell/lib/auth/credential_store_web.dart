import 'package:intochat_os_flutter/intochat_os_flutter.dart';

// Basic credentials are transient. Reload authentication uses the session cookie.
BasicCredentials? _credentials;

BasicCredentials? readStoredCredentials() => _credentials;

void writeStoredCredentials(BasicCredentials credentials) {
  _credentials = credentials;
}

void clearStoredCredentials() {
  _credentials = null;
}
