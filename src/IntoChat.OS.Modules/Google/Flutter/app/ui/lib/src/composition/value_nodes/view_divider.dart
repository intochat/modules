import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';

/// Divider value node: 1px hairline; with a label, the label sits centred in
/// muted 13 with a line either side (gap 14).
final class ViewDivider extends StatelessWidget {
  const ViewDivider({super.key, this.label = ''});
  final String label;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final line = Container(height: 1, color: c.hairline);
    if (label.isEmpty) return line;
    return Row(
      children: [
        Expanded(child: line),
        const SizedBox(width: 14),
        Text(label, style: UiType.of(context).bodyMuted.copyWith(fontSize: 13)),
        const SizedBox(width: 14),
        Expanded(child: line),
      ],
    );
  }
}
