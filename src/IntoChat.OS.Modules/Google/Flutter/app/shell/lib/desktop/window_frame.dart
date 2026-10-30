import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

const double kDesktopTitleBarHeight = 38.0;

class DesktopWindowState extends ChangeNotifier {
  DesktopWindowState({
    required this.id,
    required this.appId,
    required this.title,
    required this.neuronUri,
    this.bounds = const Rect.fromLTWH(80, 48, 860, 560),
    this.zoomed = false,
    this.minimized = false,
  });

  final int id;
  final String appId;
  String title;
  final String neuronUri;
  Rect bounds;
  bool zoomed;
  bool minimized;

  void moveBy(Offset delta, Size desk) {
    if (zoomed) return;
    final nextLeft = (bounds.left + delta.dx).clamp(
      0.0,
      (desk.width - 160).clamp(0.0, double.infinity),
    );
    final nextTop = (bounds.top + delta.dy).clamp(
      0.0,
      (desk.height - 80).clamp(0.0, double.infinity),
    );
    bounds = Rect.fromLTWH(nextLeft, nextTop, bounds.width, bounds.height);
    notifyListeners();
  }

  void resizeBy({
    required Offset delta,
    required bool left,
    required bool top,
    required bool right,
    required bool bottom,
    Size minSize = const Size(360, 260),
  }) {
    if (zoomed) return;
    var l = bounds.left;
    var t = bounds.top;
    var r = bounds.right;
    var b = bounds.bottom;
    if (left) {
      l = (l + delta.dx).clamp(0.0, r - minSize.width);
    }
    if (right) {
      r = (r + delta.dx).clamp(l + minSize.width, 4096.0);
    }
    if (top) {
      t = (t + delta.dy).clamp(0.0, b - minSize.height);
    }
    if (bottom) {
      b = (b + delta.dy).clamp(t + minSize.height, 4096.0);
    }
    bounds = Rect.fromLTRB(l, t, r, b);
    notifyListeners();
  }

  void toggleZoom() {
    zoomed = !zoomed;
    notifyListeners();
  }

  void setMinimized(bool value) {
    minimized = value;
    notifyListeners();
  }
}

class DesktopTabSpec {
  const DesktopTabSpec({
    required this.key,
    required this.label,
    required this.selected,
    this.onTap,
  });

  final String key;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}

/// Free-floating, draggable, 8-way resizable Acrylic window frame (`38px` title
/// bar with Close/Minimise/Zoom traffic lights, double-click zoom, and focus
/// mode below `1024×640`).
class WindowFrame extends StatelessWidget {
  const WindowFrame({
    super.key,
    required this.window,
    required this.focused,
    required this.forceZoom,
    required this.desk,
    required this.child,
    this.tabs = const [],
    this.onFocus,
    this.onClose,
    this.onMinimize,
    this.onToggleZoom,
    this.onNewTab,
    this.onFavourite,
    this.isFavourite = false,
    this.onPin,
    this.isPinned = false,
  });

  final DesktopWindowState window;
  final bool focused;
  final bool forceZoom;
  final Size desk;
  final Widget child;
  final List<DesktopTabSpec> tabs;
  final VoidCallback? onFocus;
  final VoidCallback? onClose;
  final VoidCallback? onMinimize;
  final VoidCallback? onToggleZoom;
  final VoidCallback? onNewTab;
  final VoidCallback? onFavourite;
  final bool isFavourite;
  final VoidCallback? onPin;
  final bool isPinned;

  static final Map<int, bool> _lastZoom = {};

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final zoomed = window.zoomed || forceZoom;
    final radius = zoomed ? 0.0 : IcRadius.card;

    final innerContent = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _TitleBar(
          window: window,
          focused: focused,
          zoomed: zoomed,
          canMove: !forceZoom,
          desk: desk,
          onFocus: onFocus,
          onClose: onClose,
          onMinimize: onMinimize,
          onToggleZoom: onToggleZoom ?? window.toggleZoom,
          onNewTab: onNewTab,
          onFavourite: onFavourite,
          isFavourite: isFavourite,
          onPin: onPin,
          isPinned: isPinned,
        ),
        if (tabs.length > 1) _WindowTabStrip(tabs: tabs),
        Expanded(
          child: Listener(
            behavior: HitTestBehavior.translucent,
            onPointerDown: (_) {
              if (!focused) onFocus?.call();
            },
            child: RepaintBoundary(child: child),
          ),
        ),
      ],
    );

    final borderColor = focused
        ? (c.isGlass ? c.specularBorder : c.borderInput)
        : c.borderHairline;

    final frame = Container(
      decoration: BoxDecoration(
        color: c.isGlass
            ? c.surfaceCard.withValues(alpha: c.windowOpacity)
            : c.surfaceCard,
        borderRadius: BorderRadius.circular(radius),
        border: Border.all(color: borderColor, width: zoomed ? 0 : 1),
        boxShadow: zoomed ? null : (focused ? IcShadow.xl(c) : IcShadow.lg(c)),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(radius == 0 ? 0 : radius - 1),
        child: c.isGlass && c.blurSigma > 0
            ? BackdropFilter(
                filter: ImageFilter.blur(
                  sigmaX: c.blurSigma,
                  sigmaY: c.blurSigma,
                ),
                child: innerContent,
              )
            : innerContent,
      ),
    );

    final body = RepaintBoundary(
      child: zoomed
          ? frame
          : _ResizeHandles(window: window, onFocus: onFocus, child: frame),
    );

    return ListenableBuilder(
      listenable: window,
      child: body,
      builder: (context, builtChild) {
        final rect = zoomed ? Offset.zero & desk : window.bounds;
        return AnimatedPositioned(
          duration: zoomed != _lastZoom.putIfAbsent(window.id, () => zoomed)
              ? IcMotion.base
              : Duration.zero,
          curve: IcMotion.ease,
          left: rect.left,
          top: rect.top,
          width: rect.width,
          height: rect.height,
          onEnd: () => _lastZoom[window.id] = zoomed,
          child: builtChild!,
        );
      },
    );
  }
}

class _TitleBar extends StatefulWidget {
  const _TitleBar({
    required this.window,
    required this.focused,
    required this.zoomed,
    required this.canMove,
    required this.desk,
    required this.onToggleZoom,
    this.onFocus,
    this.onClose,
    this.onMinimize,
    this.onNewTab,
    this.onFavourite,
    this.isFavourite = false,
    this.onPin,
    this.isPinned = false,
  });

  final DesktopWindowState window;
  final bool focused;
  final bool zoomed;
  final bool canMove;
  final Size desk;
  final VoidCallback? onFocus;
  final VoidCallback? onClose;
  final VoidCallback? onMinimize;
  final VoidCallback onToggleZoom;
  final VoidCallback? onNewTab;
  final VoidCallback? onFavourite;
  final bool isFavourite;
  final VoidCallback? onPin;
  final bool isPinned;

  @override
  State<_TitleBar> createState() => _TitleBarState();
}

class _TitleBarState extends State<_TitleBar> {
  bool _hoverControls = false;
  DateTime _lastDown = DateTime(0);
  Offset _lastPos = Offset.zero;

  void _onDown(PointerDownEvent e) {
    final now = DateTime.now();
    final quick = now.difference(_lastDown) < const Duration(milliseconds: 350);
    final near = (e.position - _lastPos).distance < 6;
    _lastDown = now;
    _lastPos = e.position;
    if (quick && near && !_hoverControls) {
      _lastDown = DateTime(0);
      widget.onToggleZoom();
    } else {
      widget.onFocus?.call();
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final w = widget.window;
    return Listener(
      onPointerDown: _onDown,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onPanStart: widget.canMove ? (_) => widget.onFocus?.call() : null,
        onPanUpdate: widget.canMove
            ? (d) => w.moveBy(d.delta, widget.desk)
            : null,
        child: Container(
          height: kDesktopTitleBarHeight,
          decoration: BoxDecoration(
            color: widget.focused ? c.titleBarFocused : c.titleBarBlurred,
            border: Border(bottom: BorderSide(color: c.borderHairline)),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 14),
          child: Stack(
            alignment: Alignment.center,
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: MouseRegion(
                  onEnter: (_) => setState(() => _hoverControls = true),
                  onExit: (_) => setState(() => _hoverControls = false),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _Light(
                        color: c.error,
                        active: widget.focused || _hoverControls,
                        glyph: LucideIcons.x,
                        showGlyph: _hoverControls,
                        label: 'Close',
                        onTap: widget.onClose ?? () {},
                      ),
                      const SizedBox(width: 8),
                      _Light(
                        color: c.working,
                        active: widget.focused || _hoverControls,
                        glyph: LucideIcons.minus,
                        showGlyph: _hoverControls,
                        label: 'Minimise',
                        onTap:
                            widget.onMinimize ??
                            () => w.setMinimized(!w.minimized),
                      ),
                      const SizedBox(width: 8),
                      _Light(
                        color: c.done,
                        active: widget.focused || _hoverControls,
                        glyph: widget.zoomed
                            ? LucideIcons.minimize2
                            : LucideIcons.maximize2,
                        showGlyph: _hoverControls,
                        label: 'Zoom',
                        onTap: widget.onToggleZoom,
                      ),
                    ],
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 80),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Flexible(
                      child: Text(
                        w.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: IcType.text(
                          13,
                          weight: FontWeight.w600,
                          color: widget.focused ? c.textStrong : c.textMuted,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    TagPill(label: w.neuronUri, tone: TagTone.neutral),
                  ],
                ),
              ),
              if (widget.onNewTab != null ||
                  widget.onFavourite != null ||
                  widget.onPin != null)
                Align(
                  alignment: Alignment.centerRight,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (widget.onFavourite != null)
                        _TitleActionButton(
                          icon: LucideIcons.star,
                          tooltip: widget.isFavourite
                              ? 'Remove from favourites'
                              : 'Add to favourites',
                          active: widget.isFavourite,
                          onTap: widget.onFavourite!,
                        ),
                      if (widget.onPin != null)
                        _TitleActionButton(
                          icon: widget.isPinned
                              ? LucideIcons.pinOff
                              : LucideIcons.pin,
                          tooltip: widget.isPinned
                              ? 'Unpin from Dock'
                              : 'Pin to Dock',
                          active: widget.isPinned,
                          onTap: widget.onPin!,
                        ),
                      if (widget.onNewTab != null)
                        _TitleActionButton(
                          icon: LucideIcons.plus,
                          tooltip: 'New tab',
                          onTap: widget.onNewTab!,
                        ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TitleActionButton extends StatefulWidget {
  const _TitleActionButton({
    required this.icon,
    required this.tooltip,
    required this.onTap,
    this.active = false,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;
  final bool active;

  @override
  State<_TitleActionButton> createState() => _TitleActionButtonState();
}

class _TitleActionButtonState extends State<_TitleActionButton> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Tooltip(
      message: widget.tooltip,
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hover = true),
        onExit: (_) => setState(() => _hover = false),
        child: GestureDetector(
          onTap: widget.onTap,
          child: Container(
            width: 26,
            height: 26,
            margin: const EdgeInsets.only(left: 4),
            decoration: BoxDecoration(
              color: widget.active
                  ? c.accentTint
                  : (_hover ? c.surfaceSunk : Colors.transparent),
              borderRadius: BorderRadius.circular(IcRadius.xs),
            ),
            child: Icon(
              widget.icon,
              size: 13.5,
              color: widget.active
                  ? c.accent
                  : (_hover ? c.textStrong : c.textMuted),
            ),
          ),
        ),
      ),
    );
  }
}

class _WindowTabStrip extends StatelessWidget {
  const _WindowTabStrip({required this.tabs});

  final List<DesktopTabSpec> tabs;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Container(
      height: 32,
      decoration: BoxDecoration(
        color: c.surfaceSunk.withValues(alpha: 0.72),
        border: Border(bottom: BorderSide(color: c.borderHairline)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: tabs.length,
        separatorBuilder: (_, _) => const SizedBox(width: 4),
        itemBuilder: (context, index) {
          final tab = tabs[index];
          return GestureDetector(
            onTap: tab.onTap,
            child: MouseRegion(
              cursor: SystemMouseCursors.click,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10),
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: tab.selected ? c.surfaceCard : Colors.transparent,
                  borderRadius: BorderRadius.circular(IcRadius.xs),
                  border: tab.selected
                      ? Border.all(color: c.borderHairline)
                      : null,
                ),
                child: Text(
                  tab.label,
                  style: IcType.text(
                    12,
                    weight: tab.selected ? FontWeight.w600 : FontWeight.w500,
                    color: tab.selected ? c.textStrong : c.textMuted,
                  ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _Light extends StatelessWidget {
  const _Light({
    required this.color,
    required this.active,
    required this.glyph,
    required this.showGlyph,
    required this.label,
    required this.onTap,
  });

  final Color color;
  final bool active;
  final IconData glyph;
  final bool showGlyph;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Semantics(
      button: true,
      label: label,
      child: GestureDetector(
        onTap: onTap,
        onDoubleTap: () {},
        child: MouseRegion(
          cursor: SystemMouseCursors.click,
          child: Container(
            width: 12,
            height: 12,
            decoration: BoxDecoration(
              color: active ? color : c.borderInput,
              shape: BoxShape.circle,
            ),
            child: showGlyph
                ? Icon(glyph, size: 8, color: const Color(0xB317140F))
                : null,
          ),
        ),
      ),
    );
  }
}

class _ResizeHandles extends StatelessWidget {
  const _ResizeHandles({
    required this.window,
    required this.child,
    this.onFocus,
  });

  final DesktopWindowState window;
  final Widget child;
  final VoidCallback? onFocus;

  static const _edge = 6.0;
  static const _corner = 14.0;

  @override
  Widget build(BuildContext context) {
    Widget handle({
      required MouseCursor cursor,
      double? left,
      double? top,
      double? right,
      double? bottom,
      double? width,
      double? height,
      required bool l,
      required bool t,
      required bool r,
      required bool b,
    }) {
      return Positioned(
        left: left,
        top: top,
        right: right,
        bottom: bottom,
        width: width,
        height: height,
        child: MouseRegion(
          cursor: cursor,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onPanStart: (_) => onFocus?.call(),
            onPanUpdate: (d) => window.resizeBy(
              delta: d.delta,
              left: l,
              top: t,
              right: r,
              bottom: b,
            ),
          ),
        ),
      );
    }

    return Stack(
      children: [
        Positioned.fill(child: child),
        handle(
          cursor: SystemMouseCursors.resizeLeftRight,
          left: 0,
          top: _corner,
          bottom: _corner,
          width: _edge,
          l: true,
          t: false,
          r: false,
          b: false,
        ),
        handle(
          cursor: SystemMouseCursors.resizeLeftRight,
          right: 0,
          top: _corner,
          bottom: _corner,
          width: _edge,
          l: false,
          t: false,
          r: true,
          b: false,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpDown,
          top: 0,
          left: _corner,
          right: _corner,
          height: _edge,
          l: false,
          t: true,
          r: false,
          b: false,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpDown,
          bottom: 0,
          left: _corner,
          right: _corner,
          height: _edge,
          l: false,
          t: false,
          r: false,
          b: true,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpLeftDownRight,
          left: 0,
          top: 0,
          width: _corner,
          height: _corner,
          l: true,
          t: true,
          r: false,
          b: false,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpRightDownLeft,
          right: 0,
          top: 0,
          width: _corner,
          height: _corner,
          l: false,
          t: true,
          r: true,
          b: false,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpRightDownLeft,
          left: 0,
          bottom: 0,
          width: _corner,
          height: _corner,
          l: true,
          t: false,
          r: false,
          b: true,
        ),
        handle(
          cursor: SystemMouseCursors.resizeUpLeftDownRight,
          right: 0,
          bottom: 0,
          width: _corner,
          height: _corner,
          l: false,
          t: false,
          r: true,
          b: true,
        ),
      ],
    );
  }
}
