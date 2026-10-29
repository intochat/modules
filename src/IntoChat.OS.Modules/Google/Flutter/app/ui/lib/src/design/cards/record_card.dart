import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum RecordState { loaded, loading, error }

class RecordField {
  const RecordField(this.label, this.value, {this.warn = false});

  final String label;
  final String value;

  /// Shows the value in the error colour (for example an overdue date).
  final bool warn;
}

class RecordData {
  const RecordData({
    required this.objectLabel,
    required this.name,
    required this.fields,
    this.subtitle,
    this.linkLabel = 'Open in Salesforce',
  });

  final String objectLabel;
  final String name;
  final String? subtitle;
  final List<RecordField> fields;
  final String linkLabel;
}

/// Record card: one CRM record with its key fields.
class RecordCard extends StatelessWidget {
  const RecordCard({
    super.key,
    required this.source,
    required this.data,
    this.state = RecordState.loaded,
    this.onOpen,
    this.onRetry,
  });

  final CardSource source;
  final RecordData data;
  final RecordState state;
  final VoidCallback? onOpen;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = data;
    return CardShell(
      source: source,
      width: 520,
      child: switch (state) {
        RecordState.loading => const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Skeleton(width: 80, height: 10),
            SizedBox(height: 8),
            Skeleton(width: 240, height: 16),
            SizedBox(height: 16),
            Row(
              children: [
                Expanded(child: Skeleton(width: 120)),
                SizedBox(width: 16),
                Expanded(child: Skeleton(width: 120)),
              ],
            ),
            SizedBox(height: 10),
            Row(
              children: [
                Expanded(child: Skeleton(width: 120)),
                SizedBox(width: 16),
                Expanded(child: Skeleton(width: 120)),
              ],
            ),
          ],
        ),
        RecordState.error => CardError(
          text: 'I couldn’t open ${d.name}. It may have been deleted, or you may not have access to it.',
          onRetry: onRetry,
        ),
        RecordState.loaded => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              d.objectLabel,
              style: IcType.text(12, weight: FontWeight.w600, color: c.textMuted),
            ),
            const SizedBox(height: 2),
            Text(
              d.name,
              style: IcType.text(17, weight: FontWeight.w700, color: c.textStrong, height: 1.3),
            ),
            if (d.subtitle != null) ...[
              const SizedBox(height: 2),
              Text(d.subtitle!, style: IcType.text(13, color: c.textMuted)),
            ],
            const SizedBox(height: 14),
            LayoutBuilder(
              builder: (context, box) {
                final w = (box.maxWidth - 16) / 2;
                return Wrap(
                  spacing: 16,
                  runSpacing: 12,
                  children: [
                    for (final f in d.fields)
                      SizedBox(
                        width: w,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(f.label, style: IcType.text(12, color: c.textMuted)),
                            const SizedBox(height: 2),
                            Row(
                              children: [
                                if (f.warn) ...[
                                  Icon(LucideIcons.clockAlert, size: 13, color: c.error),
                                  const SizedBox(width: 5),
                                ],
                                Flexible(
                                  child: Text(
                                    f.value,
                                    style: IcType.text(
                                      14,
                                      weight: FontWeight.w600,
                                      color: f.warn ? c.error : c.textStrong,
                                      tnum: true,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                  ],
                );
              },
            ),
            const SizedBox(height: 14),
            Container(height: 1, color: c.borderHairline),
            const SizedBox(height: 10),
            Align(
              alignment: Alignment.centerLeft,
              child: Stop(
                onActivate: onOpen,
                isDefault: true,
                builder: (context, v) => SelectionRing(
                  on: v.selected,
                  radius: IcRadius.xs,
                  child: MouseRegion(
                    cursor: SystemMouseCursors.click,
                    child: GestureDetector(
                      onTap: onOpen,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              d.linkLabel,
                              style: IcType.text(13, weight: FontWeight.w700, color: c.accent),
                            ),
                            const SizedBox(width: 4),
                            Icon(LucideIcons.arrowUpRight, size: 14, color: c.accent),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      },
    );
  }
}
