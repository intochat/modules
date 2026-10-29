import 'dart:collection';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flame_behavior_tree/flame_behavior_tree.dart';

/// The facts a tree's conditions read; the game rewrites them from the node's `blackboard` on each apply.
final class SphereBlackboard extends Blackboard with MapMixin<String, String> {
  @override
  String? operator [](Object? key) =>
      key is String && has(key) ? get<String>(key) : null;
  @override
  void operator []=(String key, String value) => set(key, value);
  @override
  String? remove(Object? key) =>
      key is String ? super.remove(key) as String? : null;
}

/// What a tree acts on. [now] and [frame] advance once per tick, so a task can tell that it ran last frame.
abstract interface class SphereBody {
  double get now;
  int get frame;

  /// Starts the visual effect of a moveTo, scale, fade or pulse task; [params] carries its numbers.
  void begin(String kind, TreeNode params);
}

/// Maps a tree definition onto `behavior_tree` nodes bound to [blackboard] and [body].
BaseNode buildTree(
  BehaviorTreeDef tree,
  Map<String, String> blackboard,
  SphereBody body,
) => _build(tree.root, blackboard, body);

BaseNode _build(TreeNode node, Map<String, String> board, SphereBody body) {
  List<BaseNode> children() => [
    for (final child in node.children) _build(child, board, body),
  ];
  return switch (node.kind) {
    'selector' => Selector(children: children()),
    'sequence' =>
      node.memory
          ? _MemorySequence(children(), body)
          : Sequence(children: children()),
    'repeat' =>
      node.children.length == 1
          ? _Repeat(_build(node.children.single, board, body))
          : _failing(),
    'condition' => Condition(() => board[node.key] == node.value),
    'wait' || 'moveTo' || 'scale' || 'fade' || 'pulse' => _task(node, body),
    _ => _failing(),
  };
}

BaseNode _failing() => Task(() => NodeStatus.failure);

// A task runs from its first tick until [TreeNode.seconds] have passed. A task that was not ticked on the
// previous frame (an earlier branch took over) starts again rather than resuming a stale clock.
BaseNode _task(TreeNode node, SphereBody body) {
  double? started;
  var lastFrame = -1;
  return Task(() {
    if (started == null || body.frame - lastFrame > 1) {
      started = body.now;
      if (node.kind != 'wait') body.begin(node.kind, node);
    }
    lastFrame = body.frame;
    if (body.now - started! >= node.seconds) {
      started = null;
      return NodeStatus.success;
    }
    return NodeStatus.running;
  });
}

final class _Repeat extends BaseNode {
  _Repeat(this._child) {
    setParent(_child);
  }

  final BaseNode _child;

  @override
  void tick() {
    _child.tick();
    if (_child.status != NodeStatus.running) _child.reset();
    status = NodeStatus.running;
  }

  @override
  void reset() {
    _child.reset();
    super.reset();
  }
}

final class _MemorySequence extends BaseNode {
  _MemorySequence(this._children, this._body) {
    _children.forEach(setParent);
  }

  final List<BaseNode> _children;
  final SphereBody _body;
  var _index = 0;
  var _lastFrame = -1;

  @override
  void tick() {
    if (_body.frame - _lastFrame > 1) _index = 0;
    _lastFrame = _body.frame;
    while (_index < _children.length) {
      final child = _children[_index];
      child.tick();
      switch (child.status) {
        case NodeStatus.success:
          _index++;
        case NodeStatus.failure:
          _index = 0;
          status = NodeStatus.failure;
          return;
        case NodeStatus.running || NodeStatus.notStarted:
          status = NodeStatus.running;
          return;
      }
    }
    _index = 0;
    status = NodeStatus.success;
  }

  @override
  void reset() {
    _index = 0;
    for (final child in _children) {
      child.reset();
    }
    super.reset();
  }
}
