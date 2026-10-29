import 'dart:async';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> pumpWindow(
  WidgetTester tester,
  Map<String, Object?> Function() load, {
  Future<void> Function(Map<String, dynamic>)? onAction,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: NeuronView(
          kind: 'surface',
          name: 'window',
          load: (_, _) async => load(),
          onAction: onAction,
        ),
      ),
    ),
  );
}

void main() {
  testWidgets('a previous component failure cannot replace current loading', (
    tester,
  ) async {
    final oldRead = Completer<Map<String, dynamic>>();
    final currentRead = Completer<Map<String, dynamic>>();
    Widget view(String name) => MaterialApp(
      home: Scaffold(
        body: NeuronView(
          kind: 'card',
          name: name,
          load: (_, name) =>
              name == 'old' ? oldRead.future : currentRead.future,
        ),
      ),
    );
    await tester.pumpWidget(view('old'));
    await tester.pumpWidget(view('current'));
    oldRead.completeError(StateError('Old component unavailable'));
    await tester.pump();
    expect(find.text('Could not load'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    currentRead.complete({'title': 'Current component', 'body': 'Loaded'});
    await tester.pumpAndSettle();
    expect(find.text('Current component'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('retry shows loading until the requested component returns', (
    tester,
  ) async {
    final retry = Completer<Map<String, dynamic>>();
    var reads = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'card',
            name: 'retry',
            load: (_, _) {
              if (++reads == 1) return Future.error(StateError('Unavailable'));
              return retry.future;
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Could not load'), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pump();
    expect(reads, 2);
    expect(find.text('Could not load'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    retry.complete({'title': 'Recovered', 'body': 'Available again'});
    await tester.pumpAndSettle();
    expect(find.text('Recovered'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('a throwing loader renders the failed state', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'surface',
            name: 'window',
            load: (_, _) async => throw StateError('nope'),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Could not load'), findsOneWidget);
    expect(find.text('This component could not be loaded.'), findsOneWidget);
  });

  testWidgets('an unknown kind renders the declared fallback, never blank', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'not-a-kind',
            name: 'window',
            load: (_, _) async => {'definition': <String, Object?>{}},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      find.text('${RendererRegistry.fallbackLabel}: not-a-kind'),
      findsOneWidget,
    );
    expect(find.text(RendererRegistry.fallbackExplanation), findsOneWidget);
  });
}
