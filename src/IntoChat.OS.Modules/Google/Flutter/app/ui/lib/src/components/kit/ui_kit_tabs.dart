import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';

/// Figma `.tabs`: a 4 px gap over a 1 px `hairline` bottom border. Each tab
/// has 9x10 padding, 12 px `muted` text and a 2 px transparent bottom
/// border; the active one is `strong`, 700, with an `accent` border. The row
/// scrolls horizontally when it overflows and keeps the active tab in view.
/// Indexes in [disabled] are dimmed and ignore taps.
final class UiKitTabs extends StatefulWidget {
  const UiKitTabs({
    super.key,
    required this.labels,
    required this.index,
    required this.onChanged,
    this.disabled = const {},
  });

  final List<String> labels;
  final int index;
  final ValueChanged<int> onChanged;
  final Set<int> disabled;

  @override
  State<UiKitTabs> createState() => _UiKitTabsState();
}

class _UiKitTabsState extends State<UiKitTabs> {
  List<GlobalKey> _keys = [];

  List<String> get labels => widget.labels;
  int get index => widget.index;
  ValueChanged<int> get onChanged => widget.onChanged;
  Set<int> get disabled => widget.disabled;

  @override
  void initState() {
    super.initState();
    _reveal(Duration.zero);
  }

  @override
  void didUpdateWidget(UiKitTabs oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.index != widget.index ||
        oldWidget.labels.length != widget.labels.length) {
      _reveal(const Duration(milliseconds: 200));
    }
  }

  // After layout, scroll the row just enough to show the active tab.
  void _reveal(Duration duration) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || index < 0 || index >= _keys.length) return;
      final tab = _keys[index].currentContext;
      if (tab == null) return;
      // Centred, clamped at the row's ends.
      Scrollable.ensureVisible(
        tab,
        alignment: 0.5,
        duration: duration,
        curve: UiTokens.of(context).ease,
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_keys.length != labels.length) {
      _keys = [for (var i = 0; i < labels.length; i++) GlobalKey()];
    }
    final c = UiColors.of(context);
    final type = UiType.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: c.hairline)),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        primary: false,
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++) ...[
              if (i > 0) const SizedBox(width: 4),
              _tab(c, type, i),
            ],
          ],
        ),
      ),
    );
  }

  Widget _tab(UiColors c, UiTypeSet type, int i) {
    final active = i == index;
    final off = disabled.contains(i);
    return Semantics(
      key: _keys[i],
      button: true,
      selected: active,
      enabled: !off,
      child: FocusableActionDetector(
        enabled: !off,
        mouseCursor: off ? SystemMouseCursors.basic : SystemMouseCursors.click,
        actions: {
          ActivateIntent: CallbackAction<ActivateIntent>(
            onInvoke: (_) => onChanged(i),
          ),
        },
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: off ? null : () => onChanged(i),
          child: Opacity(
            opacity: off ? 0.45 : 1,
            child: DecoratedBox(
              key: ValueKey('ui_kit_tab_$i'),
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(
                    width: 2,
                    color: active ? c.accent : Colors.transparent,
                  ),
                ),
              ),
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  vertical: 9,
                  horizontal: 10,
                ),
                child: Text(
                  labels[i],
                  style: type.body.copyWith(
                    fontSize: 12,
                    color: active ? c.strong : c.muted,
                    fontWeight: active ? FontWeight.w700 : FontWeight.w400,
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
