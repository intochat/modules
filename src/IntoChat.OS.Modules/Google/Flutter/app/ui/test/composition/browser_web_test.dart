@TestOn('browser')
library;

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('web embeds the requested page without a desktop connection', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: UiWebBrowser(
            part: const UiWebBrowserPart(
              name: 'browser',
              title: 'Editor',
              uri: 'https://example.com',
            ),
            connection: 'waiting',
            onConnect: (_, _) async =>
                fail('Web frames cannot attach a desktop session'),
          ),
        ),
      ),
    );
    await tester.pump();
    expect(find.byType(HtmlElementView), findsOneWidget);
    expect(find.text('Waiting for the page...'), findsNothing);
    expect(
      find.text('Browsing requires the Windows desktop app.'),
      findsNothing,
    );
  });

  testWidgets('web refuses a non HTTP page', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: UiWebBrowser(
            part: UiWebBrowserPart(
              name: 'browser',
              title: 'Editor',
              uri: 'javascript:alert(1)',
            ),
          ),
        ),
      ),
    );
    expect(find.byType(HtmlElementView), findsNothing);
    expect(
      find.text('Choose an HTTP or HTTPS page to display.'),
      findsOneWidget,
    );
  });
}
