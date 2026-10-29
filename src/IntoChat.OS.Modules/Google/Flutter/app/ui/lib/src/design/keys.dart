import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'tokens.dart';

/// Alias for [PromptKeys] keyboard navigation group.
typedef KeyGroup = PromptKeys;

/// Where the selection lands when a group gets focus.
enum PromptEntry { initial, first, last }

class PromptKeys extends StatefulWidget {
  const PromptKeys({
    super.key,
    required this.child,
    this.focusNode,
    this.autofocus = false,
  });

  final Widget child;

  /// The host's node for this group, so it can move focus between groups.
  final FocusNode? focusNode;
  final bool autofocus;

  static PromptKeysState? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<_GroupScope>()?.state;

  static final _entry = Expando<PromptEntry>();

  /// Focuses the group that owns [node], with the selection at [at].
  static void focus(FocusNode node, {PromptEntry at = PromptEntry.initial}) {
    _entry[node] = at;
    if (node.hasPrimaryFocus) {
      final g = node.context?.findAncestorStateOfType<PromptKeysState>();
      g?._enter(at);
    }
    node.requestFocus();
  }

  @override
  State<PromptKeys> createState() => PromptKeysState();
}

class PromptKeysState extends State<PromptKeys> {
  late final FocusNode _own = FocusNode(debugLabel: 'PromptKeys');
  FocusNode get node => widget.focusNode ?? _own;

  final _stops = <_StopState>{};
  final _notifier = _Notifier();
  _StopState? _selected;

  /// The group or something in it (a value being corrected) has focus.
  bool get focused => node.hasFocus;

  @override
  void dispose() {
    _own.dispose();
    _notifier.dispose();
    super.dispose();
  }

  /// Back to the group after correcting a value with the keyboard.
  void refocus() => node.requestFocus();

  void _register(_StopState s) => _stops.add(s);

  void _unregister(_StopState s) {
    _stops.remove(s);
    if (_selected == s) _selected = null;
  }

  /// Stops in reading order: top to bottom, then left to right.
  List<_StopState> get _ordered {
    final live = [
      for (final s in _stops)
        if (s.mounted && s.enabled && s.context.findRenderObject() is RenderBox)
          s,
    ];
    Offset at(_StopState s) {
      final box = s.context.findRenderObject()! as RenderBox;
      return box.attached ? box.localToGlobal(Offset.zero) : Offset.zero;
    }

    live.sort((a, b) {
      final pa = at(a), pb = at(b);
      final dy = (pa.dy - pb.dy);
      if (dy.abs() > 4) return dy.sign.toInt();
      return (pa.dx - pb.dx).sign.toInt();
    });
    return live;
  }

  _StopState? get _default {
    final all = _ordered;
    for (final s in all) {
      if (s.widget.isDefault) return s;
    }
    for (final s in all) {
      if (s.widget.number != null) return s;
    }
    return all.isEmpty ? null : all.first;
  }

  void _select(_StopState? s, {bool reveal = true}) {
    _selected = s;
    _notifier.ping();
    if (s != null && reveal) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!s.mounted) return;
        Scrollable.ensureVisible(
          s.context,
          duration: IcMotion.fast,
          alignmentPolicy: ScrollPositionAlignmentPolicy.keepVisibleAtEnd,
        );
        Scrollable.ensureVisible(
          s.context,
          duration: IcMotion.fast,
          alignmentPolicy: ScrollPositionAlignmentPolicy.keepVisibleAtStart,
        );
      });
    }
  }

  void _enter(PromptEntry at) {
    final all = _ordered;
    _select(switch (at) {
      PromptEntry.first => all.isEmpty ? null : all.first,
      PromptEntry.last => all.isEmpty ? null : all.last,
      PromptEntry.initial =>
        _selected != null && _selected!.mounted ? _selected : _default,
    });
  }

  void _onFocus(bool has) {
    if (has && node.hasPrimaryFocus) {
      final at = PromptKeys._entry[node] ?? PromptEntry.initial;
      PromptKeys._entry[node] = null;
      if (_stops.isEmpty) {
        WidgetsBinding.instance.addPostFrameCallback(
          (_) => mounted ? _enter(at) : null,
        );
      } else {
        _enter(at);
      }
    }
    _notifier.ping();
  }

  static const _digits = [
    PhysicalKeyboardKey.digit1,
    PhysicalKeyboardKey.digit2,
    PhysicalKeyboardKey.digit3,
    PhysicalKeyboardKey.digit4,
    PhysicalKeyboardKey.digit5,
    PhysicalKeyboardKey.digit6,
    PhysicalKeyboardKey.digit7,
    PhysicalKeyboardKey.digit8,
    PhysicalKeyboardKey.digit9,
  ];

  KeyEventResult _move(int by) {
    final all = _ordered;
    if (all.isEmpty) return KeyEventResult.ignored;
    final i = _selected == null ? -1 : all.indexOf(_selected!);
    final next = i < 0 ? (by > 0 ? 0 : all.length - 1) : i + by;
    if (next < 0 || next >= all.length) return KeyEventResult.ignored;
    _select(all[next]);
    return KeyEventResult.handled;
  }

  KeyEventResult _onKey(FocusNode n, KeyEvent e) {
    if (!n.hasPrimaryFocus) return KeyEventResult.ignored;
    if (e is! KeyDownEvent && e is! KeyRepeatEvent) {
      return KeyEventResult.ignored;
    }
    final kb = HardwareKeyboard.instance;
    final k = e.logicalKey;
    final enter =
        k == LogicalKeyboardKey.enter || k == LogicalKeyboardKey.numpadEnter;
    if (kb.isMetaPressed || kb.isControlPressed) {
      if (enter) {
        _default?.run();
        return KeyEventResult.handled;
      }
      return KeyEventResult.ignored;
    }
    if (kb.isAltPressed) return KeyEventResult.ignored;
    final sel = _selected?.mounted == true ? _selected : null;
    final horizontal =
        k == LogicalKeyboardKey.arrowLeft || k == LogicalKeyboardKey.arrowRight;
    if (horizontal && sel?.widget.onArrow != null) {
      sel!.widget.onArrow!(k == LogicalKeyboardKey.arrowRight ? 1 : -1);
      return KeyEventResult.handled;
    }
    if (k == LogicalKeyboardKey.arrowDown ||
        k == LogicalKeyboardKey.arrowRight) {
      return _move(1);
    }
    if (k == LogicalKeyboardKey.arrowUp || k == LogicalKeyboardKey.arrowLeft) {
      return _move(-1);
    }
    if (k == LogicalKeyboardKey.tab) return _move(kb.isShiftPressed ? -1 : 1);
    if (e is KeyRepeatEvent) return KeyEventResult.ignored;
    if (enter || k == LogicalKeyboardKey.space) {
      if (sel == null) {
        _enter(PromptEntry.initial);
      } else {
        sel.run();
      }
      return KeyEventResult.handled;
    }
    if (k == LogicalKeyboardKey.escape) {
      for (final s in _stops) {
        if (s.mounted && s.widget.chat && s.enabled) {
          s.run();
          return KeyEventResult.handled;
        }
      }
      return KeyEventResult.ignored;
    }
    final d = _digits.indexOf(e.physicalKey);
    if (d >= 0 && !kb.isShiftPressed) {
      for (final s in _stops) {
        if (s.mounted && s.enabled && s.widget.number == d + 1) {
          _select(s, reveal: false);
          s.run();
          return KeyEventResult.handled;
        }
      }
    }
    return KeyEventResult.ignored;
  }

  @override
  Widget build(BuildContext context) => Focus(
    focusNode: node,
    autofocus: widget.autofocus,
    onFocusChange: _onFocus,
    onKeyEvent: _onKey,
    child: Listener(
      onPointerDown: (_) {
        if (!node.hasFocus) node.requestFocus();
      },
      child: _GroupScope(state: this, notifier: _notifier, child: widget.child),
    ),
  );
}

class _Notifier extends ChangeNotifier {
  void ping() => notifyListeners();
}

class _GroupScope extends InheritedNotifier<_Notifier> {
  const _GroupScope({
    required this.state,
    required super.notifier,
    required super.child,
  });

  final PromptKeysState state;
}

/// What a [Stop] draws with.
class StopView {
  const StopView({this.selected = false, this.groupFocused = false});

  final bool selected;
  final bool groupFocused;
}

/// One thing the keyboard can reach inside a [PromptKeys] group.
class Stop extends StatefulWidget {
  const Stop({
    super.key,
    required this.builder,
    this.onActivate,
    this.number,
    this.isDefault = false,
    this.chat = false,
    this.onArrow,
  });

  final Widget Function(BuildContext context, StopView view) builder;
  final VoidCallback? onActivate;
  final int? number;
  final bool isDefault;
  final bool chat;
  final ValueChanged<int>? onArrow;

  @override
  State<Stop> createState() => _StopState();
}

class _StopState extends State<Stop> {
  PromptKeysState? _group;

  bool get enabled => widget.onActivate != null || widget.onArrow != null;

  void run() => widget.onActivate?.call();

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final g = PromptKeys.maybeOf(context);
    if (g != _group) {
      _group?._unregister(this);
      _group = g;
      g?._register(this);
    }
  }

  @override
  void dispose() {
    _group?._unregister(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final g = PromptKeys.maybeOf(context);
    final focused = g != null && g.focused;
    final selected = focused && g._selected == this;
    return Semantics(
      button: widget.onActivate != null,
      selected: selected,
      focused: selected,
      onTap: widget.onActivate,
      child: widget.builder(
        context,
        StopView(selected: selected, groupFocused: focused),
      ),
    );
  }
}

/// The keyboard selection on a button, value or toggle: a 1.5px accent edge
/// and the focus ring around it.
class SelectionRing extends StatelessWidget {
  const SelectionRing({
    super.key,
    required this.on,
    required this.child,
    this.radius = IcRadius.button,
  });

  final bool on;
  final Widget child;
  final double radius;

  @override
  Widget build(BuildContext context) {
    if (!on) return child;
    final c = IcTheme.of(context);
    return CustomPaint(
      foregroundPainter: _RingPainter(
        accent: c.accent,
        ring: c.ring,
        radius: radius,
      ),
      child: child,
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter({
    required this.accent,
    required this.ring,
    required this.radius,
  });

  final Color accent;
  final Color ring;
  final double radius;

  @override
  void paint(Canvas canvas, Size size) {
    final r = RRect.fromRectAndRadius(
      Offset.zero & size,
      Radius.circular(radius),
    );
    canvas.drawRRect(
      r.inflate(2.25),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3
        ..color = ring,
    );
    canvas.drawRRect(
      r.deflate(0.75),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5
        ..color = accent,
    );
  }

  @override
  bool shouldRepaint(_RingPainter old) =>
      old.accent != accent || old.ring != ring || old.radius != radius;
}

class PromptScope extends InheritedWidget {
  const PromptScope({
    super.key,
    required super.child,
    this.onChat,
    this.docked = false,
  });

  final VoidCallback? onChat;
  final bool docked;

  static PromptScope? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<PromptScope>();

  @override
  bool updateShouldNotify(PromptScope old) =>
      old.onChat != onChat || old.docked != docked;
}

class PromptOption {
  const PromptOption(
    this.label, {
    this.onTap,
    this.detail,
    this.icon,
    this.danger = false,
  });

  final String label;
  final String? detail;
  final IconData? icon;
  final VoidCallback? onTap;
  final bool danger;
}

class PromptOptions extends StatelessWidget {
  const PromptOptions({super.key, required this.options, this.note});

  final List<PromptOption> options;
  final String? note;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final scope = PromptScope.maybeOf(context);
    final group = PromptKeys.maybeOf(context);
    final firstLive = options.indexWhere((o) => o.onTap != null);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final (i, o) in options.indexed) ...[
          if (i > 0) const SizedBox(height: 6),
          _OptionRow(option: o, number: i + 1, isDefault: i == firstLive),
        ],
        const SizedBox(height: 6),
        _OptionRow(
          option: PromptOption(
            'Chat about this',
            icon: LucideIcons.messageSquare,
            detail: 'Ask or tell Ino something first',
            onTap: scope?.onChat ?? () {},
          ),
          number: options.length + 1,
          chat: true,
        ),
        if (note != null || (group?.focused ?? false)) ...[
          const SizedBox(height: 10),
          Row(
            children: [
              if (note != null)
                Expanded(
                  child: Text(
                    note!,
                    style: IcType.text(12, color: c.textMuted),
                  ),
                )
              else
                const Spacer(),
              if (group?.focused ?? false)
                Text(
                  '↑↓ to move  ·  ↵ to choose  ·  Esc to chat',
                  style: IcType.text(12, color: c.textSubtle),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

class _OptionRow extends StatefulWidget {
  const _OptionRow({
    required this.option,
    required this.number,
    this.isDefault = false,
    this.chat = false,
  });

  final PromptOption option;
  final int number;
  final bool isDefault;
  final bool chat;

  @override
  State<_OptionRow> createState() => _OptionRowState();
}

class _OptionRowState extends State<_OptionRow> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final o = widget.option;
    final disabled = o.onTap == null;
    return Stop(
      onActivate: o.onTap,
      number: disabled ? null : widget.number,
      isDefault: widget.isDefault,
      chat: widget.chat,
      builder: (context, v) {
        final on = v.selected;
        final labelColor = o.danger
            ? c.error
            : (widget.chat ? c.textBody : c.textStrong);
        return Opacity(
          opacity: disabled ? 0.45 : 1,
          child: MouseRegion(
            cursor: disabled ? MouseCursor.defer : SystemMouseCursors.click,
            onEnter: (_) => setState(() => _hover = true),
            onExit: (_) => setState(() => _hover = false),
            child: GestureDetector(
              onTap: o.onTap,
              child: AnimatedContainer(
                duration: IcMotion.fast,
                constraints: const BoxConstraints(minHeight: 42),
                padding: EdgeInsets.symmetric(
                  horizontal: on ? 11.5 : 12,
                  vertical: on ? 7.5 : 8,
                ),
                decoration: BoxDecoration(
                  color: on
                      ? c.accentTint
                      : (_hover && !disabled ? c.surfaceSunk : c.surfaceCard),
                  borderRadius: BorderRadius.circular(IcRadius.button),
                  border: Border.all(
                    color: on ? c.accent : c.borderInput,
                    width: on ? 1.5 : 1,
                  ),
                ),
                child: Row(
                  children: [
                    Container(
                      width: 22,
                      height: 22,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: on ? c.accent : c.surfaceSunk,
                        borderRadius: BorderRadius.circular(IcRadius.xs),
                      ),
                      child: Text(
                        '${widget.number}',
                        style: IcType.text(
                          12,
                          weight: FontWeight.w700,
                          color: on ? c.onAccent : c.textBody,
                          tnum: true,
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    if (o.icon != null) ...[
                      Icon(
                        o.icon,
                        size: 15,
                        color: widget.chat ? c.textMuted : labelColor,
                      ),
                      const SizedBox(width: 8),
                    ],
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            o.label,
                            style: IcType.text(
                              14,
                              weight: widget.chat
                                  ? FontWeight.w500
                                  : FontWeight.w600,
                              color: labelColor,
                            ),
                          ),
                          if (o.detail != null)
                            Padding(
                              padding: const EdgeInsets.only(top: 1),
                              child: Text(
                                o.detail!,
                                style: IcType.text(12, color: c.textMuted),
                              ),
                            ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    if (on)
                      Text(
                        '↵',
                        style: IcType.text(
                          13,
                          weight: FontWeight.w600,
                          color: c.accent,
                        ),
                      )
                    else
                      Icon(
                        LucideIcons.chevronRight,
                        size: 15,
                        color: c.textSubtle,
                      ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}
