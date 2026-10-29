import 'dart:ui';

import 'package:flutter/material.dart';

import '../../theme/ui_colors.dart';
import '../../theme/ui_tokens.dart';

// A mounted snapshot is already current; only a subsequent identity change moves.
class UiMotion extends StatefulWidget {
  const UiMotion({
    super.key,
    required this.contentKey,
    required this.child,
    this.animate = true,
  });
  final Object contentKey;
  final Widget child;
  final bool animate;
  @override
  State<UiMotion> createState() => _UiMotionState();
}

class _UiMotionState extends State<UiMotion>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    value: 1,
  );
  late final Animation<double> _opacity = _controller.drive(
    CurveTween(curve: Curves.easeOutCubic),
  );
  bool get _enabled =>
      widget.animate && !MediaQuery.disableAnimationsOf(context);
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _controller.duration = UiTokens.of(context).standard;
    if (!_enabled) _controller.value = 1;
  }

  @override
  void didUpdateWidget(UiMotion oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!_enabled) {
      _controller.value = 1;
    } else if (widget.contentKey != oldWidget.contentKey) {
      _controller.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => FadeTransition(
    opacity: _opacity,
    child: KeyedSubtree(key: ValueKey(widget.contentKey), child: widget.child),
  );
}

class UiSurface extends StatelessWidget {
  const UiSurface({
    super.key,
    required this.child,
    this.padding,
    this.emphasized = false,
  });
  final Widget child;
  final EdgeInsetsGeometry? padding;
  final bool emphasized;
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tokens = UiTokens.of(context);
    final c = UiColors.of(context);
    final isDark = theme.brightness == Brightness.dark;
    final fill = emphasized
        ? theme.colorScheme.primaryContainer.withValues(
            alpha: isDark ? 0.88 : 0.96,
          )
        : c.surface.withValues(alpha: isDark ? 0.86 : 0.96);
    final borderColor = emphasized
        ? c.accent.withValues(alpha: 0.35)
        : (isDark ? Colors.white.withValues(alpha: 0.12) : c.hairline);
    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(tokens.surfaceRadius),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: isDark ? 0.38 : 0.08),
            blurRadius: 24,
            offset: const Offset(0, 10),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(tokens.surfaceRadius),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
          child: Material(
            color: fill,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(tokens.surfaceRadius),
              side: BorderSide(color: borderColor),
            ),
            clipBehavior: Clip.antiAlias,
            child: Padding(
              padding: padding ?? EdgeInsets.all(tokens.spaceLarge),
              child: child,
            ),
          ),
        ),
      ),
    );
  }
}

@immutable
class UiBreadcrumbEntry {
  const UiBreadcrumbEntry({required this.id, required this.label});
  final String id;
  final String label;
}

class UiBreadcrumbs extends StatelessWidget {
  const UiBreadcrumbs({
    super.key,
    required this.entries,
    this.onSelected,
    this.maxVisible = 4,
  }) : assert(maxVisible >= 2);
  final List<UiBreadcrumbEntry> entries;
  final ValueChanged<String>? onSelected;
  final int maxVisible;
  @override
  Widget build(BuildContext context) {
    if (entries.isEmpty) return const SizedBox.shrink();
    final theme = Theme.of(context);
    final collapsed = entries.length > maxVisible;
    final hiddenEnd = collapsed
        ? (entries.length - maxVisible + 2).clamp(1, entries.length - 1)
        : 1;
    final children = <Widget>[];
    void add(Widget child) {
      if (children.isNotEmpty) {
        children.add(
          Icon(
            Icons.chevron_right,
            size: 16,
            color: theme.colorScheme.onSurfaceVariant,
          ),
        );
      }
      children.add(child);
    }

    for (var i = 0; i < entries.length; i++) {
      if (collapsed && i == 1) {
        add(
          PopupMenuButton<String>(
            tooltip: 'Show parent locations',
            enabled: onSelected != null,
            onSelected: onSelected,
            icon: const Icon(Icons.more_horiz),
            itemBuilder: (_) => [
              for (final entry in entries.sublist(1, hiddenEnd))
                PopupMenuItem(value: entry.id, child: Text(entry.label)),
            ],
          ),
        );
        i = hiddenEnd - 1;
        continue;
      }
      final entry = entries[i];
      final current = i == entries.length - 1;
      add(
        Semantics(
          selected: current,
          child: TextButton(
            key: ValueKey(entry.id),
            onPressed: current || onSelected == null
                ? null
                : () => onSelected!(entry.id),
            style: TextButton.styleFrom(
              disabledForegroundColor: theme.colorScheme.onSurface,
              textStyle: theme.textTheme.labelLarge?.copyWith(
                fontWeight: current ? FontWeight.w600 : FontWeight.w400,
              ),
            ),
            child: Text(entry.label),
          ),
        ),
      );
    }
    return Semantics(
      label: 'Location',
      container: true,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(mainAxisSize: MainAxisSize.min, children: children),
      ),
    );
  }
}
