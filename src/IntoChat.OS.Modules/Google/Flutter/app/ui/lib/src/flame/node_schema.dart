/// The scene node vocabulary as one schema, mirroring the Flame contracts' `FlameSchema`. The canonical
/// artifact is `docs/superpowers/contracts/flame-schema.json`; `node_schema_test.dart` fails if this mirror
/// drifts from it, and the contracts' `SchemaParityFacts` fails if the C# side drifts from the same file.
library;

enum FlameParamKind { text, number, color, json, choice, boolean }

final class FlameParam {
  const FlameParam(
    this.key,
    this.kind, {
    this.required = false,
    this.choices = const [],
  });

  final String key;
  final FlameParamKind kind;
  final bool required;
  final List<String> choices;
}

final class FlameNodeType {
  const FlameNodeType(this.type, this.parameters);

  final String type;
  final List<FlameParam> parameters;

  FlameParam? param(String key) {
    for (final param in parameters) {
      if (param.key == key) return param;
    }
    return null;
  }
}

final class TreeTask {
  const TreeTask(
    this.kind,
    this.minSeconds,
    this.maxSeconds,
    this.minValue,
    this.maxValue,
  );

  final String kind;
  final double minSeconds, maxSeconds, minValue, maxValue;
}

abstract final class FlameSchema {
  static const List<String> gestures = ['tap', 'drag', 'hover'];
  static const List<String> tweenProperties = [
    'x',
    'y',
    'scale',
    'rotation',
    'opacity',
  ];
  static const List<String> tweenCurves = [
    'linear',
    'easeIn',
    'easeOut',
    'easeInOut',
  ];
  static const List<String> moods = [
    'idle',
    'listening',
    'thinking',
    'acting',
    'speaking',
    'error',
    'disconnected',
  ];

  static const List<String> treeKinds = [
    'selector',
    'sequence',
    'repeat',
    'condition',
    'wait',
    'moveTo',
    'scale',
    'fade',
    'pulse',
  ];
  static const List<String> reservedBlackboardKeys = ['running', 'attention'];
  static const int maxTreeNodes = 256;
  static const int maxTreeDepth = 16;
  static const List<TreeTask> treeTasks = [
    TreeTask('wait', 0, 60, 0, 0),
    TreeTask('moveTo', 0, 10, -4096, 4096),
    TreeTask('scale', 0, 10, 0.1, 4),
    TreeTask('fade', 0, 10, 0, 1),
    TreeTask('pulse', 0, 10, 0, 1),
  ];

  static const List<FlameNodeType> nodes = [
    FlameNodeType('circle', [
      FlameParam('radius', FlameParamKind.number),
      FlameParam('color', FlameParamKind.color),
    ]),
    FlameNodeType('rect', [
      FlameParam('width', FlameParamKind.number),
      FlameParam('height', FlameParamKind.number),
      FlameParam('color', FlameParamKind.color),
      FlameParam('corner', FlameParamKind.number),
    ]),
    FlameNodeType('text', [
      FlameParam('text', FlameParamKind.text, required: true),
      FlameParam('size', FlameParamKind.number),
      FlameParam('color', FlameParamKind.color),
      FlameParam(
        'align',
        FlameParamKind.choice,
        choices: ['left', 'center', 'right'],
      ),
    ]),
    FlameNodeType('sprite', [
      FlameParam('src', FlameParamKind.text, required: true),
      FlameParam('width', FlameParamKind.number),
      FlameParam('height', FlameParamKind.number),
    ]),
    FlameNodeType('path', [
      FlameParam('d', FlameParamKind.text, required: true),
    ]),
    FlameNodeType('particles', [
      FlameParam('count', FlameParamKind.number),
      FlameParam('speed', FlameParamKind.number),
      FlameParam('color', FlameParamKind.color),
    ]),
    FlameNodeType('group', []),
    FlameNodeType('activity_ring', [
      FlameParam('items', FlameParamKind.json),
      FlameParam('radius', FlameParamKind.number),
    ]),
    FlameNodeType('sphere', [
      FlameParam('icon', FlameParamKind.text, required: true),
      FlameParam('title', FlameParamKind.text),
      FlameParam('description', FlameParamKind.text),
      FlameParam('blackboard', FlameParamKind.json),
      FlameParam('side', FlameParamKind.json),
      FlameParam('main', FlameParamKind.json),
      FlameParam('toolbar', FlameParamKind.json),
      FlameParam(
        'state',
        FlameParamKind.choice,
        choices: ['ready', 'unready', 'error'],
      ),
      FlameParam('error', FlameParamKind.text),
      FlameParam('neuron', FlameParamKind.text),
      FlameParam('focused', FlameParamKind.boolean),
    ]),
  ];

  static FlameNodeType? forType(String type) {
    for (final node in nodes) {
      if (node.type == type) return node;
    }
    return null;
  }
}
