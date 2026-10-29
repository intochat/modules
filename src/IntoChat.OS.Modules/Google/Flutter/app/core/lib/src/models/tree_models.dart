// Dart mirrors of the Flame module's behavior tree records (camelCase JSON).

final class TreeNode {
  const TreeNode({
    required this.kind,
    this.children = const [],
    this.key,
    this.value,
    this.seconds = 0,
    this.x = 0,
    this.y = 0,
    this.factor = 1,
    this.alpha = 1,
    this.amplitude = 0,
    this.memory = false,
  });

  final String kind;
  final List<TreeNode> children;
  final String? key, value;
  final double seconds, x, y, factor, alpha, amplitude;
  final bool memory;

  factory TreeNode.fromJson(Map<String, dynamic> json) => TreeNode(
    kind: json['kind'] as String,
    children: (json['children'] as List? ?? const [])
        .whereType<Map>()
        .map((e) => TreeNode.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false),
    key: json['key'] as String?,
    value: json['value'] as String?,
    seconds: _number(json['seconds'], 0),
    x: _number(json['x'], 0),
    y: _number(json['y'], 0),
    factor: _number(json['factor'], 1),
    alpha: _number(json['alpha'], 1),
    amplitude: _number(json['amplitude'], 0),
    memory: json['memory'] == true,
  );

  Map<String, Object?> toJson() => {
    'kind': kind,
    'children': children.map((c) => c.toJson()).toList(growable: false),
    'key': key,
    'value': value,
    'seconds': seconds,
    'x': x,
    'y': y,
    'factor': factor,
    'alpha': alpha,
    'amplitude': amplitude,
    'memory': memory,
  };
}

final class BehaviorTreeDef {
  const BehaviorTreeDef(this.root);
  final TreeNode root;

  factory BehaviorTreeDef.fromJson(Map<String, dynamic> json) =>
      BehaviorTreeDef(
        TreeNode.fromJson((json['root'] as Map).cast<String, dynamic>()),
      );

  Map<String, Object?> toJson() => {'root': root.toJson()};
}

double _number(Object? value, double fallback) =>
    (value as num?)?.toDouble() ?? fallback;
