import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum ChangeState { pending, applying, applied, cancelled, error }

/// One field that changes. [newValue] is pre-filled; you correct it in place.
class ChangeField {
  ChangeField({required this.label, required this.oldValue, required this.newValue, this.editable = true});

  final String label;

  /// Null when the field is empty today.
  final String? oldValue;
  String newValue;
  final bool editable;
  bool edited = false;
}

class ChangeData {
  ChangeData({
    required this.objectLabel,
    required this.name,
    required this.fields,
    required this.systemName,
    this.subtitle,
    this.appliedBy = 'Jeff',
    this.appliedAt = '14:40',
    this.errorText,
  });

  /// e.g. "Opportunity".
  final String objectLabel;
  final String name;
  final String? subtitle;
  final List<ChangeField> fields;

  /// Where the change lands, e.g. "Salesforce".
  final String systemName;
  String appliedBy;
  String appliedAt;
  String? errorText;

  /// Every field is empty today: the change creates the record.
  bool get isNew => fields.every((f) => f.oldValue == null);
}

/// Change preview: what will change on one record, before and after. Ino
/// pre-fills every new value; you click one to correct it, then apply.
class ChangePreviewCard extends StatelessWidget {
  const ChangePreviewCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onApply,
    this.onCancel,
    this.onRetry,
    this.onEdited,
  });

  final CardSource source;
  final ChangeData data;
  final ChangeState state;
  final VoidCallback? onApply;
  final VoidCallback? onCancel;
  final VoidCallback? onRetry;
  final VoidCallback? onEdited;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = data;
    final n = d.fields.length;
    final changes = '$n ${n == 1 ? 'change' : 'changes'}';
    switch (state) {
      case ChangeState.applied:
        return CardResult(
          source: source,
          tone: ResultTone.done,
          text: '${d.isNew ? 'Created' : 'Updated'} ${d.name}',
          detail: d.isNew
              ? '${d.objectLabel} in ${d.systemName}, by ${d.appliedBy} ${d.appliedAt}'
              : '$changes in ${d.systemName}, by ${d.appliedBy} ${d.appliedAt}',
          expanded: _Body(data: d, readOnly: true, onEdited: () {}),
        );
      case ChangeState.cancelled:
        return CardResult(
          source: source,
          tone: ResultTone.rejected,
          text: d.isNew ? 'Not created' : 'Not changed',
          detail: d.name,
        );
      case ChangeState.pending:
      case ChangeState.applying:
      case ChangeState.error:
        final applying = state == ChangeState.applying;
        final error = state == ChangeState.error;
        return CardShell(
          source: source,
          status: switch (state) {
            ChangeState.applying => CardChip(
              label: 'Saving',
              fg: c.working,
              bg: c.workingBg,
              icon: const WorkingSpinner(size: 12),
            ),
            ChangeState.error => CardChip(
              label: 'Not saved',
              fg: c.error,
              bg: c.errorBg,
              icon: Icon(LucideIcons.triangleAlert, size: 13, color: c.error),
            ),
            _ => CardChip(
              label: 'Needs your answer',
              fg: c.accent,
              bg: c.accentTint,
              icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
            ),
          },
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                d.objectLabel.toUpperCase(),
                style: IcType.text(11, weight: FontWeight.w700, color: c.textMuted),
              ),
              const SizedBox(height: 2),
              Text(
                d.name,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
              ),
              if (d.subtitle != null) Text(d.subtitle!, style: IcType.text(13, color: c.textMuted)),
              const SizedBox(height: 8),
              Text(
                error
                    ? d.errorText ?? 'Nothing was saved.'
                    : d.isNew
                    ? 'New ${d.objectLabel.toLowerCase()} in ${d.systemName}. Select a value to correct it.'
                    : '$changes in ${d.systemName}. Select a new value to correct it.',
                style: IcType.text(
                  13.5,
                  weight: error ? FontWeight.w500 : FontWeight.w400,
                  color: error ? c.error : c.textBody,
                  height: 1.4,
                ),
              ),
              const SizedBox(height: 10),
              _Body(data: d, readOnly: applying, onEdited: onEdited ?? () {}),
              const SizedBox(height: 14),
              if (applying)
                CardWorking(text: 'Saving in ${d.systemName}…')
              else
                PromptOptions(
                  options: [
                    if (error)
                      PromptOption('Try again', icon: LucideIcons.rotateCcw, onTap: onRetry)
                    else
                      PromptOption(
                        d.isNew ? 'Create' : 'Apply $changes',
                        detail: d.isNew ? 'Creates it in ${d.systemName}' : 'Saves to ${d.systemName}',
                        onTap: onApply,
                      ),
                    PromptOption(
                      'Cancel',
                      detail: d.isNew ? 'Nothing is created' : 'Nothing is changed',
                      onTap: onCancel,
                    ),
                  ],
                  note: _editedLine(d).isEmpty ? null : _editedLine(d),
                ),
            ],
          ),
        );
    }
  }

  String _editedLine(ChangeData d) {
    final e = d.fields.where((f) => f.edited).length;
    return e == 0 ? '' : 'You corrected $e';
  }
}

class _Body extends StatefulWidget {
  const _Body({required this.data, required this.readOnly, required this.onEdited});

  final ChangeData data;
  final bool readOnly;
  final VoidCallback onEdited;

  @override
  State<_Body> createState() => _BodyState();
}

class _BodyState extends State<_Body> {
  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    Widget head(String t, {int flex = 4}) => Expanded(
      flex: flex,
      child: Text(
        t,
        style: IcType.text(11.5, weight: FontWeight.w600, color: c.textMuted),
      ),
    );
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(IcRadius.button),
        border: Border.all(color: c.borderHairline),
      ),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
            child: Row(children: [head('Field', flex: 3), head('Now'), head('After', flex: 5)]),
          ),
          for (final f in widget.data.fields)
            _FieldRow(
              field: f,
              readOnly: widget.readOnly || !f.editable,
              onEdited: () {
                setState(() {});
                widget.onEdited();
              },
            ),
        ],
      ),
    );
  }
}

class _FieldRow extends StatefulWidget {
  const _FieldRow({required this.field, required this.readOnly, required this.onEdited});

  final ChangeField field;
  final bool readOnly;
  final VoidCallback onEdited;

  @override
  State<_FieldRow> createState() => _FieldRowState();
}

class _FieldRowState extends State<_FieldRow> {
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

  void _back() {
    if (_byKey) PromptKeys.maybeOf(context)?.refocus();
    _byKey = false;
  }

  @override
  void dispose() {
    _text.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _start({bool byKey = false}) {
    _byKey = byKey;
    _text.text = widget.field.newValue;
    _text.selection = TextSelection(baseOffset: 0, extentOffset: _text.text.length);
    setState(() => _editing = true);
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  void _commit({bool back = true}) {
    final v = _text.text.trim();
    if (v.isNotEmpty && v != widget.field.newValue) {
      widget.field
        ..newValue = v
        ..edited = true;
      widget.onEdited();
    }
    setState(() => _editing = false);
    if (back) _back();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final f = widget.field;
    final style = IcType.text(13, weight: FontWeight.w600, color: f.edited ? c.accent : c.textStrong, tnum: true);
    final Widget after;
    if (_editing) {
      after = Focus(
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
          child: Field(controller: _text, focusNode: _focus, style: style, onSubmitted: (_) => _commit()),
        ),
      );
    } else {
      after = Stop(
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
                  constraints: const BoxConstraints(minHeight: 26),
                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                  decoration: BoxDecoration(
                    color: lit ? c.surfaceSunk : const Color(0x00000000),
                    borderRadius: BorderRadius.circular(IcRadius.xs),
                  ),
                  child: Row(
                    children: [
                      Flexible(child: Text(f.newValue, style: style)),
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
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        border: Border(top: BorderSide(color: c.borderHairline)),
      ),
      child: Row(
        children: [
          Expanded(
            flex: 3,
            child: Text(
              f.label,
              style: IcType.text(13, weight: FontWeight.w500, color: c.textBody),
            ),
          ),
          Expanded(
            flex: 4,
            child: Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Text(
                f.oldValue ?? 'Empty',
                style: f.oldValue == null
                    ? IcType.text(13, color: c.textSubtle)
                    : IcType.text(
                        13,
                        color: c.textMuted,
                        tnum: true,
                      ).copyWith(decoration: TextDecoration.lineThrough, decorationColor: c.textMuted),
              ),
            ),
          ),
          Expanded(flex: 5, child: after),
        ],
      ),
    );
  }
}
