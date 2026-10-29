import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('collection entries and breadcrumbs dispatch domain activation', (
    tester,
  ) async {
    final actions = <Map<String, dynamic>>[];
    final artifacts = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'collection',
            name: 'brain/apps/files/listing',
            load: (_, _) async => {
              'revision': 7,
              'definition': {
                'items': [
                  {
                    'id': 'folder',
                    'label': 'Documents',
                    'kind': 'folder',
                    'canActivate': true,
                  },
                ],
                'breadcrumbs': [
                  {'id': 'storages', 'label': 'Storages'},
                  {'id': 'pc', 'label': 'PC'},
                ],
              },
            },
            onAction: (event) async => actions.add(event),
            onActivate: artifacts.add,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Documents'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Storages'));
    await tester.pumpAndSettle();
    expect(actions.map((event) => event['value']), ['folder', 'storages']);
    for (final event in actions) {
      expect(event['kind'], 'collection');
      expect(event['name'], 'brain/apps/files/listing');
      expect(event['action'], 'activate');
      expect(event['revision'], 7);
    }
    expect(artifacts, isEmpty);
    await tester.pumpWidget(const SizedBox());
  });
}
