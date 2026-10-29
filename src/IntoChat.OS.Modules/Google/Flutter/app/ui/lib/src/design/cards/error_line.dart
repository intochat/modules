import 'package:flutter/widgets.dart';

import '../controls.dart';
import '../copy/ino_copy.dart';
import '../tokens.dart';

/// An error in a session: one line that says what happened, what didn't, and
/// the one thing to do next. Ino is still trying when [InoError.working].
class ErrorLine extends StatelessWidget {
  const ErrorLine({super.key, required this.error, this.onAction, this.width = 560});

  final InoError error;
  final VoidCallback? onAction;
  final double width;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final e = error;
    final fg = e.working ? c.working : c.error;
    return ConstrainedBox(
      constraints: BoxConstraints(maxWidth: width),
      child: Container(
        padding: const EdgeInsets.fromLTRB(14, 10, 10, 10),
        decoration: BoxDecoration(
          color: e.working ? c.workingBg : c.errorBg,
          borderRadius: BorderRadius.circular(IcRadius.card),
          border: Border.all(color: fg.withValues(alpha: 0.35)),
        ),
        child: Row(
          children: [
            SizedBox(width: 18, child: e.working ? const WorkingSpinner(size: 15) : Icon(e.icon, size: 16, color: fg)),
            const SizedBox(width: 10),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Text(
                  e.text,
                  style: IcType.text(14, weight: FontWeight.w500, color: c.textStrong, height: 1.4),
                ),
              ),
            ),
            if (e.action != null) ...[const SizedBox(width: 12), Btn(label: e.action!, small: true, onTap: onAction)],
          ],
        ),
      ),
    );
  }
}
