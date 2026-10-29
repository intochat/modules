import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('a manifest carries its toolbar with kinds and slider bounds', () {
    final manifest = AppManifest.fromJson({
      'title': 'Player',
      'description': 'plays',
      'icon': 'icon.svg',
      'operations': [
        {'name': 'play', 'description': ''},
        {'name': 'volume', 'description': ''},
      ],
      'toolbar': [
        {'operation': 'play', 'icon': 'play', 'label': 'Play'},
        {
          'operation': 'volume',
          'kind': 'slider',
          'icon': 'volume',
          'min': 0,
          'max': 100,
        },
      ],
    });
    expect(manifest.operations, ['play', 'volume']);
    expect(manifest.toolbar.map((entry) => entry.kind), [
      AppToolbarKind.button,
      AppToolbarKind.slider,
    ]);
    expect(manifest.toolbar.last.max, 100);
    expect(manifest.toolbar.first.label, 'Play');
  });

  test('a manifest and a listing carry what the app requires', () {
    final requires = {
      'semanticTypes': ['person.email'],
      'connectors': ['windows'],
      'secrets': null,
    };
    final manifest = AppManifest.fromJson({
      'title': 'Files',
      'operations': [],
      'requires': requires,
    });
    expect(manifest.requires!.semanticTypes, ['person.email']);
    expect(manifest.requires!.connectors, ['windows']);
    expect(manifest.requires!.secrets, isEmpty);
    final listing = MarketplaceListing.fromJson({
      'publisher': 'os',
      'name': 'files',
      'requires': requires,
    });
    expect(listing.requires!.connectors, ['windows']);
    expect(AppManifest.fromJson({'title': 'Echo'}).requires, isNull);
  });

  test('a manifest without a toolbar has none', () {
    expect(
      AppManifest.fromJson({'title': 'Echo', 'operations': [], 'toolbar': null})
          .toolbar,
      isEmpty,
    );
    expect(AppToolbarKind.parse('knob'), AppToolbarKind.button);
  });

  test('the client reads a manifest from the app view', () async {
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async {
        expect(request.url.path, '/brains/b1/apps/player');
        return http.Response(
          jsonEncode({
            'name': 'player',
            'manifest': {
              'title': 'Player',
              'description': '',
              'operations': [
                {'name': 'play', 'description': ''},
              ],
              'toolbar': [
                {'operation': 'play'},
              ],
            },
          }),
          200,
        );
      }),
    );
    final manifest = await client.readAppManifest('b1', 'player');
    expect(manifest.toolbar.single.operation, 'play');
    client.close();
  });

  test('the client streams live envelopes and decodes the cluster', () async {
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient.streaming((request, _) async {
        expect(request.url.path, '/brains/b1/live');
        expect(request.url.queryParametersAll['type'], ['DeploymentChanged']);
        final event = jsonEncode({
          'type': 'DeploymentChanged',
          'publisher': 'b1/aspire/deployment',
          'payload': {
            'stage': 'rolling',
            'sha': 'abc1234567',
            'silos': [
              {
                'id': 'silo-1',
                'sha': 'abc1234567',
                'state': 'Running',
                'health': 'Healthy',
              },
              {
                'id': 'silo-2',
                'sha': 'old',
                'state': 'Starting',
                'health': null,
              },
            ],
          },
        });
        return http.StreamedResponse(
          Stream.value(utf8.encode('data: $event\n\n')),
          200,
        );
      }),
    );
    final cluster = await client.watchDeployment('b1').first;
    expect(cluster.stage, 'rolling');
    expect(cluster.silos.map((silo) => silo.id), ['silo-1', 'silo-2']);
    expect(cluster.silos.last.health, isNull);
    client.close();
  });
}
