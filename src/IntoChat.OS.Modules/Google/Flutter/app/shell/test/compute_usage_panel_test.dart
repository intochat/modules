import 'dart:convert';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/workspace/compute_usage_panel.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

final _recentCosts = [
  {
    'traceId': 'trace-1',
    'costId': 'cost-1',
    'kind': 'model',
    'item': 'gpt-5.4:input',
    'quantity': 0.0012,
    'unit': 'mtok',
    'micros': 1500,
    'synapse': null,
    'app': null,
    'neuron': null,
    'at': '2026-10-10T12:00:00Z',
  },
];

Widget panelWith(IntoChatOSUiClient client, {Listenable? refreshSignal}) =>
    MaterialApp(
      home: Scaffold(
        body: ComputeUsagePanel(
          client: client,
          workspaceId: 'workspace',
          workspaceName: 'My project',
          onClose: () {},
          refreshSignal: refreshSignal,
        ),
      ),
    );

void main() {
  testWidgets(
    'informational rows explain exclusion and complete detail may remain pending',
    (tester) async {
      final informational = {
        ..._recentCosts.single,
        'micros': 0,
        'chargePolicy': 'informational',
      };
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          if (request.url.path.contains('/traces/')) {
            return http.Response(
              jsonEncode({
                'rows': [informational],
                'status': 'complete',
                'pendingCount': 1,
              }),
              200,
            );
          }
          if (request.url.path.endsWith('/recent')) {
            return http.Response(jsonEncode([informational]), 200);
          }
          return http.Response('{"rows":[]}', 200);
        }),
      );
      await tester.pumpWidget(panelWith(client));
      await tester.pumpAndSettle();
      expect(
        find.text('Informational usage · excluded from totals'),
        findsOneWidget,
      );
      await tester.tap(find.byTooltip('View trace trace-1'));
      await tester.pumpAndSettle();
      expect(find.text('Trace detail complete'), findsOneWidget);
      expect(find.text('1 pending'), findsOneWidget);
      expect(find.text('Complete trace'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'personal zero, unpriced usage and pending estimates stay distinct',
    (tester) async {
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          if (request.url.path.endsWith('/recent')) {
            return http.Response(
              jsonEncode([
                {
                  ..._recentCosts.single,
                  'costId': 'personal',
                  'micros': 0,
                  'fundingKind': 'ownconnection',
                  'pricingStatus': 'known',
                  'listPriceMicros': 0,
                  'attemptState': 'settled',
                },
                {
                  ..._recentCosts.single,
                  'costId': 'unpriced',
                  'micros': 0,
                  'pricingStatus': 'unpriced',
                },
                {
                  ..._recentCosts.single,
                  'costId': 'pending',
                  'micros': 0,
                  'pricingStatus': 'known',
                  'attemptState': 'uncertain',
                  'estimatedListPriceMicros': 5000,
                },
              ]),
              200,
            );
          }
          return http.Response(
            jsonEncode({
              'balanceMicros': 0,
              'spentMicros': 0,
              'pendingCount': 1,
              'unpricedCount': 1,
              'rows': [],
            }),
            200,
          );
        }),
      );
      await tester.pumpWidget(panelWith(client));
      await tester.pumpAndSettle();
      expect(
        find.text('Personal connection · platform charge \$0'),
        findsOneWidget,
      );
      expect(find.text('List price \$0'), findsOneWidget);
      expect(find.text('Unpriced provider usage'), findsOneWidget);
      expect(find.text('Pending accounting'), findsOneWidget);
      expect(find.text('Estimated provider price \$0.005'), findsOneWidget);
      expect(find.text('1 pending · 1 unpriced'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'trace paging keeps snapshot totals and shows incomplete detail',
    (tester) async {
      final cursors = <String?>[];
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          if (request.url.path.contains('/traces/')) {
            final cursor = request.url.queryParameters['continuationToken'];
            cursors.add(cursor);
            return http.Response(
              jsonEncode({
                'rows': [
                  {
                    ..._recentCosts.single,
                    'costId': cursor == null ? 'first' : 'second',
                    'item': cursor == null ? 'First attempt' : 'Second attempt',
                  },
                ],
                'chargedMicros': 1500,
                'knownListPriceMicros': 1200,
                'pendingCount': 1,
                'unpricedCount': 2,
                'unknownCount': 3,
                'snapshotWatermark': 42,
                'continuationToken': cursor == null ? 'next+/=' : null,
                'status': 'incomplete',
              }),
              200,
            );
          }
          if (request.url.path.endsWith('/recent')) {
            return http.Response(jsonEncode(_recentCosts), 200);
          }
          return http.Response('{"rows":[]}', 200);
        }),
      );
      await tester.pumpWidget(panelWith(client));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('View trace trace-1'));
      await tester.pumpAndSettle();
      expect(
        find.text('Trace incomplete. Some detail is not available yet.'),
        findsOneWidget,
      );
      expect(find.text('Charged \$0.0015'), findsOneWidget);
      expect(find.text('Known list price \$0.0012'), findsOneWidget);
      expect(find.text('1 pending · 2 unpriced · 3 unknown'), findsOneWidget);
      expect(find.textContaining('First attempt'), findsOneWidget);
      await tester.ensureVisible(find.byKey(const Key('compute-trace-more')));
      await tester.tap(find.byKey(const Key('compute-trace-more')));
      await tester.pumpAndSettle();
      expect(cursors, [null, 'next+/=']);
      expect(find.textContaining('First attempt'), findsOneWidget);
      expect(find.textContaining('Second attempt'), findsOneWidget);
      expect(find.text('Charged \$0.0015'), findsOneWidget);
      expect(find.byKey(const Key('compute-trace-more')), findsNothing);
      expect(find.text('Complete trace'), findsNothing);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );

  for (final entry in {
    'expired': 'Trace detail expired.',
    'detailunavailable': 'Trace detail unavailable for this history.',
  }.entries) {
    testWidgets('trace ${entry.key} never claims complete history', (
      tester,
    ) async {
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          if (request.url.path.contains('/traces/')) {
            return http.Response(
              jsonEncode({'status': entry.key, 'rows': []}),
              200,
            );
          }
          if (request.url.path.endsWith('/recent')) {
            return http.Response(jsonEncode(_recentCosts), 200);
          }
          return http.Response('{"rows":[]}', 200);
        }),
      );
      await tester.pumpWidget(panelWith(client));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('View trace trace-1'));
      await tester.pumpAndSettle();
      expect(find.text(entry.value), findsOneWidget);
      expect(find.text('Complete trace'), findsNothing);
      expect(find.text('Charged \$0'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });
  }

  testWidgets(
    'a changed snapshot refuses the next page without mixing totals or rows',
    (tester) async {
      final client = IntoChatOSUiClient(
        baseUri: Uri.parse('http://test'),
        httpClient: MockClient((request) async {
          if (request.url.path.contains('/traces/')) {
            final next = request.url.queryParameters.containsKey(
              'continuationToken',
            );
            return http.Response(
              jsonEncode({
                'rows': [
                  {
                    ..._recentCosts.single,
                    'item': next
                        ? 'Wrong snapshot row'
                        : 'Original snapshot row',
                  },
                ],
                'chargedMicros': next ? 9000 : 1500,
                'snapshotWatermark': next ? 43 : 42,
                'continuationToken': next ? null : 'next',
                'status': 'complete',
              }),
              200,
            );
          }
          if (request.url.path.endsWith('/recent')) {
            return http.Response(jsonEncode(_recentCosts), 200);
          }
          return http.Response('{"rows":[]}', 200);
        }),
      );
      await tester.pumpWidget(panelWith(client));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('View trace trace-1'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('compute-trace-more')));
      await tester.pumpAndSettle();
      expect(
        find.text('Trace snapshot changed. Refresh trace.'),
        findsOneWidget,
      );
      expect(find.textContaining('Wrong snapshot row'), findsNothing);
      expect(find.textContaining('Original snapshot row'), findsOneWidget);
      expect(find.text('Charged \$0.0015'), findsOneWidget);
      expect(find.text('Charged \$0.009'), findsNothing);
      expect(find.text('Complete trace'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets('panel renders balance, spend, grouped rows and recent costs', (
    tester,
  ) async {
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async {
        if (request.url.path.endsWith('/recent')) {
          return http.Response(jsonEncode(_recentCosts), 200);
        }
        return http.Response(
          jsonEncode({
            'balanceMicros': 3500000,
            'spentMicros': 1500,
            'rows': [
              {'key': 'brain/apps/x/synapses/a.cs', 'micros': 1200, 'count': 3},
              {'key': 'brain/apps/y', 'micros': 300, 'count': 1},
            ],
          }),
          200,
        );
      }),
    );
    await tester.pumpWidget(panelWith(client));
    await tester.pumpAndSettle();
    expect(find.text('Balance'), findsOneWidget);
    expect(find.text('\$3.5'), findsOneWidget);
    expect(find.text('Spent · last 30 days'), findsOneWidget);
    expect(find.text('\$0.0015'), findsNWidgets(2));
    expect(find.text('Spend by app'), findsOneWidget);
    expect(find.text('brain/apps/x/synapses/a.cs'), findsOneWidget);
    expect(find.text('3 calls'), findsOneWidget);
    expect(find.text('\$0.0012'), findsOneWidget);
    expect(find.text('Recent'), findsOneWidget);
    expect(find.textContaining('gpt-5.4:input'), findsOneWidget);
    expect(find.textContaining('· model'), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('switching grouping to Synapse re-reads compute by synapse', (
    tester,
  ) async {
    final bys = <String>[];
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async {
        if (request.url.path.endsWith('/recent')) {
          return http.Response(jsonEncode(<Object>[]), 200);
        }
        final by = request.url.queryParameters['by'];
        bys.add(by!);
        return http.Response(
          jsonEncode({
            'balanceMicros': 0,
            'spentMicros': 900,
            'rows': by == 'synapse'
                ? [
                    {
                      'key': 'brain/apps/x/synapses/a.cs',
                      'micros': 900,
                      'count': 2,
                    },
                  ]
                : [
                    {'key': 'brain/apps/x', 'micros': 900, 'count': 2},
                  ],
          }),
          200,
        );
      }),
    );
    await tester.pumpWidget(panelWith(client));
    await tester.pumpAndSettle();
    expect(find.text('brain/apps/x'), findsOneWidget);
    expect(find.text('brain/apps/x/synapses/a.cs'), findsNothing);
    await tester.tap(find.byKey(const Key('compute-group-synapse')));
    await tester.pumpAndSettle();
    expect(bys, ['app', 'synapse']);
    expect(find.text('Spend by synapse'), findsOneWidget);
    expect(find.text('brain/apps/x/synapses/a.cs'), findsOneWidget);
    expect(find.text('brain/apps/x'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('refresh signal re-reads grouped totals', (tester) async {
    var ready = false;
    final signal = ValueNotifier<int>(0);
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async {
        if (request.url.path.endsWith('/recent')) {
          return http.Response(jsonEncode(<Object>[]), 200);
        }
        return http.Response(
          jsonEncode({
            'balanceMicros': 0,
            'spentMicros': ready ? 200 : 0,
            'rows': ready
                ? [
                    {'key': 'brain/apps/x', 'micros': 200, 'count': 1},
                  ]
                : <Object>[],
          }),
          200,
        );
      }),
    );
    await tester.pumpWidget(panelWith(client, refreshSignal: signal));
    await tester.pumpAndSettle();
    expect(find.text('No usage recorded yet.'), findsOneWidget);
    ready = true;
    signal.value++;
    await tester.pumpAndSettle();
    expect(find.text('brain/apps/x'), findsOneWidget);
    expect(find.text('\$0.0002'), findsNWidgets(2));
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    signal.dispose();
  });

  testWidgets('fetch failure surfaces an error and refresh recovers', (
    tester,
  ) async {
    var fail = true;
    final client = IntoChatOSUiClient(
      baseUri: Uri.parse('http://test'),
      httpClient: MockClient((request) async {
        if (request.url.path.endsWith('/recent')) {
          return http.Response(jsonEncode(<Object>[]), 200);
        }
        if (fail) return http.Response('{"error":"offline"}', 503);
        return http.Response(
          jsonEncode({
            'balanceMicros': 0,
            'spentMicros': 400,
            'rows': [
              {'key': 'brain/apps/x', 'micros': 400, 'count': 1},
            ],
          }),
          200,
        );
      }),
    );
    await tester.pumpWidget(panelWith(client));
    await tester.pumpAndSettle();
    expect(find.textContaining('Could not load usage'), findsOneWidget);
    fail = false;
    await tester.tap(find.byTooltip('Refresh usage'));
    await tester.pumpAndSettle();
    expect(find.text('brain/apps/x'), findsOneWidget);
    expect(find.textContaining('Could not load usage'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
}
