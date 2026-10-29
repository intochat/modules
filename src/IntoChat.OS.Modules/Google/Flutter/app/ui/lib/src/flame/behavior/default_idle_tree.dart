import 'package:intochat_os_flutter/intochat_os_flutter.dart';

/// What a sphere runs when its node carries no tree: pulse while the neuron runs, pulse gently when it
/// wants attention, otherwise idle.
BehaviorTreeDef defaultIdleTree() => const BehaviorTreeDef(
  TreeNode(
    kind: 'selector',
    children: [
      TreeNode(
        kind: 'sequence',
        children: [
          TreeNode(kind: 'condition', key: 'running', value: 'true'),
          TreeNode(kind: 'pulse', amplitude: 0.08, seconds: 1.2),
        ],
      ),
      TreeNode(
        kind: 'sequence',
        children: [
          TreeNode(kind: 'condition', key: 'attention', value: 'true'),
          TreeNode(kind: 'pulse', amplitude: 0.04, seconds: 0.6),
        ],
      ),
      TreeNode(
        kind: 'repeat',
        children: [TreeNode(kind: 'wait', seconds: 1)],
      ),
    ],
  ),
);
