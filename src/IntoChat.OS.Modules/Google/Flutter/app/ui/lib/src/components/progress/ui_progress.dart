import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';

/// A linear progress bar; [value] is clamped to 0..1 and ignored when
/// [determinate] is false.
final class UiProgress extends StatelessWidget {
  const UiProgress({
    super.key,
    required this.determinate,
    required this.value,
    required this.label,
  });

  final bool determinate;
  final double value;
  final String label;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final fraction = value.isNaN ? 0.0 : value.clamp(0.0, 1.0);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        if (label.isNotEmpty) ...[
          Text(label, style: UiType.of(context).bodyMuted),
          const SizedBox(height: 8),
        ],
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: LinearProgressIndicator(
            value: determinate ? fraction : null,
            minHeight: 8,
            color: c.accent,
            backgroundColor: c.recessed,
            semanticsLabel: label.isEmpty ? null : label,
          ),
        ),
      ],
    );
  }
}
