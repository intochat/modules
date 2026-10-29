import 'package:flutter/material.dart';

import '../../models/ui_part.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';

final class UiExpander extends StatelessWidget {
  const UiExpander({
    super.key,
    required this.part,
    this.onToggle,
    this.children = const [],
  });

  final UiExpanderPart part;
  final VoidCallback? onToggle;

  /// Content shown below the header while [UiExpanderPart.expanded].
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final status = part.expanded ? 'Expanded' : 'Collapsed';
    final header = Semantics(
      button: true,
      label: '$status ${part.header}',
      child: Material(
        key: Key('ui_expander_${part.name}'),
        color: c.recessed,
        borderRadius: BorderRadius.circular(12),
        child: InkWell(
          onTap: onToggle,
          borderRadius: BorderRadius.circular(12),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                Icon(
                  part.expanded ? Icons.expand_less : Icons.expand_more,
                  color: c.muted,
                ),
                const SizedBox(width: 8),
                Expanded(child: Text(part.header, style: type.cardTitle)),
                Text(status, style: type.bodyMuted),
              ],
            ),
          ),
        ),
      ),
    );
    if (!part.expanded || children.isEmpty) return header;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      spacing: 8,
      children: [header, ...children],
    );
  }
}
