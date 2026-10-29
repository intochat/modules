import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:intochat_os_ui/src/components/browser/embedded_browser_windows.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final originalEnvironmentFactory = environmentFactory;
  tearDown(() {
    debugDefaultTargetPlatformOverride = null;
    debugBrowserPlatformSupported = null;
    environmentFactory = originalEnvironmentFactory;
  });
  testWidgets('browser composition is explicit on unsupported platforms', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'webbrowser',
            name: 'workspace/app/browser',
            load: (_, _) async => {'title': 'Customer Researcher', 'uri': ''},
            onAction: (_) async =>
                fail('Unsupported browser must not send an attachment'),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(UiWebBrowser), findsOneWidget);
    expect(
      find.text('Browsing requires the Windows desktop app.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets(
    'reconnect replaces the browser session while ordinary rebuilds preserve it',
    (tester) async {
      Future<void> show(String title) async {
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SizedBox(
                width: 360,
                child: NeuronView(
                  kind: 'webbrowser',
                  name: 'workspace/app/browser',
                  load: (_, _) async => {'title': title, 'uri': ''},
                  onAction: (_) async {},
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
      }

      await show('Browser');
      final initial = tester.state(find.byType(EmbeddedBrowser));
      await show('Updated browser');
      expect(tester.state(find.byType(EmbeddedBrowser)), same(initial));
      await tester.tap(find.byTooltip('Reconnect browser'));
      await tester.pumpAndSettle();
      expect(initial.mounted, isFalse);
      expect(tester.state(find.byType(EmbeddedBrowser)), isNot(same(initial)));
      expect(find.byType(UiWebBrowser), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('aPreviewNeuronViewDoesNotMountTheEmbeddedBrowser', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'webbrowser',
            name: 'workspace/app/browser',
            load: (_, _) async => {'title': 'Preview', 'uri': 'http://intent/'},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(UiWebBrowser), findsNothing);
    expect(find.byType(EmbeddedBrowser), findsNothing);
    expect(find.text('Preview'), findsOneWidget);
  });

  testWidgets('aFailedEnvironmentIsReportedAsUnavailable', (tester) async {
    debugBrowserPlatformSupported = true;
    debugDefaultTargetPlatformOverride = TargetPlatform.windows;

    environmentFactory = () async => throw StateError('WebView2 missing');
    final events = <Map<String, dynamic>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'webbrowser',
            name: 'workspace/app/browser',
            load: (_, _) async => {'title': 'Browser', 'uri': 'http://intent/'},
            onAction: (event) async => events.add(event),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final unavailable = events
        .where((event) => event['action'] == 'unavailable')
        .toList();
    expect(unavailable, isNotEmpty);
    expect(unavailable.last['value'] as String, contains('WebView2 missing'));
    debugDefaultTargetPlatformOverride = null;
  });

  testWidgets('aFailedEnvironmentIsRetriedByTheNextBrowser', (tester) async {
    debugBrowserPlatformSupported = true;
    debugDefaultTargetPlatformOverride = TargetPlatform.windows;

    var calls = 0;
    environmentFactory = () async {
      calls++;
      throw StateError('boom');
    };

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'webbrowser',
            name: 'workspace/app/browser',
            load: (_, _) async => {'title': 'Browser', 'uri': 'http://intent/'},
            onAction: (_) async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(calls, 1);
    await tester.tap(find.byTooltip('Reconnect browser'));
    await tester.pumpAndSettle();
    expect(calls, 2);
    debugDefaultTargetPlatformOverride = null;
  });

  testWidgets('browser keeps the observed URL out of its compact header', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: NeuronView(
            kind: 'webbrowser',
            name: 'workspace/app/browser',
            load: (_, _) async => {
              'title': 'Browser',
              'uri': 'http://intended.example/',
              'actualUrl': 'http://observed.example/page',
              'connection': 'attached',
              'error': '',
            },
            onAction: (_) async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('http://observed.example/page'), findsNothing);
    expect(find.text('http://intended.example/'), findsNothing);
    expect(
      tester.widget<EmbeddedBrowser>(find.byType(EmbeddedBrowser)).uri,
      'http://observed.example/page',
    );
  });
}
