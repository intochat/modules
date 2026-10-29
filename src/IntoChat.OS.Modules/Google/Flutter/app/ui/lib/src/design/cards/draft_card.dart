import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum DraftState { pending, sending, sent, scheduled, discarded, error }

enum DraftChannel { email, slack, sms }

extension DraftChannelX on DraftChannel {
  IconData get icon => switch (this) {
    DraftChannel.email => LucideIcons.mail,
    DraftChannel.slack => LucideIcons.hash,
    DraftChannel.sms => LucideIcons.smartphone,
  };

  String get noun => switch (this) {
    DraftChannel.email => 'Email',
    DraftChannel.slack => 'Slack message',
    DraftChannel.sms => 'SMS',
  };
}

class DraftData {
  DraftData({
    required this.channel,
    required this.to,
    required this.body,
    required this.from,
    this.subject,
    this.cc,
    this.language,
    this.scheduleLabel,
    this.sentAt = '14:48',
    this.errorText,
  });

  final DraftChannel channel;

  /// Who it goes to, e.g. "Ingrid Solberg" or "#support".
  String to;
  String? cc;

  /// Email only.
  String? subject;

  /// Pre-filled by Ino in your voice; you correct it in place.
  String body;

  /// The account it sends from, e.g. "jeff@nordlys.no".
  final String from;

  /// Shown when the draft is not in your usual language, e.g. "German".
  final String? language;

  /// Offers "Send <label>" next to Send, e.g. "tomorrow 08:00".
  final String? scheduleLabel;
  String sentAt;
  String? errorText;
  bool edited = false;
}

/// Draft: an email, Slack message or SMS that Ino wrote for you. Every part
/// is pre-filled; click the recipient, subject or text to correct it, then
/// send, schedule or discard. Ino never sends without this click.
class DraftCard extends StatefulWidget {
  const DraftCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onSend,
    this.onSchedule,
    this.onDiscard,
    this.onRetry,
    this.onEdited,
  });

  final CardSource source;
  final DraftData data;
  final DraftState state;
  final VoidCallback? onSend;
  final VoidCallback? onSchedule;
  final VoidCallback? onDiscard;
  final VoidCallback? onRetry;
  final VoidCallback? onEdited;

  @override
  State<DraftCard> createState() => _DraftCardState();
}

class _DraftCardState extends State<DraftCard> {
  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    final what = '${d.channel.noun} to ${d.to}';
    switch (widget.state) {
      case DraftState.sent:
        return CardResult(
          source: widget.source,
          tone: ResultTone.done,
          text: '$what sent, ${d.sentAt}',
          detail: d.subject ?? _firstLine(d.body),
          expanded: _body(c, readOnly: true),
        );
      case DraftState.scheduled:
        return CardResult(
          source: widget.source,
          tone: ResultTone.done,
          text: '$what sends ${d.scheduleLabel}',
          detail: d.subject ?? _firstLine(d.body),
          expanded: _body(c, readOnly: true),
        );
      case DraftState.discarded:
        return CardResult(
          source: widget.source,
          tone: ResultTone.rejected,
          text: 'Draft discarded',
          detail: 'Nothing was sent',
          expanded: _body(c, readOnly: true),
        );
      default:
        return _body(c, readOnly: widget.state == DraftState.sending);
    }
  }

  static String _firstLine(String s) => s.split('\n').firstWhere((l) => l.trim().isNotEmpty, orElse: () => s);

  Widget _body(IcColors c, {required bool readOnly}) {
    final d = widget.data;
    final pending = widget.state == DraftState.pending;
    final ro = readOnly || !(pending || widget.state == DraftState.error);
    void edited() {
      setState(() => d.edited = true);
      widget.onEdited?.call();
    }

    return CardShell(
      source: widget.source,
      status: pending
          ? CardChip(
              label: 'Needs your answer',
              fg: c.accent,
              bg: c.accentTint,
              icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Icon(d.channel.icon, size: 16, color: c.textBody),
              const SizedBox(width: 8),
              Text(
                '${d.channel.noun} draft',
                style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
              ),
              const Spacer(),
              if (d.language != null)
                Text(
                  d.language!,
                  style: IcType.text(12, weight: FontWeight.w600, color: c.textMuted),
                ),
            ],
          ),
          const SizedBox(height: 12),
          Container(
            decoration: BoxDecoration(
              border: Border.all(color: c.borderHairline),
              borderRadius: BorderRadius.circular(IcRadius.button),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _Line(
                  label: 'To',
                  value: d.to,
                  readOnly: ro,
                  onChanged: (v) {
                    d.to = v;
                    edited();
                  },
                ),
                if (d.cc != null)
                  _Line(
                    label: 'Cc',
                    value: d.cc!,
                    readOnly: ro,
                    onChanged: (v) {
                      d.cc = v;
                      edited();
                    },
                  ),
                if (d.subject != null)
                  _Line(
                    label: 'Subject',
                    value: d.subject!,
                    readOnly: ro,
                    strong: true,
                    onChanged: (v) {
                      d.subject = v;
                      edited();
                    },
                  ),
                _Line(
                  value: d.body,
                  readOnly: ro,
                  multiline: true,
                  last: true,
                  onChanged: (v) {
                    d.body = v;
                    edited();
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          Text(
            d.edited
                ? 'You changed the draft. Sends from ${d.from}.'
                : 'Select any part to change it. Sends from ${d.from}.',
            style: IcType.text(12, color: c.textMuted),
          ),
          const SizedBox(height: 14),
          if (widget.state == DraftState.sending)
            CardWorking(text: 'Sending to ${d.to}…')
          else if (pending || widget.state == DraftState.error) ...[
            if (widget.state == DraftState.error && d.errorText != null) ...[
              CardError(text: d.errorText!),
              const SizedBox(height: 10),
            ],
            PromptOptions(
              options: [
                if (widget.state == DraftState.error)
                  PromptOption('Try again', icon: LucideIcons.rotateCcw, onTap: widget.onRetry)
                else
                  PromptOption(
                    'Send',
                    icon: LucideIcons.send,
                    detail: 'Sends now from ${d.from}',
                    onTap: widget.onSend,
                  ),
                if (d.scheduleLabel != null && pending)
                  PromptOption('Send ${d.scheduleLabel}', icon: LucideIcons.clock, onTap: widget.onSchedule),
                PromptOption('Discard', detail: 'Nothing is sent', danger: true, onTap: widget.onDiscard),
              ],
            ),
          ],
        ],
      ),
    );
  }
}

/// One part of the draft: a label and a value you click to correct.
class _Line extends StatefulWidget {
  const _Line({
    required this.value,
    required this.readOnly,
    required this.onChanged,
    this.label,
    this.multiline = false,
    this.strong = false,
    this.last = false,
  });

  final String? label;
  final String value;
  final bool readOnly;
  final bool multiline;
  final bool strong;
  final bool last;
  final ValueChanged<String> onChanged;

  @override
  State<_Line> createState() => _LineState();
}

class _LineState extends State<_Line> {
  bool _editing = false;
  bool _hover = false;

  /// Started from the keyboard: Return (⌘↵ for the text) or Esc go back to the card's list.
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
    if (widget.readOnly) return;
    _byKey = byKey;
    _text.text = widget.value;
    setState(() => _editing = true);
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  void _commit({bool back = true}) {
    final v = _text.text.trimRight();
    if (v.trim().isNotEmpty && v != widget.value) widget.onChanged(v);
    setState(() => _editing = false);
    if (back) _back();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final style = IcType.text(
      widget.multiline ? 14 : 13.5,
      weight: widget.strong ? FontWeight.w600 : FontWeight.w400,
      color: c.textStrong,
      height: widget.multiline ? 1.55 : 1.35,
    );
    final Widget value = _editing
        ? Focus(
            onKeyEvent: (_, e) {
              if (e is! KeyDownEvent) return KeyEventResult.ignored;
              if (e.logicalKey == LogicalKeyboardKey.escape) {
                setState(() => _editing = false);
                _back();
                return KeyEventResult.handled;
              }
              final kb = HardwareKeyboard.instance;
              if (e.logicalKey == LogicalKeyboardKey.enter && (kb.isMetaPressed || kb.isControlPressed)) {
                _commit();
                return KeyEventResult.handled;
              }
              return KeyEventResult.ignored;
            },
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
              decoration: BoxDecoration(
                color: c.surfaceCard,
                borderRadius: BorderRadius.circular(IcRadius.xs),
                border: Border.all(color: c.accent),
                boxShadow: [BoxShadow(color: c.ring, spreadRadius: 2)],
              ),
              child: Field(
                controller: _text,
                focusNode: _focus,
                style: style,
                maxLines: widget.multiline ? null : 1,
                minLines: widget.multiline ? 4 : null,
                onSubmitted: widget.multiline ? null : (_) => _commit(),
              ),
            ),
          )
        : Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: Text(widget.value, style: style)),
              if (!widget.readOnly) ...[
                const SizedBox(width: 8),
                Padding(
                  padding: const EdgeInsets.only(top: 2),
                  child: Icon(LucideIcons.pencil, size: 13, color: _hover ? c.textBody : c.textSubtle),
                ),
              ],
            ],
          );
    return Stop(
      onActivate: widget.readOnly || _editing ? null : () => _start(byKey: true),
      builder: (context, v) => MouseRegion(
        cursor: widget.readOnly || _editing ? MouseCursor.defer : SystemMouseCursors.text,
        onEnter: (_) => setState(() => _hover = true),
        onExit: (_) => setState(() => _hover = false),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: _editing ? null : _start,
          child: AnimatedContainer(
            duration: IcMotion.fast,
            padding: EdgeInsets.symmetric(horizontal: 12, vertical: widget.multiline ? 12 : 9),
            decoration: BoxDecoration(
              color: v.selected
                  ? c.accentTint
                  : (_hover && !widget.readOnly && !_editing ? c.surfaceSunk : const Color(0x00000000)),
              border: widget.last ? null : Border(bottom: BorderSide(color: c.borderHairline)),
            ),
            child: widget.label == null
                ? value
                : Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(
                        width: 64,
                        child: Padding(
                          padding: EdgeInsets.only(top: _editing ? 7 : 1),
                          child: Text(
                            widget.label!,
                            style: IcType.text(12.5, weight: FontWeight.w600, color: c.textMuted),
                          ),
                        ),
                      ),
                      Expanded(child: value),
                    ],
                  ),
          ),
        ),
      ),
    );
  }
}
