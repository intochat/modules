import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum ApprovalState { pending, saving, approved, rejected, error }

/// One change to approve. [newValue] is pre-filled by the chat; the user may correct it.
class ApprovalRow {
  ApprovalRow({
    required this.label,
    required this.oldValue,
    required this.newValue,
    this.amount,
    this.warn = false,
  });

  final String label;
  final String? amount;
  final String oldValue;
  String newValue;
  bool edited = false;

  /// Highlights the old value (for example a date already passed).
  final bool warn;
}

class ApprovalData {
  ApprovalData({
    required this.title,
    required this.summary,
    required this.rows,
    required this.valueLabel,
    this.systemName = 'Salesforce',
    this.labelHeader = 'Record',
    this.amountHeader = 'Amount',
    this.decidedBy = 'Jeff',
    this.decidedAt = '14:32',
    this.errorText,
    this.resultText,
  });

  final String title;
  final String summary;
  final List<ApprovalRow> rows;
  final String labelHeader;
  final String amountHeader;

  /// Name of the field being changed, e.g. "Close date".
  final String valueLabel;

  /// Where the changes are saved, e.g. "Salesforce".
  final String systemName;
  String decidedBy;
  String decidedAt;
  String? errorText;

  /// Shown after approving, e.g. "14 close dates updated in Salesforce".
  String? resultText;

  int get editedCount => rows.where((r) => r.edited).length;
}

/// Approval: everything is pre-filled; the user corrects values in place, then approves.
/// Never a form to fill in.
class ApprovalCard extends StatefulWidget {
  const ApprovalCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onApprove,
    this.onReject,
    this.onRetry,
    this.onEdited,
    this.onEditRow,
    this.visibleRows = 5,
  });

  final CardSource source;
  final ApprovalData data;
  final ApprovalState state;
  final VoidCallback? onApprove;
  final VoidCallback? onReject;
  final VoidCallback? onRetry;
  final VoidCallback? onEdited;
  final void Function(int index, String newValue)? onEditRow;
  final int visibleRows;

  @override
  State<ApprovalCard> createState() => _ApprovalCardState();
}

class _ApprovalCardState extends State<ApprovalCard> {
  bool _all = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    switch (widget.state) {
      case ApprovalState.approved:
        return CardResult(
          source: widget.source,
          tone: ResultTone.done,
          text: 'Approved by ${d.decidedBy}, ${d.decidedAt}',
          detail: d.resultText,
          expanded: _body(c, readOnly: true),
        );
      case ApprovalState.rejected:
        return CardResult(
          source: widget.source,
          tone: ResultTone.rejected,
          text: 'Rejected by ${d.decidedBy}, ${d.decidedAt}',
          detail: 'Nothing was changed',
          expanded: _body(c, readOnly: true),
        );
      default:
        return _body(c, readOnly: widget.state == ApprovalState.saving);
    }
  }

  Widget _body(IcColors c, {required bool readOnly}) {
    final d = widget.data;
    final rows = _all ? d.rows : d.rows.take(widget.visibleRows).toList();
    final hidden = d.rows.length - rows.length;
    final pending = widget.state == ApprovalState.pending;
    return CardShell(
      source: widget.source,
      status: pending
          ? CardChip(
              label: 'Needs your approval',
              fg: c.accent,
              bg: c.accentTint,
              icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            d.title,
            style: IcType.text(16, weight: FontWeight.w600, color: c.textStrong, height: 1.4),
          ),
          const SizedBox(height: 4),
          Text(d.summary, style: IcType.text(13, color: c.textMuted, height: 1.45)),
          const SizedBox(height: 14),
          // Table
          Container(
            decoration: BoxDecoration(
              border: Border.all(color: c.borderHairline),
              borderRadius: BorderRadius.circular(IcRadius.button),
            ),
            child: Column(
              children: [
                _HeaderRow(d: d),
                for (final (idx, r) in rows.indexed)
                  _Row(
                    row: r,
                    readOnly: readOnly || !pending,
                    onEdited: () {
                      setState(() {});
                      widget.onEdited?.call();
                      widget.onEditRow?.call(idx, r.newValue);
                    },
                  ),
                if (hidden > 0)
                  Stop(
                    onActivate: () => setState(() => _all = true),
                    builder: (context, v) => GestureDetector(
                      onTap: () => setState(() => _all = true),
                      child: MouseRegion(
                        cursor: SystemMouseCursors.click,
                        child: Container(
                          height: 36,
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: v.selected ? c.accentTint : null,
                            border: Border(top: BorderSide(color: c.borderHairline)),
                          ),
                          child: Text(
                            'Show all ${d.rows.length}',
                            style: IcType.text(13, weight: FontWeight.w600, color: c.accent),
                          ),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          if (widget.state == ApprovalState.saving) ...[
            const SizedBox(height: 14),
            CardWorking(text: 'Saving to ${d.systemName}…'),
          ] else if (widget.state == ApprovalState.error) ...[
            const SizedBox(height: 14),
            if (d.errorText != null) ...[CardError(text: d.errorText!), const SizedBox(height: 10)],
            PromptOptions(
              options: [
                PromptOption('Try again', icon: LucideIcons.rotateCcw, onTap: widget.onRetry),
                PromptOption('Reject', detail: 'Nothing is changed', onTap: widget.onReject),
              ],
            ),
          ] else if (pending) ...[
            const SizedBox(height: 14),
            PromptOptions(
              options: [
                PromptOption(
                  d.rows.length == 1 ? 'Approve' : 'Approve ${d.rows.length} changes',
                  detail: 'Saves to ${d.systemName}',
                  onTap: widget.onApprove,
                ),
                PromptOption('Reject', detail: 'Nothing is changed', onTap: widget.onReject),
              ],
              note: d.editedCount > 0
                  ? 'You changed ${d.editedCount} ${d.editedCount == 1 ? 'value' : 'values'}.'
                  : 'To change a ${d.valueLabel.toLowerCase()}, select it and press ↵, or click it.',
            ),
          ],
        ],
      ),
    );
  }
}

class _HeaderRow extends StatelessWidget {
  const _HeaderRow({required this.d});

  final ApprovalData d;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final st = IcType.text(12, weight: FontWeight.w600, color: c.textMuted);
    return Container(
      height: 34,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: c.surfaceSunk,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(IcRadius.button - 1)),
      ),
      child: Row(
        children: [
          Expanded(flex: 5, child: Text(d.labelHeader, style: st)),
          if (d.rows.any((r) => r.amount != null))
            Expanded(
              flex: 2,
              child: Text(d.amountHeader, style: st, textAlign: TextAlign.right),
            ),
          const SizedBox(width: 16),
          Expanded(flex: 4, child: Text(d.valueLabel, style: st)),
        ],
      ),
    );
  }
}

class _Row extends StatefulWidget {
  const _Row({required this.row, required this.readOnly, required this.onEdited});

  final ApprovalRow row;
  final bool readOnly;
  final VoidCallback onEdited;

  @override
  State<_Row> createState() => _RowState();
}

class _RowState extends State<_Row> {
  bool _editing = false;
  bool _hover = false;

  /// Started from the keyboard: Return or Esc go back to the card's list.
  bool _byKey = false;
  final _text = TextEditingController();
  final _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _focus.addListener(() {
      if (!_focus.hasFocus && _editing) _commit(back: false);
    });
  }

  @override
  void dispose() {
    _text.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _back() {
    if (_byKey) PromptKeys.maybeOf(context)?.refocus();
    _byKey = false;
  }

  void _start({bool byKey = false}) {
    _byKey = byKey;
    _text.text = widget.row.newValue;
    _text.selection = TextSelection(baseOffset: 0, extentOffset: _text.text.length);
    setState(() => _editing = true);
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  void _commit({bool back = true}) {
    final v = _text.text.trim();
    if (v.isNotEmpty && v != widget.row.newValue) {
      widget.row.newValue = v;
      widget.row.edited = true;
      widget.onEdited();
    }
    setState(() => _editing = false);
    if (back) _back();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final r = widget.row;
    final valueStyle = IcType.text(13, weight: FontWeight.w600, color: r.edited ? c.accent : c.textStrong, tnum: true);
    Widget value;
    if (_editing) {
      value = Focus(
        onKeyEvent: (_, e) {
          if (e is KeyDownEvent && e.logicalKey == LogicalKeyboardKey.escape) {
            setState(() => _editing = false);
            _back();
            return KeyEventResult.handled;
          }
          return KeyEventResult.ignored;
        },
        child: Container(
          height: 26,
          padding: const EdgeInsets.symmetric(horizontal: 6),
          alignment: Alignment.centerLeft,
          decoration: BoxDecoration(
            color: c.surfaceCard,
            borderRadius: BorderRadius.circular(IcRadius.xs),
            border: Border.all(color: c.accent),
            boxShadow: [BoxShadow(color: c.ring, spreadRadius: 2)],
          ),
          child: Field(controller: _text, focusNode: _focus, style: valueStyle, onSubmitted: (_) => _commit()),
        ),
      );
    } else {
      value = Stop(
        onActivate: widget.readOnly ? null : () => _start(byKey: true),
        builder: (context, v) {
          final lit = (_hover || v.selected) && !widget.readOnly;
          return MouseRegion(
            cursor: widget.readOnly ? MouseCursor.defer : SystemMouseCursors.text,
            onEnter: (_) => setState(() => _hover = true),
            onExit: (_) => setState(() => _hover = false),
            child: GestureDetector(
              onTap: widget.readOnly ? null : _start,
              child: SelectionRing(
                on: v.selected,
                radius: IcRadius.xs,
                child: Container(
                  height: 26,
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  decoration: BoxDecoration(
                    color: lit ? c.surfaceSunk : const Color(0x00000000),
                    borderRadius: BorderRadius.circular(IcRadius.xs),
                  ),
                  child: Row(
                    children: [
                      Flexible(
                        child: Text(r.newValue, maxLines: 1, overflow: TextOverflow.ellipsis, style: valueStyle),
                      ),
                      if (lit) ...[const SizedBox(width: 6), Icon(LucideIcons.pencil, size: 12, color: c.textMuted)],
                    ],
                  ),
                ),
              ),
            ),
          );
        },
      );
    }
    return Container(
      constraints: const BoxConstraints(minHeight: 40),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: c.borderHairline)),
      ),
      child: Row(
        children: [
          Expanded(
            flex: 5,
            child: Text(
              r.label,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: IcType.text(13, weight: FontWeight.w500, color: c.textBody),
            ),
          ),
          if (r.amount != null)
            Expanded(
              flex: 2,
              child: Text(
                r.amount!,
                textAlign: TextAlign.right,
                style: IcType.text(13, color: c.textBody, tnum: true),
              ),
            ),
          const SizedBox(width: 16),
          Expanded(
            flex: 4,
            child: Row(
              children: [
                Text(
                  r.oldValue,
                  style: IcType.text(
                    13,
                    color: r.warn ? c.error : c.textMuted,
                    tnum: true,
                  ).copyWith(decoration: TextDecoration.lineThrough, decorationColor: r.warn ? c.error : c.textMuted),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: Icon(LucideIcons.arrowRight, size: 12, color: c.textSubtle),
                ),
                Expanded(child: value),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
