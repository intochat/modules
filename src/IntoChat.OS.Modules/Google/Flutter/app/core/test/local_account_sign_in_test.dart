import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:test/test.dart';

void main() {
  const session =
      '{"principalId":"vlad","accountId":"allocated-account","brainId":"allocated-brain"}';
  for (final loginStatus in [200, 401, 409, 500]) {
    test(
      'local sign in handles login status $loginStatus without anonymous fallback',
      () async {
        final paths = <String>[];
        final client = IntoChatOSUiClient(
          baseUri: Uri.parse('http://localhost:5080'),
          httpClient: MockClient((r) async {
            paths.add(r.url.path);
            expect(jsonDecode(r.body)['principalId'], 'vlad');
            return http.Response(
              session,
              r.url.path.endsWith('register') ? 200 : loginStatus,
            );
          }),
        );
        addTearDown(client.close);
        if (loginStatus == 200 || loginStatus == 401) {
          await client.signInLocalAccount('vlad', 'local-test-password');
          expect(client.principalId, 'vlad');
          expect(client.defaultBrainId, 'allocated-brain');
        } else {
          await expectLater(
            client.signInLocalAccount('vlad', 'local-test-password'),
            throwsA(isA<UiRequestException>()),
          );
          expect(client.principalId, isNull);
        }
        expect(paths, [
          '/identity/login',
          if (loginStatus == 401) '/identity/register',
        ]);
      },
    );
  }
  test('a registration conflict is not retried or replaced', () async {
    var calls = 0;
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://localhost'),
      httpClient: MockClient(
        (r) async => http.Response('{}', ++calls == 1 ? 401 : 409),
      ),
    );
    await expectLater(
      client.signInLocalAccount('vlad', 'password'),
      throwsA(isA<UiRequestException>()),
    );
    expect(calls, 2);
    expect(client.principalId, isNull);
    client.close();
  });
  test('remote destinations never receive local credentials', () async {
    var calls = 0;
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('https://example.test'),
      httpClient: MockClient((r) async {
        calls++;
        return http.Response(session, 200);
      }),
    );
    await expectLater(
      client.signInLocalAccount('vlad', 'password'),
      throwsStateError,
    );
    expect(calls, 0);
    client.close();
  });
  for (final response in [
    '{}',
    '{"principalId":"owner","accountId":"owner","brainId":"owner"}',
    '{"principalId":"vlad","accountId":"","brainId":""}',
  ]) {
    test('invalid or wrong session is refused: $response', () async {
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://localhost'),
        httpClient: MockClient((r) async => http.Response(response, 200)),
      );
      await expectLater(
        client.signInLocalAccount('vlad', 'password'),
        throwsStateError,
      );
      expect(client.principalId, isNull);
      client.close();
    });
  }
}
