import 'package:intochat_os_flutter/intochat_os_flutter.dart';

// Desktop keeps credentials in memory for the process lifetime only; there is
// no browser reload to survive, and writing them to disk is not warranted for
// a dev stand.
BasicCredentials? readStoredCredentials() => null;

void writeStoredCredentials(BasicCredentials credentials) {}

void clearStoredCredentials() {}
