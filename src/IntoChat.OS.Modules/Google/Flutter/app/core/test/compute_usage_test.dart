import 'dart:convert';

import 'package:test/test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';

Map<String, Object?> cost([Map<String, Object?> fields = const {}]) => {
  'traceId': 'trace',
  'costId': 'attempt',
  'kind': 'externalprovider',
  'item': 'mail.read',
  'quantity': 1,
  'unit': 'request',
  'micros': 0,
  ...fields,
};

void main() {
  test('old costs and aggregates retain unknown accounting metadata', () {
    final old = ComputeCost.fromJson(cost());
    expect(old.fundingKind, isNull);
    expect(old.pricingStatus, isNull);
    expect(old.listPriceMicros, isNull);
    expect(old.estimatedListPriceMicros, isNull);
    expect(old.attemptState, isNull);
    final summary = ComputeSummary.fromJson({
      'balanceMicros': 100,
      'spentMicros': 20,
      'rows': [
        {'key': 'old', 'micros': 20, 'count': 2},
      ],
    });
    expect(summary.knownListPriceMicros, isNull);
    expect(summary.unknownCount, isNull);
    expect(summary.rows.single.unpricedCount, isNull);
  });

  test(
    'zero, unpriced and pending are distinct and wire enums are preserved',
    () {
      final known = ComputeCost.fromJson(
        cost({
          'fundingKind': 'ownconnection',
          'pricingStatus': 'known',
          'listPriceMicros': 0,
          'attemptState': 'settled',
          'outcome': 'succeeded',
          'invocationId': 'request-1',
          'providerRequestId': 'provider-1',
          'tariffVersion': '2026-10',
          'chargePolicy': 'everydispatchedattempt',
        }),
      );
      expect(known.listPriceMicros, 0);
      expect(known.fundingKind, 'ownconnection');
      expect(known.outcome, 'succeeded');
      expect(known.invocationId, 'request-1');
      expect(known.providerRequestId, 'provider-1');
      expect(known.tariffVersion, '2026-10');
      expect(known.chargePolicy, 'everydispatchedattempt');
      expect(known.isPending, isFalse);
      final unpriced = ComputeCost.fromJson(
        cost({'pricingStatus': 'unpriced'}),
      );
      expect(unpriced.listPriceMicros, isNull);
      final pending = ComputeCost.fromJson(
        cost({
          'attemptState': 'dispatchstarted',
          'estimatedListPriceMicros': 500,
        }),
      );
      expect(pending.isPending, isTrue);
      expect(pending.micros, 0);
      expect(pending.estimatedListPriceMicros, 500);
    },
  );

  test(
    'trace pages preserve snapshot totals and a missing status is unavailable',
    () {
      final page = ComputeTracePage.fromJson({
        'rows': [cost()],
        'chargedMicros': 10,
        'knownListPriceMicros': 8,
        'unpricedCount': 1,
        'pendingCount': 2,
        'unknownCount': 3,
        'snapshotWatermark': 17,
        'continuationToken': 'next',
        'status': 'incomplete',
      });
      expect(page.rows.single.costId, 'attempt');
      expect(page.chargedMicros, 10);
      expect(page.knownListPriceMicros, 8);
      expect(page.unpricedCount, 1);
      expect(page.pendingCount, 2);
      expect(page.unknownCount, 3);
      expect(page.snapshotWatermark, 17);
      expect(page.continuationToken, 'next');
      expect(page.status, 'incomplete');
      expect(ComputeTracePage.fromJson({}).status, 'detailunavailable');
    },
  );

  test(
    'trace client encodes selectors and continuation and enforces page bounds',
    () async {
      final requests = <Uri>[];
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          requests.add(request.url);
          return http.Response(
            jsonEncode({'rows': [], 'status': 'expired'}),
            200,
          );
        }),
      );
      final page = await client.readComputeTrace(
        'brain/a',
        'trace/b',
        take: 2,
        continuationToken: 'next+/=',
      );
      expect(page.status, 'expired');
      expect(
        requests.single.toString(),
        contains('brain%2Fa/compute/traces/trace%2Fb'),
      );
      expect(requests.single.queryParameters['take'], '2');
      expect(requests.single.queryParameters['continuationToken'], 'next+/=');
      await expectLater(
        client.readComputeTrace('brain', 'trace', take: 0),
        throwsArgumentError,
      );
      await expectLater(
        client.readComputeTrace('brain', 'trace', take: 501),
        throwsArgumentError,
      );
      expect(requests, hasLength(1));
    },
  );
}
