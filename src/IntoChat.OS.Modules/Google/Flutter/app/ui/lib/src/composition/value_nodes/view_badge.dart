import 'package:flutter/material.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../../components/kit/kit_toast.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_icon.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';

/// Badge / TagPill value node unified with `TagPill` (`controls.dart`):
/// 22px height, 6px `xs` radius, `11.5px w600 tnum` text + `12px` status icon.
final class ViewBadge extends StatelessWidget {
  const ViewBadge({
    super.key,
    required this.text,
    required this.tone,
    this.icon,
  });
  final String text;
  final Object? tone;
  final String? icon;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final resolvedTone = UiTone.fromWire(tone);
    final (fill, ink, defaultIcon) = switch (resolvedTone) {
      UiTone.done => (c.doneBg, c.done, LucideIcons.circleCheck),
      UiTone.working => (c.workingBg, c.working, LucideIcons.loaderCircle),
      UiTone.error => (c.errorBg, c.error, LucideIcons.triangleAlert),
      UiTone.neutral => (c.recessed, c.muted, null),
    };
    final glyph = (icon != null ? uiIcon(icon!) : null) ?? defaultIcon;
    return Align(
      alignment: AlignmentDirectional.centerStart,
      widthFactor: 1,
      heightFactor: 1,
      child: Container(
        height: tokens.tagPillHeight,
        padding: const EdgeInsets.symmetric(horizontal: 7),
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(tokens.xs),
          border: Border.all(color: ink.withValues(alpha: 0.25)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (glyph != null) ...[
              Icon(glyph, size: 12, color: ink),
              const SizedBox(width: 4),
            ],
            Flexible(
              child: Text(
                text,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: UiType.textStyle(
                  11.5,
                  weight: FontWeight.w600,
                  color: ink,
                  tnum: true,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
