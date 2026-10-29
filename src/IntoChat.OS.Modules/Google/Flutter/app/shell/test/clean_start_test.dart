import 'dart:typed_data';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('declared voice input sends audio to its neuron', (tester) async {
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'voiceinput',
            name: 'voice',
            load: (_, _) async => {'label': 'Dictate'},
            onAction: (event) async => events.add(event),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byTooltip('Dictate'), findsOneWidget);
    await tester.widget<UiVoiceInput>(find.byType(UiVoiceInput)).onAudio!(
      Uint8List.fromList([1, 2, 3]),
      'audio/wav',
    );
    await tester.pump();
    expect(events.single, {
      'kind': 'voiceinput',
      'name': 'voice',
      'value': 'AQID',
      'mimeType': 'audio/wav',
    });
    await tester.pumpWidget(const SizedBox());
  });
}
