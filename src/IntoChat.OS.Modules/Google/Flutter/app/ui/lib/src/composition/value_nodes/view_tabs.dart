import 'package:flutter/material.dart';

import '../../components/kit/ui_kit_tabs.dart';

/// Tabs value node: button children drawn as the kit tab strip. The active
/// tab is the one child with style 0 (Primary); selecting it dispatches
/// nothing, selecting another dispatches that child's action. Tabs without
/// an action are disabled.
final class ViewTabs extends StatelessWidget {
  const ViewTabs({super.key, required this.node, required this.action});
  final Map node;
  final VoidCallback? Function(Map node) action;

  @override
  Widget build(BuildContext context) {
    final children = [
      for (final child in node['children'] as List? ?? const [])
        if (child is Map) child,
    ];
    final active = children.indexWhere((child) => child['style'] == 0);
    final index = active < 0 ? 0 : active;
    return UiKitTabs(
      labels: [for (final child in children) child['text'] as String? ?? ''],
      index: index,
      // A tab with no action cannot be selected; the active one stays drawn.
      disabled: {
        for (var i = 0; i < children.length; i++)
          if (i != index && action(children[i]) == null) i,
      },
      onChanged: (i) {
        if (i != index) action(children[i])?.call();
      },
    );
  }
}
