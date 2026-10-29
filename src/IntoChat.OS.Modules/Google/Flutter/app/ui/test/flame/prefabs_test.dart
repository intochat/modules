import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame/components.dart' show Vector2;
import 'package:flame_test/flame_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'scene_game_test.dart' show node, scene;

const _items =
    '[{"id":"r1","label":"Routing","kind":"route","progress":0.4},{"id":"b2","label":"timer","kind":"behavior","progress":1}]';

Map<String, Object?> _atOrigin(Map<String, Object?> node) => {
  ...node,
  'transform': {
    'x': 0,
    'y': 0,
    'z': 0,
    'scale': 1,
    'rotation': 0,
    'opacity': 1,
  },
};

void main() {
  final flameTester = FlameTester(SceneGame.new);

  flameTester.testGameWidget(
    'activity_ring lays out one tappable child per item and reports its id',
    setUp: (game, _) async {
      game.apply(
        scene([
          node(
            'ring',
            'activity_ring',
            params: {
              'items': '[{"id":"r1","label":"Routing","kind":"route","progress":0.4},{"id":"b2","label":"timer","kind":"behavior","progress":1}]',
            },
            inputs: ['tap'],
          ),
        ]),
      );
      await game.ready();
    },
    verify: (game, _) async {
      final ring = game.componentFor('ring') as ActivityRing;
      expect(ring.items.length, 2);
      final reports = <String?>[];
      game.onInput = (id, kind, x, y, payload) => reports.add(payload);
      ring.simulateTap('b2');
      expect(reports, ['b2']);
    },
  );

  flameTester.testGameWidget(
    'activity_ring without the tap input reports nothing',
    setUp: (game, _) async {
      game.apply(
        scene([
          node('ring', 'activity_ring', params: {'items': _items}),
        ]),
      );
      await game.ready();
    },
    verify: (game, _) async {
      final ring = game.componentFor('ring') as ActivityRing;
      final reports = <String?>[];
      game.onInput = (id, kind, x, y, payload) => reports.add(payload);
      ring.simulateTap('b2');
      expect(reports, isEmpty);
    },
  );

  flameTester.testGameWidget(
    'a real tap on a ring pill reports its id',
    setUp: (game, _) async {
      game.apply(
        scene([
          _atOrigin(
            node(
              'ring',
              'activity_ring',
              params: {'items': _items, 'radius': '120'},
              inputs: ['tap'],
            ),
          ),
        ]),
      );
      await game.ready();
    },
    verify: (game, tester) async {
      for (var i = 0; i < 90; i++) {
        await tester.pump(const Duration(milliseconds: 16));
      }
      final ring = game.componentFor('ring') as ActivityRing;
      final pill = ring.items.firstWhere((i) => i.entry.id == 'b2');
      final reports = <String?>[];
      game.onInput = (id, kind, x, y, payload) =>
          reports.add('$id:$kind:$payload');
      // The pill's outer edge: outside a box of just 2*radius.
      final at = game.canvasSize / 2 + pill.absoluteCenter + Vector2(0, 10);
      await tester.tapAt(Offset(at.x, at.y));
      // Let the tap recognizer's double-tap window close.
      await tester.pump(const Duration(milliseconds: 500));
      expect(reports, ['ring:tap:b2']);
    },
  );
}
