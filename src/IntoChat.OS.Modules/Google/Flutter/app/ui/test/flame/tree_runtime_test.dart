import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flame_behavior_tree/flame_behavior_tree.dart';
import 'package:flutter_test/flutter_test.dart';

final class FakeBody implements SphereBody {
  @override
  double now = 0;
  @override
  int frame = 0;
  final begun = <TreeNode>[];

  @override
  void begin(String kind, TreeNode params) => begun.add(params);

  List<TreeNode> of(String kind) =>
      begun.where((task) => task.kind == kind).toList();
}

TreeNode condition(String key, String value) =>
    TreeNode(kind: 'condition', key: key, value: value);
TreeNode wait(double seconds) => TreeNode(kind: 'wait', seconds: seconds);
TreeNode moveTo(double x, double y, double seconds) =>
    TreeNode(kind: 'moveTo', x: x, y: y, seconds: seconds);
TreeNode fade(double alpha, double seconds) =>
    TreeNode(kind: 'fade', alpha: alpha, seconds: seconds);
TreeNode sequence(List<TreeNode> children, {bool memory = false}) =>
    TreeNode(kind: 'sequence', children: children, memory: memory);
TreeNode selector(List<TreeNode> children) =>
    TreeNode(kind: 'selector', children: children);
TreeNode repeat(TreeNode child) => TreeNode(kind: 'repeat', children: [child]);

BaseNode build(TreeNode root, Map<String, String> board, FakeBody body) =>
    buildTree(BehaviorTreeDef(root), board, body);

void tick(BaseNode root, FakeBody body, [double advance = 0]) {
  body.now += advance;
  body.frame++;
  root.tick();
}

void main() {
  // robot: selector( sequence(charge=low, moveTo(0,0,1), wait(1)), sequence(task=patrol, repeat(moveTo(100,0,2))) )
  final robot = selector([
    sequence([condition('charge', 'low'), moveTo(0, 0, 1), wait(1)]),
    sequence([condition('task', 'patrol'), repeat(moveTo(100, 0, 2))]),
  ]);

  test('a selector picks the first branch whose condition holds', () {
    final body = FakeBody();
    final board = {'charge': 'low', 'task': 'patrol'};
    final root = build(robot, board, body);
    tick(root, body);
    expect(body.begun.map((task) => (task.x, task.y)), [(0.0, 0.0)]);
    expect(root.status, NodeStatus.running);

    board['charge'] = 'full';
    tick(root, body, 0.1);
    expect(body.begun.last.x, 100);
  });

  test('a selector fails when no branch holds', () {
    final body = FakeBody();
    final root = build(robot, {'charge': 'full', 'task': 'idle'}, body);
    tick(root, body);
    expect(root.status, NodeStatus.failure);
    expect(body.begun, isEmpty);
  });

  test('a memory sequence resumes its running child on the next tick', () {
    // traffic: sequence(memory, fade(1,0.5), wait(2), fade(0.2,0.5), wait(1))
    final body = FakeBody();
    final root = build(
      sequence([fade(1, 0.5), wait(2), fade(0.2, 0.5), wait(1)], memory: true),
      {},
      body,
    );
    tick(root, body);
    expect(body.of('fade'), hasLength(1));
    tick(root, body, 0.6);
    tick(root, body, 0.5);
    tick(root, body, 0.5);
    expect(body.of('fade'), hasLength(1), reason: 'the wait is still running');
    tick(root, body, 1.5);
    expect(body.of('fade').map((task) => task.alpha), [1, 0.2]);
  });

  test('a sequence without memory starts over each tick', () {
    final body = FakeBody();
    final root = build(sequence([fade(1, 0.5), wait(2)]), {}, body);
    tick(root, body);
    tick(root, body, 0.6);
    tick(root, body, 0.1);
    expect(body.of('fade'), hasLength(2));
  });

  test('repeat ticks its child again after success', () {
    final body = FakeBody();
    final root = build(repeat(moveTo(100, 0, 2)), {}, body);
    tick(root, body);
    expect(body.begun, hasLength(1));
    tick(root, body, 1);
    expect(body.begun, hasLength(1));
    tick(root, body, 1);
    expect(root.status, NodeStatus.running);
    tick(root, body, 0.1);
    expect(body.begun, hasLength(2));
  });

  test(
    'a task preempted by an earlier branch starts fresh when it returns',
    () {
      final body = FakeBody();
      final board = {'charge': 'low', 'task': 'patrol'};
      final root = build(robot, board, body);
      tick(root, body);
      board['charge'] = 'full';
      tick(root, body, 0.1);
      board['charge'] = 'low';
      tick(root, body, 0.1);
      expect(body.begun.where((task) => task.x == 0), hasLength(2));
    },
  );

  test('an unknown kind renders as a no-op leaf that fails', () {
    final body = FakeBody();
    final root = build(const TreeNode(kind: 'spin'), {}, body);
    tick(root, body);
    expect(root.status, NodeStatus.failure);
    expect(body.begun, isEmpty);
  });
}
