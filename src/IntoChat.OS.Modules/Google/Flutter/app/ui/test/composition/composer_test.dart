import 'dart:async';

import 'package:intochat_os_ui/src/chat/ui_composer.dart';
import 'package:intochat_os_ui/src/composition/neuron_view.dart';
import 'package:intochat_os_ui/src/voice_input/ui_voice_input.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Future<void> show(
    WidgetTester tester,
    Map<String, dynamic> state,
    Future<void> Function(Map<String, dynamic>) dispatch,
  ) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SizedBox(
          height: 152,
          child: UiComposer(state: state, dispatch: dispatch),
        ),
      ),
    ),
  );

  testWidgets(
    'edit submit and voice begin carry displayed selection revision',
    (tester) async {
      final events = <Map<String, dynamic>>[];
      await show(tester, {'context': 'a', 'generation': 7, 'draft': ''}, (
        event,
      ) async {
        events.add(event);
      });
      await tester.enterText(find.byType(TextField), 'hello');
      await tester.pump();
      await tester.tap(find.byTooltip('Send'));
      await tester.pump();
      await tester.widget<UiVoiceInput>(find.byType(UiVoiceInput)).onBegin!();
      expect(events.map((event) => event['action']), [
        'edit',
        'submit',
        'voice-begin',
      ]);
      expect(events.map((event) => event['selectionRevision']), [7, 7, 7]);
    },
  );

  testWidgets('ordered queue preserves the latest failed edit after refresh', (
    tester,
  ) async {
    final first = Completer<void>();
    final second = Completer<void>();
    final third = Completer<void>();
    var count = 0;
    var draft = '';
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: NeuronView(
              kind: 'composer',
              name: 'composer',
              load: (_, _) async => {
                'context': 'a',
                'generation': 1,
                'draft': draft,
              },
              onAction: (event) async {
                final completion = [first, second, third][count++];
                await completion.future;
                draft = event['value'] as String;
              },
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'A');
    await tester.enterText(find.byType(TextField), 'AB');
    await tester.enterText(find.byType(TextField), 'A');
    first.complete();
    await tester.pump();
    second.complete();
    await tester.pump();
    third.completeError(StateError('latest edit failed'));
    await tester.pumpAndSettle();
    expect(draft, 'AB');
    final actual = tester
        .widget<TextField>(find.byType(TextField))
        .controller!
        .text;
    await tester.pumpWidget(const SizedBox());
    expect(actual, 'A');
  });
  testWidgets(
    'a failed latest edit survives an older equal-value acknowledgement',
    (tester) async {
      final first = Completer<void>();
      final second = Completer<void>();
      final third = Completer<void>();
      var count = 0;
      Future<void> dispatch(Map<String, dynamic> event) {
        count++;
        return [first, second, third][count - 1].future;
      }

      Widget view(String saved) => MaterialApp(
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: UiComposer(
              state: {'context': 'a', 'draft': saved},
              dispatch: dispatch,
            ),
          ),
        ),
      );
      await tester.pumpWidget(view(''));
      await tester.enterText(find.byType(TextField), 'A');
      await tester.enterText(find.byType(TextField), 'AB');
      await tester.enterText(find.byType(TextField), 'A');
      first.complete();
      await tester.pump();
      second.complete();
      await tester.pump();
      third.completeError(StateError('latest edit failed'));
      await tester.pump();
      await tester.pumpWidget(view('AB'));
      expect(
        tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'A',
      );
    },
  );

  testWidgets('Send retry cannot acknowledge a newer equal draft', (
    tester,
  ) async {
    final retry = Completer<void>();
    var edits = 0;
    final actions = <String>[];
    Future<void> dispatch(Map<String, dynamic> event) async {
      actions.add(event['action'] as String);
      if (event['action'] != 'edit') return;
      edits++;
      if (edits == 2) return retry.future;
      if (edits != 3) throw StateError('save failed');
    }

    await show(tester, {'context': 'a', 'draft': ''}, dispatch);
    await tester.enterText(find.byType(TextField), 'A');
    await tester.pump();
    await tester.tap(find.byTooltip('Send'));
    await tester.enterText(find.byType(TextField), 'AB');
    await tester.enterText(find.byType(TextField), 'A');
    await tester.pump();
    retry.complete();
    await tester.pumpAndSettle();
    await show(tester, {'context': 'a', 'draft': 'AB'}, dispatch);
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'A',
    );
    expect(actions.where((action) => action == 'submit'), isEmpty);
    expect(find.textContaining('save failed'), findsOneWidget);
  });

  testWidgets('older edit failure cannot replace the latest successful save', (
    tester,
  ) async {
    final older = Completer<void>();
    var edits = 0;
    await show(tester, {'context': 'a', 'draft': ''}, (_) async {
      if (++edits == 1) await older.future;
    });
    await tester.enterText(find.byType(TextField), 'A');
    await tester.enterText(find.byType(TextField), 'AB');
    await tester.pump();
    older.completeError(StateError('obsolete failure'));
    await tester.pump();
    expect(find.textContaining('obsolete failure'), findsNothing);
  });

  for (final dispose in [false, true]) {
    testWidgets(
      'Send retry stops after ${dispose ? 'disposal' : 'context change'}',
      (tester) async {
        final retry = Completer<void>();
        var edits = 0;
        final actions = <String>[];
        Future<void> dispatch(Map<String, dynamic> event) async {
          actions.add(event['action'] as String);
          if (event['action'] == 'edit') {
            if (++edits == 1) throw StateError('offline');
            await retry.future;
          }
        }

        await show(tester, {'context': 'a', 'draft': ''}, dispatch);
        await tester.enterText(find.byType(TextField), 'A');
        await tester.pump();
        await tester.tap(find.byTooltip('Send'));
        if (dispose) {
          await tester.pumpWidget(const SizedBox());
        } else {
          await show(tester, {'context': 'b', 'draft': 'B'}, dispatch);
        }
        retry.complete();
        await tester.pumpAndSettle();
        expect(actions.where((action) => action == 'submit'), isEmpty);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets('voice failure remains actionable when its save also fails', (
    tester,
  ) async {
    var offline = true;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SizedBox(
            height: 152,
            child: UiComposer(
              state: const {'context': 'a', 'draft': 'keep my draft'},
              dispatch: (_) async {
                if (offline) throw StateError('connection refused');
              },
            ),
          ),
        ),
      ),
    );
    var voice = tester.widget<UiVoiceInput>(find.byType(UiVoiceInput));
    await expectLater(voice.onBegin!(), throwsStateError);
    await expectLater(
      voice.onCaptureError!('connection refused'),
      throwsStateError,
    );
    await tester.pump();
    expect(
      find.textContaining('try again or type your message'),
      findsOneWidget,
    );
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'keep my draft',
    );
    offline = false;
    voice = tester.widget<UiVoiceInput>(find.byType(UiVoiceInput));
    await voice.onBegin!();
    await tester.pump();
    expect(find.textContaining('try again or type your message'), findsNothing);
  });

  testWidgets('repeat Send before refresh never restores a cleared draft', (
    tester,
  ) async {
    var draft = 'send once';
    var submissions = 0;
    await show(tester, {'context': 'a', 'draft': draft}, (event) async {
      if (event['action'] == 'edit') draft = event['value'] as String;
      if (event['action'] == 'submit') {
        if (event['value'] != draft) throw StateError('Draft changed');
        submissions++;
        draft = '';
      }
    });
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();
    expect(submissions, 1);
    expect(draft, '');
  });

  testWidgets('failed edits survive refresh and Send retries saving first', (
    tester,
  ) async {
    final events = <String>[];
    var offline = true;
    Future<void> dispatch(Map<String, dynamic> event) async {
      events.add(event['action'] as String);
      if (offline) throw StateError('Offline');
    }

    await show(tester, {'context': 'a', 'draft': 'saved'}, dispatch);
    await tester.enterText(find.byType(TextField), 'unsaved work');
    await tester.pumpAndSettle();
    await show(tester, {'context': 'a', 'draft': 'saved'}, dispatch);
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'unsaved work',
    );
    offline = false;
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();
    expect(events, ['edit', 'edit', 'submit']);
  });

  testWidgets('Enter captures the draft once while submission is pending', (
    tester,
  ) async {
    final events = <Map<String, dynamic>>[];
    final pending = Completer<void>();
    await show(tester, {'context': 'conversation-a', 'draft': ''}, (
      event,
    ) async {
      events.add(event);
      if (event['action'] == 'submit') await pending.future;
    });
    await tester.enterText(find.byType(TextField), 'A real question');
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pump();
    expect(events.map((e) => e['action']), ['edit', 'submit']);
    expect(events.last['context'], 'conversation-a');
    expect(events.last['value'], 'A real question');
    expect(events.last['operationId'], matches(RegExp(r'^[0-9a-f-]{36}$')));
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'A real question',
    );
    pending.complete();
    await tester.pumpAndSettle();
    await show(tester, {
      'context': 'conversation-a',
      'draft': '',
    }, (_) async {});
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      '',
    );
  });

  testWidgets('Shift Enter and whitespace never submit', (tester) async {
    final events = <Map<String, dynamic>>[];
    await show(tester, {'context': 'a', 'draft': ''}, (event) async {
      events.add(event);
    });
    await tester.enterText(find.byType(TextField), 'First line');
    await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);
    expect(events.where((e) => e['action'] == 'submit'), isEmpty);
    await tester.enterText(find.byType(TextField), '  \n');
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.tap(find.byTooltip('Send'));
    expect(events.where((e) => e['action'] == 'submit'), isEmpty);
  });

  testWidgets('failed submission keeps editable text and explains the error', (
    tester,
  ) async {
    await show(tester, {'context': 'a', 'draft': 'Keep this'}, (event) async {
      if (event['action'] == 'submit') {
        throw StateError('Connection unavailable');
      }
    });
    await tester.tap(find.byTooltip('Send'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Connection unavailable'), findsOneWidget);
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'Keep this',
    );
    await tester.enterText(find.byType(TextField), 'Keep this edited');
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'Keep this edited',
    );
  });

  for (final width in [360.0, 768.0, 1440.0]) {
    testWidgets(
      'transcription can be cancelled in a short composer at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 320);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.reset);
        final events = <Map<String, dynamic>>[];
        await show(
          tester,
          {
            'context': 'a',
            'draft': 'Existing draft',
            'voice': {'id': 'capture', 'status': 'transcribing'},
          },
          (event) async {
            events.add(event);
          },
        );
        await tester.tap(find.byTooltip('Cancel transcription'));
        expect(events.single['action'], 'voice-cancel');
        expect(events.single['operationId'], 'capture');
        expect(events.where((e) => e['action'] == 'submit'), isEmpty);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
