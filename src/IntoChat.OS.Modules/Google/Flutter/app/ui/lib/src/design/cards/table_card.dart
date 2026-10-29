import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum TableState { loading, ready, empty, error }

class TableColumn {
  const TableColumn(this.label, {this.numeric = false, this.date = false, this.flex = 3});

  final String label;

  /// Right aligned, tabular figures. Sorts by the number in the text.
  final bool numeric;

  /// Day and month like "12 Aug". Sorts by date.
  final bool date;
  final int flex;
}

const _months = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];

/// The value a cell sorts by: a number for numeric and date columns, else the
/// lowercased text.
Comparable<Object> _sortValue(String text, TableColumn col) {
  if (col.numeric) return double.tryParse(text.replaceAll(RegExp(r'[^0-9.\-]'), '')) ?? 0;
  if (col.date) {
    final m = RegExp(r'(\d+)\s+([A-Za-z]{3})').firstMatch(text);
    if (m == null) return 0;
    return _months.indexOf(m.group(2)!.toLowerCase()) * 40 + int.parse(m.group(1)!);
  }
  return text.toLowerCase();
}

class TableData {
  const TableData({
    required this.title,
    required this.columns,
    required this.rows,
    required this.from,
    this.emptyText = 'Nothing matches.',
    this.errorText = 'Something stopped the lookup. Nothing changed.',
    this.sourceNeuronUri,
  });

  final String title;
  final List<TableColumn> columns;
  final List<List<String>> rows;

  /// Where the rows come from, e.g. "Salesforce".
  final String from;
  final String emptyText;
  final String errorText;
  final String? sourceNeuronUri;
}

/// Canonical alias for [TablePreviewCard].
typedef TableCard = TablePreviewCard;

/// Table preview: rows Ino looked up, a few at a time. Click a column head to
/// sort every row in the card (numbers and dates biggest first, text A to Z;
/// again reverses, a third time goes back to Ino's order). Open in Sheets for
/// anything more.
class TablePreviewCard extends StatefulWidget {
  const TablePreviewCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onOpen,
    this.onDownload,
    this.onRetry,
    this.onSort,
    this.onFilter,
    this.onCopyNeuronUri,
    this.visibleRows = 6,
  });

  final CardSource source;
  final TableData data;
  final TableState state;
  final VoidCallback? onOpen;
  final VoidCallback? onDownload;
  final VoidCallback? onRetry;
  final void Function(int? columnIndex, bool descending)? onSort;
  final ValueChanged<String>? onFilter;
  final ValueChanged<String>? onCopyNeuronUri;
  final int visibleRows;

  @override
  State<TablePreviewCard> createState() => _TablePreviewCardState();
}

class _TablePreviewCardState extends State<TablePreviewCard> {
  bool _all = false;
  int? _sortCol;
  bool _desc = false;

  void _sortBy(int i) {
    final col = widget.data.columns[i];
    final firstDesc = col.numeric || col.date;
    setState(() {
      if (_sortCol != i) {
        _sortCol = i;
        _desc = firstDesc;
      } else if (_desc == firstDesc) {
        _desc = !_desc;
      } else {
        _sortCol = null;
      }
    });
    widget.onSort?.call(_sortCol, _desc);
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    final n = d.rows.length;
    final sorted = List.of(d.rows);
    final sc = _sortCol;
    if (sc != null && sc < d.columns.length) {
      final col = d.columns[sc];
      sorted.sort((a, b) {
        final av = sc < a.length ? a[sc] : '';
        final bv = sc < b.length ? b[sc] : '';
        final x = _sortValue(av, col).compareTo(_sortValue(bv, col));
        return _desc ? -x : x;
      });
    }
    final shown = _all ? sorted : sorted.take(widget.visibleRows).toList();

    Widget cell(String text, TableColumn col, {bool head = false}) => Expanded(
      flex: col.flex,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6),
        child: Text(
          text,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: col.numeric ? TextAlign.right : TextAlign.left,
          style: head
              ? IcType.text(11.5, weight: FontWeight.w600, color: c.textMuted)
              : IcType.text(13, color: c.textStrong, tnum: col.numeric),
        ),
      ),
    );

    Widget head(int i, TableColumn col) {
      final on = _sortCol == i;
      final label = Text(
        col.label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: IcType.text(
          11.5,
          weight: on ? FontWeight.w700 : FontWeight.w600,
          color: on ? c.textStrong : c.textMuted,
        ),
      );
      return Expanded(
        flex: col.flex,
        child: Stop(
          onActivate: () => _sortBy(i),
          builder: (context, v) => GestureDetector(
            onTap: () => _sortBy(i),
            child: MouseRegion(
              cursor: SystemMouseCursors.click,
              child: SelectionRing(
                on: v.selected,
                radius: IcRadius.xs,
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  child: Row(
                    mainAxisAlignment: col.numeric ? MainAxisAlignment.end : MainAxisAlignment.start,
                    children: [
                      Flexible(child: label),
                      if (on) ...[
                        const SizedBox(width: 3),
                        Icon(_desc ? LucideIcons.arrowDown : LucideIcons.arrowUp, size: 12, color: c.textStrong),
                      ],
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      );
    }

    final Widget body = switch (widget.state) {
      TableState.loading => Column(
        children: [
          for (var i = 0; i < 4; i++)
            Container(
              height: 34,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
              decoration: BoxDecoration(
                border: i == 0 ? null : Border(top: BorderSide(color: c.borderHairline)),
              ),
              child: Row(
                children: [
                  for (final w in const [0.38, 0.22, 0.14])
                    Expanded(
                      child: FractionallySizedBox(
                        alignment: Alignment.centerLeft,
                        widthFactor: w / 0.38,
                        child: Container(
                          margin: const EdgeInsets.only(right: 16),
                          decoration: BoxDecoration(
                            color: c.surfaceSunk,
                            borderRadius: BorderRadius.circular(IcRadius.xs),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
        ],
      ),
      TableState.empty => Padding(
        padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 12),
        child: Row(
          children: [
            Icon(LucideIcons.searchX, size: 16, color: c.textMuted),
            const SizedBox(width: 10),
            Expanded(
              child: Text(d.emptyText, style: IcType.text(14, color: c.textBody)),
            ),
          ],
        ),
      ),
      TableState.error => Padding(
        padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 12),
        child: Row(
          children: [
            Icon(LucideIcons.triangleAlert, size: 16, color: c.error),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                d.errorText,
                style: IcType.text(14, weight: FontWeight.w500, color: c.textStrong),
              ),
            ),
            Btn(label: 'Try again', small: true, onTap: widget.onRetry),
          ],
        ),
      ),
      TableState.ready => Column(
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
            color: c.surfaceSunk,
            child: Row(children: [for (final (i, col) in d.columns.indexed) head(i, col)]),
          ),
          for (final r in shown)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
              decoration: BoxDecoration(
                border: Border(top: BorderSide(color: c.borderHairline)),
              ),
              child: Row(
                children: [
                  for (final (i, col) in d.columns.indexed)
                    cell(i < r.length ? r[i] : '', col),
                ],
              ),
            ),
          if (n > widget.visibleRows)
            Stop(
              onActivate: () => setState(() => _all = !_all),
              builder: (context, v) => GestureDetector(
                onTap: () => setState(() => _all = !_all),
                child: MouseRegion(
                  cursor: SystemMouseCursors.click,
                  child: Container(
                    width: double.infinity,
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
                    decoration: BoxDecoration(
                      color: v.selected ? c.accentTint : null,
                      border: Border(top: BorderSide(color: c.borderHairline)),
                    ),
                    child: Text(
                      _all ? 'Show fewer' : 'Show all $n',
                      style: IcType.text(13, weight: FontWeight.w600, color: c.accent, tnum: true),
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    };

    return CardShell(
      source: widget.source,
      status: widget.state == TableState.loading
          ? CardChip(label: 'Looking up', fg: c.working, bg: c.workingBg, icon: const WorkingSpinner(size: 12))
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Expanded(
                child: Text(
                  d.title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
                ),
              ),
              if (widget.state == TableState.ready)
                Text(
                  '$n ${n == 1 ? 'row' : 'rows'} from ${d.from}',
                  style: IcType.text(12.5, color: c.textMuted, tnum: true),
                ),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(IcRadius.button),
            child: Container(
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(IcRadius.button),
                border: Border.all(color: c.borderHairline),
              ),
              child: body,
            ),
          ),
          if (widget.state == TableState.ready) ...[
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                if (d.sourceNeuronUri != null) ...[
                  Btn(
                    label: 'Copy URI',
                    icon: LucideIcons.copy,
                    small: true,
                    onTap: () {
                      final uri = d.sourceNeuronUri!;
                      Clipboard.setData(ClipboardData(text: uri));
                      widget.onCopyNeuronUri?.call(uri);
                    },
                  ),
                  const SizedBox(width: 8),
                ],
                Btn(label: 'Download CSV', icon: LucideIcons.download, small: true, onTap: widget.onDownload),
                const SizedBox(width: 8),
                Btn(label: 'Open in Sheets', icon: LucideIcons.grid3x3, small: true, onTap: widget.onOpen),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
