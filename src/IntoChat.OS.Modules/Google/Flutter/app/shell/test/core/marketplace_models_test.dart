import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('an installed copy carries its origin and update offer', () {
    final app = InstalledAppSummary.fromJson({
      'name': 'customer-researcher',
      'title': 'Customer Researcher',
      'description': 'Research companies',
      'status': 'Live',
      'version': '1.2.0',
      'operations': ['open'],
      'icon': 'icon.svg',
      'publishedVersion': null,
      'origin': {
        'publisher': 'intochat',
        'name': 'customer-researcher',
        'version': '1.2.0',
      },
      'edited': false,
      'updateAvailable': true,
    });
    expect(app.icon, 'icon.svg');
    expect(app.origin!.publisher, 'intochat');
    expect(app.origin!.version, '1.2.0');
    expect(app.edited, isFalse);
    expect(app.updateAvailable, isTrue);
  });

  test('an app of the brain\'s own has no origin and no update offer', () {
    final app = InstalledAppSummary.fromJson({
      'name': 'shouter',
      'status': 'Live',
      'version': '0.2.0',
      'operations': <String>[],
    });
    expect(app.origin, isNull);
    expect(app.icon, isNull);
    expect(app.edited, isFalse);
    expect(app.updateAvailable, isFalse);
  });

  test('a marketplace entry reads its listing and install state', () {
    final entry = MarketplaceEntry.fromJson({
      'listing': {
        'publisher': 'intochat',
        'source': {
          r'$source': 'brain',
          'accountId': 'acc-1',
          'brainId': 'account-acc-1',
        },
        'name': 'customer-researcher',
        'title': 'Customer Researcher',
        'description': 'Research companies',
        'version': '1.3.0',
        'icon': 'icon.svg',
      },
      'installed': true,
      'updateAvailable': true,
    });
    expect(entry.listing.title, 'Customer Researcher');
    expect(entry.listing.source.brainId, 'account-acc-1');
    expect(entry.listing.source.isShipped, isFalse);
    expect(entry.installed, isTrue);
    expect(entry.updateAvailable, isTrue);
  });

  test(
    'showing a neuron uses workspace state without app preparation',
    () async {
      final requests = <String>[];
      Map<String, dynamic>? opened;
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          requests.add('${request.method} ${request.url.path}');
          if (request.url.path.endsWith('/show')) {
            opened = jsonDecode(request.body) as Map<String, dynamic>;
            return http.Response(
              jsonEncode({
                'revision': 4,
                'path': ['b1', 'b1/apps/ino'],
                'open': ['b1/apps/ino'],
              }),
              200,
            );
          }
          return http.Response(
            jsonEncode({
              'revision': 3,
              'path': ['b1'],
              'open': [],
            }),
            200,
          );
        }),
      );
      final before = await client.readWorkspace('b1');
      final state = await client.showNeuron(
        'b1',
        'b1/apps/ino',
        'open',
        before.revision,
      );
      expect(opened!['app'], 'b1/apps/ino');
      expect(opened!['expectedRevision'], 3);
      expect(state.path.last, 'b1/apps/ino');
      expect(requests.where((r) => r.endsWith('/request')), isEmpty);
      client.close();
    },
  );

  test('a missing icon reads as null', () async {
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async => http.Response('', 404)),
    );
    expect(await client.readBytes('/brains/b1/apps/x/icon'), isNull);
    client.close();
  });
}
