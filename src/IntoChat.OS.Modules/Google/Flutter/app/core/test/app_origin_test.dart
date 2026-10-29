import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:test/test.dart';

void main() {
  test('an origin reads its release digest, installer and consent', () {
    final origin = AppOrigin.fromJson({
      'publisher': 'vlad',
      'source': {
        r'$source': 'brain',
        'accountId': 'account-1',
        'brainId': 'brain-1',
      },
      'name': 'echo',
      'version': '1.0.0',
      'publicationDigest': 'fixture-digest-0001',
      'installedByAccount': 'account-2',
      'consented': {
        'semanticTypes': ['person.email'],
        'connectors': ['gmail'],
      },
    });
    expect(origin.publicationDigest, 'fixture-digest-0001');
    expect(origin.installedByAccount, 'account-2');
    expect(origin.consented?.semanticTypes, ['person.email']);
    expect(origin.consented?.connectors, ['gmail']);
  });

  test('an origin recorded before consent was asked reads as none', () {
    final origin = AppOrigin.fromJson({
      'publisher': 'os',
      'source': {r'$source': 'shipped'},
      'name': 'ino',
      'version': '3.2.0',
    });
    expect(origin.publicationDigest, isNull);
    expect(origin.installedByAccount, isNull);
    expect(origin.consented, isNull);
  });
}
