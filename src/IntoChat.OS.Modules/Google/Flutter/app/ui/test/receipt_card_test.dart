import 'package:intochat_os_ui/src/chat/receipt_card.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final receipt = <String, dynamic>{
    'outcome': 'Succeeded',
    'summary': '2 tool calls · 25 rows read',
    'modelCalls': 2,
    'compute': 0.4,
    'computeUsd': 0.004,
    'shadow': true,
    'calls': [
      {
        'appId': 'neurons_call',
        'operation': 'neurons_call',
        'discovered': false,
        'succeeded': true,
      },
    ],
    'touched': [
      {
        'source': 'Active leads',
        'semanticTypeId': 'table',
        'readOnly': true,
        'rowsRead': 25,
      },
    ],
  };

  test('labels name the Compute preview and the sources it touched', () {
    expect(ReceiptCard.computeLabel(receipt), r'0.4 Compute ($0.004)');
    expect(ReceiptCard.callLabels(receipt), ['neurons_call']);
    expect(ReceiptCard.touchedLabels(receipt), ['Active leads · 25 rows']);
  });

  testWidgets('renders what ran, what it touched and the preview price', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ReceiptCard(receipt: receipt)),
      ),
    );
    expect(find.textContaining('What ran'), findsOneWidget);
    expect(find.textContaining('What it touched'), findsOneWidget);
    expect(find.textContaining('preview price, not charged'), findsOneWidget);
  });
}
