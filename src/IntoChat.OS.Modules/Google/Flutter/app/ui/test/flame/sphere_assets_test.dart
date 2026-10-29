import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';
import 'dart:ui';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame/game.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

SceneState assetScene(String icon) => SceneState(
  name: 'assets',
  revision: 1,
  definition: SceneDefinition(
    nodes: [
      SceneNode(
        id: 'app',
        type: 'sphere',
        parameters: {'icon': icon, 'neuron': 'brain/apps/app'},
      ),
    ],
  ),
);
final svgBytes = Uint8List.fromList(
  utf8.encode(
    '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24"><rect width="24" height="24" fill="#ff0000"/></svg>',
  ),
);

void main() {
  testWidgets('a presentation that is not ready draws a dimmed sphere', (
    tester,
  ) async {
    final game = SceneGame();
    game.apply(
      const SceneState(
        name: 'assets',
        revision: 1,
        definition: SceneDefinition(
          nodes: [
            SceneNode(
              id: 'app',
              type: 'sphere',
              parameters: {'icon': 'folder', 'state': 'unready'},
            ),
          ],
        ),
      ),
    );
    await tester.pumpWidget(GameWidget(game: game));
    await tester.pumpAndSettle();
    final sphere = game.componentFor('app') as SphereComponent;
    final alpha = await tester.runAsync(() async {
      final recorder = PictureRecorder();
      sphere.body.render(Canvas(recorder));
      final picture = recorder.endRecording();
      final image = await picture.toImage(96, 96);
      final data = await image.toByteData();
      final alpha = data!.getUint8((48 * 96 + 10) * 4 + 3);
      image.dispose();
      picture.dispose();
      return alpha;
    });
    expect(alpha, lessThan(150));
    expect(alpha, greaterThan(0));
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets(
    'manifest SVG bytes load through the injected loader and draw at sphere size',
    (tester) async {
      final requests = <String>[];
      final game = SceneGame()
        ..loadIcon = (neuron, icon) async {
          requests.add('$neuron:$icon');
          return svgBytes;
        };
      game.apply(assetScene('icon.svg'));
      await tester.pumpWidget(GameWidget(game: game));
      await tester.pumpAndSettle();
      final sphere = game.componentFor('app') as SphereComponent;
      await tester.runAsync(() => sphere.iconReady);
      await tester.pump();
      expect(requests, ['brain/apps/app:icon.svg']);
      expect(sphere.body.iconImage, isNotNull);
      expect(sphere.body.iconImage!.width, 96);
      final pixels = await tester.runAsync(
        () => sphere.body.iconImage!.toByteData(),
      );
      expect(pixels!.getUint8((95 * 96 + 95) * 4 + 3), 255);
      await tester.pumpWidget(const SizedBox());
    },
  );
  testWidgets(
    'stale and removed sphere icon reads never replace the current asset',
    (tester) async {
      final first = Completer<Uint8List?>(), next = Completer<Uint8List?>();
      final game = SceneGame()
        ..loadIcon = (_, icon) =>
            icon == 'first.svg' ? first.future : next.future;
      game.apply(assetScene('first.svg'));
      await tester.pumpWidget(GameWidget(game: game));
      await tester.pumpAndSettle();
      final sphere = game.componentFor('app') as SphereComponent;
      final oldRead = sphere.iconReady;
      game.apply(assetScene('next.svg'));
      final newRead = sphere.iconReady;
      await tester.runAsync(() async {
        first.complete(svgBytes);
        await oldRead;
      });
      expect(sphere.body.iconImage, isNull);
      game.apply(
        const SceneState(
          name: 'assets',
          revision: 2,
          definition: SceneDefinition(),
        ),
      );
      await tester.pump();
      await tester.runAsync(() async {
        next.complete(svgBytes);
        await newRead;
      });
      expect(sphere.body.iconImage, isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
  testWidgets(
    'named icons skip bundle loading and a failed file read retains fallback',
    (tester) async {
      var reads = 0;
      final game = SceneGame()
        ..loadIcon = (_, _) async {
          reads++;
          throw StateError('missing');
        };
      game.apply(assetScene('folder'));
      await tester.pumpWidget(GameWidget(game: game));
      await tester.pumpAndSettle();
      expect(reads, 0);
      game.apply(assetScene('missing.svg'));
      final sphere = game.componentFor('app') as SphereComponent;
      await tester.runAsync(() => sphere.iconReady);
      expect(reads, 1);
      expect(sphere.body.iconImage, isNull);
      expect(sphere.body.icon, 'missing.svg');
      await tester.pumpWidget(const SizedBox());
    },
  );
}
