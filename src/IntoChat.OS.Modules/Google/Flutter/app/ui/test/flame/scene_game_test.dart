import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame_test/flame_test.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter_test/flutter_test.dart';

SceneState scene(List<Map<String, Object?>> nodes) => SceneState.fromJson({
  'name': 's',
  'revision': 1,
  'definition': {
    'nodes': nodes,
    'camera': {'x': 0, 'y': 0, 'zoom': 1},
    'background': '#000000',
  },
});

Map<String, Object?> node(
  String id,
  String type, {
  Map<String, String> params = const {},
  List<String> inputs = const [],
}) => {
  'id': id,
  'type': type,
  'transform': {
    'x': 10,
    'y': 10,
    'z': 0,
    'scale': 1,
    'rotation': 0,
    'opacity': 1,
  },
  'style': '#ffffff',
  'parameters': params,
  'children': [],
  'tweens': [],
  'inputs': inputs,
};

void main() {
  final flameTester = FlameTester(SceneGame.new);

  flameTester.testGameWidget(
    'content updates preserve an entrance in progress',
    setUp: (game, _) async {
      game.appearanceDuration = const Duration(milliseconds: 400);
      game.apply(scene([]));
      await game.ready();
      game.apply(scene([node('a', 'circle')]));
      await game.ready();
      game.update(.1);
      final component = game.componentFor('a')!;
      final opacity = component.opacity;
      final y = component.position.y;
      expect(opacity, inExclusiveRange(0, 1));
      game.apply(
        scene([
          node('a', 'circle', params: {'radius': '40'}),
        ]),
      );
      expect(component.opacity, opacity);
      expect(component.position.y, y);
      game.update(.3);
      expect(component.opacity, closeTo(1, .001));
      expect(component.position.y, closeTo(10, .001));
    },
    verify: (game, _) async {},
  );

  flameTester.testGameWidget(
    'new nodes appear once while initial and identical snapshots stay still',
    setUp: (game, _) async {
      game.appearanceDuration = const Duration(milliseconds: 400);
      game.apply(scene([node('a', 'circle')]));
      await game.ready();
      expect(game.componentFor('a')!.opacity, 1);
      game.apply(scene([node('a', 'circle'), node('b', 'circle')]));
      await game.ready();
      expect(game.componentFor('b')!.opacity, 0);
      game.update(.2);
      expect(game.componentFor('b')!.opacity, greaterThan(0));
      expect(game.componentFor('b')!.opacity, lessThan(1));
      game.apply(scene([node('a', 'circle'), node('b', 'circle')]));
      game.update(.4);
      expect(game.componentFor('b')!.opacity, 1);
      expect(game.componentFor('b')!.position.y, 10);
    },
    verify: (game, _) async {},
  );

  flameTester.testGameWidget(
    'reduced motion snaps an appearing node to its final state',
    setUp: (game, _) async {
      game.apply(scene([node('a', 'circle')]));
      await game.ready();
      game.apply(scene([node('a', 'circle'), node('b', 'circle')]));
      await game.ready();
      game.animations = false;
      game.host();
      expect(game.componentFor('b')!.opacity, 1);
      expect(game.componentFor('b')!.position.y, 10);
    },
    verify: (game, _) async {},
  );

  flameTester.testGameWidget(
    'primitive nodes become components keyed by id',
    setUp: (game, _) async {
      game.apply(
        scene([
          node('a', 'circle'),
          node('b', 'rect'),
          node('c', 'text', params: {'text': 'hi'}),
        ]),
      );
      await game.ready();
    },
    verify: (game, _) async {
      expect(game.componentFor('a'), isNotNull);
      expect(game.componentFor('b'), isNotNull);
      expect(game.componentFor('c'), isNotNull);
    },
  );

  flameTester.testGameWidget(
    'an unknown node type renders a placeholder and the siblings',
    setUp: (game, _) async {
      game.apply(scene([node('a', 'circle'), node('x', 'hologram')]));
      await game.ready();
    },
    verify: (game, _) async {
      expect(game.componentFor('a'), isNotNull);
      expect(game.componentFor('x'), isA<PlaceholderNode>());
    },
  );

  flameTester.testGameWidget(
    'a later apply removes nodes that left and updates the rest',
    setUp: (game, _) async {
      game.apply(scene([node('a', 'circle'), node('b', 'rect')]));
      await game.ready();
      game.apply(
        scene([
          node('a', 'circle', params: {'radius': '40'}),
        ]),
      );
      await game.ready();
    },
    verify: (game, _) async {
      expect(game.componentFor('b'), isNull);
      expect(game.componentFor('a')!.params['radius'], '40');
    },
  );

  flameTester.testGameWidget(
    'a tap on a node with the tap input reports it',
    setUp: (game, _) async {
      game.apply(
        scene([
          node('a', 'circle', inputs: ['tap']),
        ]),
      );
      await game.ready();
    },
    verify: (game, tester) async {
      final reports = <String>[];
      game.onInput = (nodeId, kind, x, y, payload) =>
          reports.add('$nodeId:$kind');
      game.simulateTap('a');
      expect(reports, ['a:tap']);
    },
  );

  flameTester.testGameWidget(
    'applying the same state again keeps a node tween running',
    setUp: (game, _) async {
      SceneState pulsing() => scene([
        {
          ...node('a', 'circle'),
          'tweens': [
            {
              'property': 'scale',
              'from': 1,
              'to': 2,
              'milliseconds': 500,
              'curve': 'linear',
              'repeat': true,
            },
          ],
        },
      ]);
      game.apply(pulsing());
      await game.ready();
      game.apply(pulsing());
      await game.ready();
    },
    verify: (game, _) async {
      expect(game.componentFor('a')!.restarts, 1);
    },
  );

  flameTester.testGameWidget(
    'a parent that changes type keeps its child, and a moved child follows its new parent',
    setUp: (game, _) async {
      Map<String, Object?> parent(
        String id,
        String type,
        List<Map<String, Object?>> children,
      ) => {...node(id, type), 'children': children};
      game.apply(
        scene([
          parent('p', 'group', [node('c', 'circle')]),
          parent('q', 'group', [node('d', 'rect')]),
        ]),
      );
      await game.ready();
      game.apply(
        scene([
          parent('p', 'rect', [node('c', 'circle')]),
          parent('q', 'group', []),
          parent('r', 'group', [node('d', 'rect')]),
        ]),
      );
      await game.ready();
    },
    verify: (game, _) async {
      expect(game.componentFor('p'), isA<RectNode>());
      expect(game.componentFor('c')!.parent, same(game.componentFor('p')));
      expect(game.componentFor('c')!.isMounted, isTrue);
      expect(game.componentFor('d')!.parent, same(game.componentFor('r')));
      expect(game.componentFor('d')!.isMounted, isTrue);
    },
  );

  flameTester.testGameWidget(
    'a drag keeps the engine awake while it moves',
    setUp: (game, _) async {
      game.apply(
        scene([
          {
            ...node('a', 'rect', inputs: ['drag']),
            'transform': {
              'x': 0,
              'y': 0,
              'z': 0,
              'scale': 1,
              'rotation': 0,
              'opacity': 1,
            },
          },
        ]),
      );
      await game.ready();
    },
    verify: (game, tester) async {
      final center = game.canvasSize / 2;
      final gesture = await tester.startGesture(
        Offset(center.x, center.y),
        kind: PointerDeviceKind.mouse,
      );
      await gesture.moveBy(const Offset(20, 0));
      for (var i = 0; i < 6; i++) {
        await tester.pump(const Duration(milliseconds: 16));
      }
      expect(game.paused, isTrue);
      await gesture.moveBy(const Offset(20, 0));
      expect(game.paused, isFalse);
      await gesture.up();
    },
  );
}
