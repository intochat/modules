import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame/game.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

class _Scenes extends SceneClient {
  _Scenes() : super(http.Client(), Uri.parse('http://test'), 'b');
  int reads = 0, revision = 1;
  @override
  Future<SceneState> read(String sceneKey) async {
    reads++;
    return SceneState.fromJson({
      'name': sceneKey,
      'revision': revision,
      'definition': {'nodes': [], 'camera': {}},
    });
  }
}

void main() {
  testWidgets('a nested scene reads its own scene neuron', (tester) async {
    final scenes = _Scenes();
    await tester.pumpWidget(
      MaterialApp(
        home: NeuronView(
          kind: 'surface',
          name: 'b/apps/brainview/surface',
          scenes: scenes,
          load: (_, _) async => {
            'definition': {
              'children': [
                {'kind': 'scene', 'name': 'b/apps/brainview/scenes/brain'},
              ],
            },
          },
        ),
      ),
    );
    await tester.pump();
    await tester.pump();
    await tester.pump();
    expect(
      tester.widget<SceneView>(find.byType(SceneView)).sceneKey,
      'b/apps/brainview/scenes/brain',
    );
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('unwritten scene is observable until composition arrives', (
    tester,
  ) async {
    final scenes = _Scenes()..revision = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: NeuronView(
          kind: 'scene',
          name: 'b/apps/ino/scenes/home',
          scenes: scenes,
          refreshEvery: const Duration(seconds: 1),
          load: (_, _) async => {},
        ),
      ),
    );
    await tester.pump();
    expect(find.text('Preparing this view'), findsOneWidget);
    scenes.revision = 1;
    await tester.pump(const Duration(seconds: 1));
    await tester.pump();
    expect(find.text('Preparing this view'), findsNothing);
    expect(find.byType(SceneView), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('scene fallback refresh reads subsequent snapshots once', (
    tester,
  ) async {
    final scenes = _Scenes();
    await tester.pumpWidget(
      MaterialApp(
        home: NeuronView(
          kind: 'scene',
          name: 'b/apps/ino/scenes/home',
          scenes: scenes,
          refreshEvery: const Duration(seconds: 1),
          load: (_, _) async => throw StateError('Scene uses its typed client'),
        ),
      ),
    );
    await tester.pump();
    await tester.pump();
    expect(scenes.reads, 1);
    scenes.revision = 2;
    await tester.pump(const Duration(seconds: 1));
    await tester.pump();
    expect(scenes.reads, 2);
    expect(
      tester
          .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
          .game
          ?.state
          ?.revision,
      2,
    );
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('scene owns one watch and rereads after its stream reconnects', (
    tester,
  ) async {
    final scenes = _Scenes();
    final streams = <StreamController<void>>[];
    await tester.pumpWidget(
      MaterialApp(
        home: NeuronView(
          kind: 'scene',
          name: 'b/apps/ino/scenes/home',
          scenes: scenes,
          refreshEvery: const Duration(seconds: 1),
          load: (_, _) async => throw StateError('Scene uses its typed client'),
          watch: (_, _) {
            final stream = StreamController<void>();
            streams.add(stream);
            return stream.stream;
          },
        ),
      ),
    );
    await tester.pump();
    await tester.pump();
    expect(streams, hasLength(1));
    scenes.revision = 2;
    streams.single.add(null);
    await tester.pump();
    await tester.pump();
    expect(scenes.reads, 2);
    unawaited(streams.single.close());
    await tester.pump();
    scenes.revision = 3;
    await tester.pump(const Duration(seconds: 1));
    await tester.pump();
    expect(streams, hasLength(2));
    expect(
      tester
          .widget<GameWidget<SceneGame>>(find.byType(GameWidget<SceneGame>))
          .game
          ?.state
          ?.revision,
      3,
    );
    await tester.pumpWidget(const SizedBox());
    for (final stream in streams) {
      unawaited(stream.close());
    }
  });
}
