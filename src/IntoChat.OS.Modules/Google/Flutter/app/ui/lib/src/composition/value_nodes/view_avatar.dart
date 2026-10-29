import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';

/// Avatar value node: 34 circle, strong fill, page-coloured mono 12 initials.
final class ViewAvatar extends StatelessWidget {
  const ViewAvatar({super.key, required this.initials});
  final String initials;

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    return Align(
      alignment: AlignmentDirectional.centerStart,
      widthFactor: 1,
      heightFactor: 1,
      child: Container(
        width: 34,
        height: 34,
        alignment: Alignment.center,
        decoration: BoxDecoration(color: c.strong, shape: BoxShape.circle),
        child: Text(
          initials,
          maxLines: 1,
          style: UiType.of(context).meta
              .copyWith(fontSize: 12, color: c.page, letterSpacing: 0),
        ),
      ),
    );
  }
}
