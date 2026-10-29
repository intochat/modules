import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:test/test.dart';

void main() {
  test('a scene state round-trips the wire shape the C# neuron serializes', () {
    final json = {
      'name': 'owner/apps/ino/scenes/home',
      'revision': 3,
      'definition': {
        'nodes': [
          {
            'id': 'sphere',
            'type': 'ino_sphere',
            'transform': {
              'x': 0,
              'y': 0,
              'z': 0,
              'scale': 1,
              'rotation': 0,
              'opacity': 1,
            },
            'style': null,
            'parameters': {'mood': 'calm'},
            'children': [],
            'tweens': [],
            'inputs': ['tap'],
            'tree': {
              'root': {
                'kind': 'selector',
                'children': [
                  {
                    'kind': 'sequence',
                    'children': [
                      {
                        'kind': 'condition',
                        'children': [],
                        'key': 'mood',
                        'value': 'busy',
                      },
                      {
                        'kind': 'pulse',
                        'children': [],
                        'seconds': 1.5,
                        'amplitude': 0.2,
                      },
                    ],
                    'memory': true,
                  },
                  {
                    'kind': 'repeat',
                    'children': [
                      {
                        'kind': 'moveTo',
                        'children': [],
                        'seconds': 2,
                        'x': 10,
                        'y': -5,
                      },
                    ],
                  },
                ],
              },
            },
          },
        ],
        'camera': {'x': 0, 'y': 0, 'zoom': 1},
        'background': '#0B0F14',
      },
    };
    final state = SceneState.fromJson(json);
    expect(state.revision, 3);
    expect(state.definition.nodes.single.type, 'ino_sphere');
    final root = state.definition.nodes.single.tree!.root;
    expect(root.kind, 'selector');
    expect(root.children[0].memory, isTrue);
    expect(root.children[0].children[0].key, 'mood');
    expect(root.children[0].children[1].amplitude, 0.2);
    expect(root.children[1].children.single.x, 10);
    expect(root.children[1].children.single.y, -5);
    expect(root.children[1].children.single.factor, 1);
  });

  test('a node without a tree has none', () {
    final node = SceneNode.fromJson({
      'id': 'a',
      'type': 'circle',
      'tree': null,
    });
    expect(node.tree, isNull);
  });
}
