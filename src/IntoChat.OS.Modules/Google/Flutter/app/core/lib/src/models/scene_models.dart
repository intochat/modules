// Dart mirrors of the Flame module's scene records (camelCase JSON).

import 'tree_models.dart';

List<Map<String, dynamic>> _objects(Object? value) =>
    (value as List? ?? const [])
        .whereType<Map>()
        .map((e) => e.cast<String, dynamic>())
        .toList(growable: false);

Map<String, String> _strings(Object? value) =>
    (value as Map? ?? const {}).map((key, value) => MapEntry('$key', '$value'));

Map<String, dynamic>? _object(Object? value) =>
    value is Map ? value.cast<String, dynamic>() : null;

double _number(Object? value, double fallback) =>
    (value as num?)?.toDouble() ?? fallback;

final class SceneTransform {
  const SceneTransform({
    this.x = 0,
    this.y = 0,
    this.z = 0,
    this.scale = 1,
    this.rotation = 0,
    this.opacity = 1,
  });
  final double x, y, z, scale, rotation, opacity;

  factory SceneTransform.fromJson(Map<String, dynamic> json) => SceneTransform(
    x: _number(json['x'], 0),
    y: _number(json['y'], 0),
    z: _number(json['z'], 0),
    scale: _number(json['scale'], 1),
    rotation: _number(json['rotation'], 0),
    opacity: _number(json['opacity'], 1),
  );
}

final class SceneTween {
  const SceneTween({
    required this.property,
    required this.from,
    required this.to,
    required this.milliseconds,
    this.curve = 'easeInOut',
    this.repeat = false,
  });
  final String property, curve;
  final double from, to;
  final int milliseconds;
  final bool repeat;

  factory SceneTween.fromJson(Map<String, dynamic> json) => SceneTween(
    property: json['property'] as String,
    from: _number(json['from'], 0),
    to: _number(json['to'], 0),
    milliseconds: (json['milliseconds'] as num?)?.toInt() ?? 0,
    curve: json['curve'] as String? ?? 'easeInOut',
    repeat: json['repeat'] == true,
  );
}

final class SceneNode {
  const SceneNode({
    required this.id,
    required this.type,
    this.transform = const SceneTransform(),
    this.style,
    this.parameters = const {},
    this.children = const [],
    this.tweens = const [],
    this.inputs = const [],
    this.tree,
  });
  final String id, type;
  final SceneTransform transform;
  final String? style;
  final Map<String, String> parameters;
  final List<SceneNode> children;
  final List<SceneTween> tweens;
  final List<String> inputs;
  final BehaviorTreeDef? tree;

  factory SceneNode.fromJson(Map<String, dynamic> json) {
    final transform = _object(json['transform']);
    final tree = _object(json['tree']);
    return SceneNode(
      id: json['id'] as String,
      type: json['type'] as String,
      transform: transform == null
          ? const SceneTransform()
          : SceneTransform.fromJson(transform),
      style: json['style'] as String?,
      parameters: _strings(json['parameters']),
      children: _objects(json['children'])
          .map(SceneNode.fromJson)
          .toList(growable: false),
      tweens: _objects(json['tweens'])
          .map(SceneTween.fromJson)
          .toList(growable: false),
      inputs: (json['inputs'] as List? ?? const [])
          .map((e) => '$e')
          .toList(growable: false),
      tree: tree == null ? null : BehaviorTreeDef.fromJson(tree),
    );
  }
}

final class SceneCamera {
  const SceneCamera({this.x = 0, this.y = 0, this.zoom = 1});
  final double x, y, zoom;

  factory SceneCamera.fromJson(Map<String, dynamic> json) => SceneCamera(
    x: _number(json['x'], 0),
    y: _number(json['y'], 0),
    zoom: _number(json['zoom'], 1),
  );
}

final class SceneDefinition {
  const SceneDefinition({
    this.nodes = const [],
    this.camera = const SceneCamera(),
    this.background = '#0B0F14',
  });
  final List<SceneNode> nodes;
  final SceneCamera camera;
  final String background;

  factory SceneDefinition.fromJson(Map<String, dynamic> json) {
    final camera = _object(json['camera']);
    return SceneDefinition(
      nodes: _objects(json['nodes'])
          .map(SceneNode.fromJson)
          .toList(growable: false),
      camera: camera == null
          ? const SceneCamera()
          : SceneCamera.fromJson(camera),
      background: json['background'] as String? ?? '#0B0F14',
    );
  }
}

final class SceneState {
  const SceneState({
    required this.name,
    required this.revision,
    required this.definition,
  });
  final String name;
  final int revision;
  final SceneDefinition definition;

  factory SceneState.fromJson(Map<String, dynamic> json) {
    final definition = _object(json['definition']);
    return SceneState(
      name: json['name'] as String? ?? '',
      revision: (json['revision'] as num?)?.toInt() ?? 0,
      definition: definition == null
          ? const SceneDefinition()
          : SceneDefinition.fromJson(definition),
    );
  }
}
