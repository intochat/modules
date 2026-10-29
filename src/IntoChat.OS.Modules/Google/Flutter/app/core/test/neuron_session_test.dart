import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter/src/generated/neurons.pbgrpc.dart' as rpc;
import 'package:grpc/grpc.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:test/test.dart';

final class SessionService extends rpc.NeuronsServiceBase {
  final received = <Map<String, String>>[];
  final watches = <rpc.WatchRequest>[];
  Duration callDelay = Duration.zero;

  @override
  Future<rpc.CallReply> call(ServiceCall call, rpc.CallRequest request) async {
    received.add(Map.of(call.clientMetadata ?? {}));
    await Future<void>.delayed(callDelay);
    return rpc.CallReply(result: utf8.encode('{}'));
  }

  @override
  Stream<rpc.SignalEnvelope> watch(
    ServiceCall call,
    rpc.WatchRequest request,
  ) async* {
    received.add(Map.of(call.clientMetadata ?? {}));
    watches.add(request);
    yield rpc.SignalEnvelope();
  }
}

void main() {
  test('signedInNativeCallsAndWatchesCarryTheCurrentSessionCookie', () async {
    final service = SessionService();
    final server = Server.create(services: [service]);
    await server.serve(address: '127.0.0.1', port: 0);
    addTearDown(server.shutdown);
    var cookie = 'session=vlad-session';
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://127.0.0.1:${server.port}'),
      httpClient: MockClient(
        (request) async => http.Response(
          '{"principalId":"vlad","accountId":"account-id","brainId":"personal-brain"}',
          200,
          headers: {'set-cookie': '$cookie; HttpOnly; Path=/'},
        ),
      ),
    );
    addTearDown(client.close);
    // Constructing a channel before sign-in must not freeze anonymous metadata.
    final channel = client.channel;
    await client.signIn('vlad', 'test-password');
    await channel.call('personal-brain', 'Contract', 'key', 'Read', []);
    await channel.watch('personal-brain', []).toList();
    expect(service.received.map((m) => m['cookie']), [
      'session=vlad-session',
      'session=vlad-session',
    ]);
    cookie = 'session=renewed-session';
    await client.readSession();
    await channel.call('personal-brain', 'Contract', 'key', 'Read', []);
    expect(service.received.last['cookie'], 'session=renewed-session');
  });

  test('anonymousAndBasicClientsKeepTheirExistingBehavior', () async {
    final service = SessionService();
    final server = Server.create(services: [service]);
    await server.serve(address: '127.0.0.1', port: 0);
    addTearDown(server.shutdown);
    for (final credentials in [
      null,
      const BasicCredentials(username: 'ops', password: 'test'),
    ]) {
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://127.0.0.1:${server.port}'),
        credentials: credentials,
      );
      addTearDown(client.close);
      await client.channel.call('owner', 'Contract', 'key', 'Read', []);
      expect(service.received.last['cookie'], isNull);
      expect(
        service.received.last['authorization'],
        credentials?.authorizationHeader,
      );
    }
  });

  test("a screen's node watches ride one Watch call to the server", () async {
    final service = SessionService();
    final server = Server.create(services: [service]);
    await server.serve(address: '127.0.0.1', port: 0);
    addTearDown(server.shutdown);
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://127.0.0.1:${server.port}'),
    );
    addTearDown(client.close);
    final subscriptions = [
      for (final node in [
        ('surface', 'owner/apps/a/surface'),
        ('surface', 'owner/apps/b/surface'),
        ('text', 'owner/apps/a/title'),
      ])
        client.watchNode('owner', node.$1, node.$2).listen((_) {}),
    ];
    await Future<void>.delayed(const Duration(milliseconds: 300));
    expect(service.watches, hasLength(1));
    expect(service.watches.single.sources.map((s) => s.key), [
      'owner/apps/a/surface',
      'owner/apps/b/surface',
      'owner/apps/a/title',
    ]);
    for (final subscription in subscriptions) {
      await subscription.cancel();
    }
  });

  test('a call past its deadline fails with DEADLINE_EXCEEDED', () async {
    final service = SessionService()..callDelay = const Duration(seconds: 2);
    final server = Server.create(services: [service]);
    await server.serve(address: '127.0.0.1', port: 0);
    addTearDown(server.shutdown);
    final channel = NeuronChannel(
      Uri.parse('http://127.0.0.1:${server.port}'),
      callTimeout: const Duration(milliseconds: 100),
    );
    addTearDown(channel.shutdown);
    await expectLater(
      channel.call('owner', 'Contract', 'key', 'Read', []),
      throwsA(
        isA<GrpcError>().having(
          (e) => e.code,
          'code',
          StatusCode.deadlineExceeded,
        ),
      ),
    );
  });
}
