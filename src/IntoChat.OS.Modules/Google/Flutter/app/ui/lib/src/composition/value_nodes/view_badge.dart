import 'package:flutter/material.dart';

import '../../components/kit/kit_toast.dart';
import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';

/// Badge value node: pill, 4x7 padding, mono 11. Tone 1 Done, 2 Working,
/// 3 Error, otherwise Neutral (recessed fill, muted text).
final class ViewBadge extends StatelessWidget {
  const ViewBadge({super.key, required this.text, required this.tone});
  final String text;
  final Object? tone;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final (fill, ink) = switch (UiTone.fromWire(tone)) {
      UiTone.done => (c.doneBg, c.strong),
      UiTone.working => (c.workingBg, c.strong),
      UiTone.error => (c.errorBg, c.strong),
      UiTone.neutral => (c.recessed, c.muted),
    };
    return Align(
      alignment: AlignmentDirectional.centerStart,
      widthFactor: 1,
      heightFactor: 1,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(UiTokens.of(context).pill),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 4),
          child: Text(
            text,
            style: UiType.of(context).meta
                .copyWith(fontSize: 11, color: ink, height: 1),
          ),
        ),
      ),
    );
  }
}
