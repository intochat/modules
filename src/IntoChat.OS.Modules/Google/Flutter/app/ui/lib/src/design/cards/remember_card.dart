import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum RememberState { pending, saved, skipped }

class RememberData {
  RememberData({required this.text, required this.kind});

  /// The memory, pre-filled by Ino; you correct it in place.
  String text;

  /// "About you", "Nordlys AS" or "Preferences".
  final String kind;
  bool edited = false;
}

/// "Remember this?": Ino asks before it saves something about you. The
/// memory is pre-filled; click it to correct it, then remember or skip.
class RememberCard extends StatefulWidget {
  const RememberCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onSave,
    this.onSkip,
  });

  final CardSource source;
  final RememberData data;
  final RememberState state;
  final VoidCallback? onSave;
  final VoidCallback? onSkip;

  @override
  State<RememberCard> createState() => _RememberCardState();
}

class _RememberCardState extends State<RememberCard> {
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
    _text.text = widget.data.text;
    _text.selection = TextSelection(baseOffset: 0, extentOffset: _text.text.length);
    setState(() => _editing = true);
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  void _commit({bool back = true}) {
    final v = _text.text.trim();
    if (v.isNotEmpty && v != widget.data.text) {
      widget.data
        ..text = v
        ..edited = true;
    }
    setState(() => _editing = false);
    if (back) _back();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    switch (widget.state) {
      case RememberState.saved:
        return CardResult(source: widget.source, tone: ResultTone.done, text: 'Ino will remember this', detail: d.text);
      case RememberState.skipped:
        return CardResult(source: widget.source, tone: ResultTone.neutral, text: 'Not remembered', detail: d.text);
      case RememberState.pending:
        final style = IcType.text(
          14.5,
          weight: FontWeight.w500,
          color: d.edited ? c.accent : c.textStrong,
          height: 1.4,
        );
        return CardShell(
          source: widget.source,
          status: CardChip(
            label: 'Needs your answer',
            fg: c.accent,
            bg: c.accentTint,
            icon: Icon(LucideIcons.hand, size: 13, color: c.accent),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Icon(LucideIcons.brain, size: 16, color: c.textBody),
                  const SizedBox(width: 8),
                  Text(
                    'Remember this?',
                    style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
                  ),
                  const Spacer(),
                  Text(
                    d.kind,
                    style: IcType.text(12, weight: FontWeight.w600, color: c.textMuted),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              if (_editing)
                Focus(
                  onKeyEvent: (_, e) {
                    if (e is KeyDownEvent && e.logicalKey == LogicalKeyboardKey.escape) {
                      setState(() => _editing = false);
                      _back();
                      return KeyEventResult.handled;
                    }
                    return KeyEventResult.ignored;
                  },
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                    decoration: BoxDecoration(
                      color: c.surfaceCard,
                      borderRadius: BorderRadius.circular(IcRadius.button),
                      border: Border.all(color: c.accent),
                      boxShadow: [BoxShadow(color: c.ring, spreadRadius: 2)],
                    ),
                    child: Field(controller: _text, focusNode: _focus, style: style, onSubmitted: (_) => _commit()),
                  ),
                )
              else
                Stop(
                  onActivate: () => _start(byKey: true),
                  builder: (context, v) => MouseRegion(
                    cursor: SystemMouseCursors.text,
                    onEnter: (_) => setState(() => _hover = true),
                    onExit: (_) => setState(() => _hover = false),
                    child: GestureDetector(
                      onTap: _start,
                      child: SelectionRing(
                        on: v.selected,
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                          decoration: BoxDecoration(
                            color: _hover || v.selected ? c.surfaceSunk : c.surfacePage,
                            borderRadius: BorderRadius.circular(IcRadius.button),
                            border: Border.all(color: c.borderHairline),
                          ),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Expanded(child: Text(d.text, style: style)),
                              const SizedBox(width: 8),
                              Icon(LucideIcons.pencil, size: 13, color: _hover ? c.textBody : c.textSubtle),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              const SizedBox(height: 10),
              Text(
                'Ino uses it in every session. Change or delete it any time in Settings › Apps › Ino › Memory.',
                style: IcType.text(12.5, color: c.textMuted, height: 1.4),
              ),
              const SizedBox(height: 14),
              PromptOptions(
                options: [
                  PromptOption('Remember', icon: LucideIcons.check, onTap: widget.onSave),
                  PromptOption('Not now', onTap: widget.onSkip),
                ],
              ),
            ],
          ),
        );
    }
  }
}
