import 'dart:async';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'typing then Save and Fork sends the edited name before either action',
    (tester) async {
      final edit = Completer<void>();
      final received = <String>[];
      var savedName = 'customer-researcher-copy';
      var revision = 1;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'layout',
              name: 'root',
              load: (kind, name) async => switch (kind) {
                'layout' => {
                  'mode': 'list',
                  'children': [
                    {'kind': 'form', 'name': 'copy'},
                    {'kind': 'button', 'name': 'fork'},
                  ],
                },
                'form' => {
                  'revision': revision,
                  'definition': {
                    'title': 'Your copy',
                    'revision': revision,
                    'fields': [
                      {
                        'name': 'name',
                        'label': 'App name',
                        'kind': 'PlainText',
                        'value': savedName,
                        'supported': true,
                      },
                    ],
                  },
                },
                _ => {'label': 'Fork', 'enabled': true, 'action': 'install'},
              },
              onAction: (event) async {
                if (event['field'] == 'name') {
                  received.add('draft');
                  await edit.future;
                  savedName = event['value'] as String;
                  revision++;
                } else {
                  if (event['action'] == 'submit') {
                    expect(
                      event['revision'],
                      revision,
                      reason: 'Submit keeps optimistic concurrency after own drafts',
                    );
                    revision++;
                  }
                  received.add('${event['kind']}:$savedName');
                }
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextFormField), 'customer-researcher');
      await tester.tap(find.text('Save'));
      await tester.tap(find.text('Fork'));
      await tester.pump();
      expect(received, ['draft']);
      edit.complete();
      await tester.pumpAndSettle();
      expect(received, [
        'draft',
        'form:customer-researcher',
        'button:customer-researcher',
      ]);
      await tester.pumpWidget(const SizedBox());
    },
  );
  testWidgets(
    'Save retains the displayed revision when another writer changes the form',
    (tester) async {
      var revision = 7;
      int? submittedRevision;
      var accepted = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: NeuronView(
              kind: 'form',
              name: 'copy',
              load: (_, _) async => {
                'title': 'Your copy',
                'revision': revision,
                'fields': [
                  {
                    'name': 'name',
                    'label': 'App name',
                    'kind': 'PlainText',
                    'value': 'copy',
                    'supported': true,
                  },
                ],
              },
              onAction: (event) async {
                submittedRevision = event['revision'] as int;
                accepted = submittedRevision == revision;
              },
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      revision++; // A different session changes the server without this view observing it.
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(submittedRevision, 7);
      expect(accepted, isFalse);
      await tester.pumpWidget(const SizedBox());
    },
  );
}
