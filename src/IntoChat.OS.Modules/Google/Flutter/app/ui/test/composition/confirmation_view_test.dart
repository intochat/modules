import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

void main() {
  testWidgets('confirmation card dispatches to its runtime inbox', (
    tester,
  ) async {
    final sent = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'confirmation',
            name: 'owner/confirmations',
            load: (_, _) async => {
              'generation': 4,
              'version': 4,
              'root': {
                'kind': 4,
                'key': 'cf-1',
                'text': 'Delete report.txt?',
                'children': [
                  {
                    'kind': 0,
                    'key': 'expiry',
                    'text': 'This request expires in 5 minutes.',
                  },
                  {
                    'kind': 1,
                    'key': 'approve',
                    'text': 'Delete',
                    'action': 'Resolve:cf-1:true',
                  },
                  {
                    'kind': 1,
                    'key': 'deny',
                    'text': 'Keep',
                    'action': 'Resolve:cf-1:false',
                  },
                ],
              },
            },
            onAction: (event) async => sent.add(event),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Delete report.txt?'), findsOneWidget);
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();
    expect(sent.single, containsPair('kind', 'confirmation'));
    expect(sent.single, containsPair('name', 'owner/confirmations'));
    expect(sent.single, containsPair('action', 'Resolve:cf-1:true'));
    expect(sent.single, containsPair('revision', 4));
    await tester.pumpWidget(const SizedBox());
  });
}
