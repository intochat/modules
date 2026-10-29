import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';

/// A severity-tinted message bar. Severity is `info|success|warning|error`;
/// anything else renders as `info`.
final class UiInfoBar extends StatelessWidget {
  const UiInfoBar({
    super.key,
    required this.severity,
    required this.title,
    required this.body,
    this.onDismiss,
  });

  final String severity;
  final String title;
  final String body;
  final VoidCallback? onDismiss;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final (background, foreground, icon) = switch (severity) {
      'success' => (c.doneBg, c.done, Icons.check_circle_outline),
      'warning' => (c.workingBg, c.working, Icons.warning_amber_outlined),
      'error' => (c.errorBg, c.error, Icons.error_outline),
      _ => (c.accentTint, c.accent, Icons.info_outline),
    };
    return Semantics(
      container: true,
      liveRegion: true,
      child: DecoratedBox(
        key: const Key('ui_info_bar'),
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(8),
        ),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 8, 12),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, size: 20, color: foreground),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (title.isNotEmpty) Text(title, style: type.cardTitle),
                    if (body.isNotEmpty) Text(body, style: type.body),
                  ],
                ),
              ),
              if (onDismiss != null)
                IconButton(
                  tooltip: 'Dismiss',
                  onPressed: onDismiss,
                  icon: Icon(Icons.close, size: 18, color: c.muted),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
