import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'smart_desktop.dart';

const double kOsBarHeight = 36.0;

/// Adaptive `36px` OS bar supporting `Floating Glass Bar`, `Classic Bottom Bar`,
/// `Split Top + Dock`, and `Compact Pill` with live app indicators, working
/// pulse badges, compute token counter (`tnum`), connection status,
/// notifications, and clock.
class OsBar extends StatelessWidget {
  const OsBar({
    super.key,
    this.barLayout = BarLayout.floatingGlass,
    this.apps = defaultSmartAppSpecs,
    this.openAppIds = const {'ino'},
    this.workingAppIds = const {},
    this.focusedAppId = 'ino',
    this.paletteOpen = false,
    this.notifications = 2,
    this.tokensToday = 14800,
    this.connected = true,
    this.onTogglePalette,
    this.onLaunchApp,
    this.onOpenSettings,
    this.onOpenNotifications,
    this.onOpenCompute,
  });

  final BarLayout barLayout;
  final List<SmartAppTileSpec> apps;
  final Set<String> openAppIds;
  final Set<String> workingAppIds;
  final String? focusedAppId;
  final bool paletteOpen;
  final int notifications;
  final int tokensToday;
  final bool connected;
  final VoidCallback? onTogglePalette;
  final ValueChanged<String>? onLaunchApp;
  final VoidCallback? onOpenSettings;
  final VoidCallback? onOpenNotifications;
  final VoidCallback? onOpenCompute;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final isPill =
        barLayout == BarLayout.floatingGlass ||
        barLayout == BarLayout.compactPill;

    final innerRow = SizedBox(
      height: kOsBarHeight,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: _BarButton(
              tooltip: 'Search or ask  ⌘K',
              selected: paletteOpen,
              onTap: onTogglePalette ?? () {},
              child: Icon(LucideIcons.command, size: 16, color: c.textStrong),
            ),
          ),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final spec in apps)
                _DockAppItem(
                  spec: spec,
                  isOpen: openAppIds.contains(spec.id),
                  isWorking: workingAppIds.contains(spec.id),
                  isFocused: focusedAppId == spec.id,
                  onTap: () => onLaunchApp?.call(spec.id),
                ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: _StatusTray(
              notifications: notifications,
              tokensToday: tokensToday,
              connected: connected,
              onOpenNotifications: onOpenNotifications,
              onOpenCompute: onOpenCompute,
              onOpenSettings: onOpenSettings,
            ),
          ),
        ],
      ),
    );

    if (isPill) {
      return RepaintBoundary(
        child: Padding(
          padding: EdgeInsets.symmetric(
            horizontal: barLayout == BarLayout.compactPill ? 64 : 16,
            vertical: 2,
          ),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(IcRadius.pill),
            child: BackdropFilter(
              filter: ImageFilter.blur(
                sigmaX: c.blurSigma > 0 ? c.blurSigma : 18,
                sigmaY: c.blurSigma > 0 ? c.blurSigma : 18,
              ),
              child: Container(
                decoration: BoxDecoration(
                  color: c.chrome,
                  borderRadius: BorderRadius.circular(IcRadius.pill),
                  border: Border.all(
                    color: c.isGlass ? c.specularBorder : c.chromeBorder,
                  ),
                  boxShadow: IcShadow.md(c),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 10),
                child: innerRow,
              ),
            ),
          ),
        ),
      );
    }

    return RepaintBoundary(
      child: ClipRect(
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 16, sigmaY: 16),
          child: Container(
            decoration: BoxDecoration(
              color: c.chrome,
              border: Border(top: BorderSide(color: c.chromeBorder)),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 10),
            child: innerRow,
          ),
        ),
      ),
    );
  }
}

class _DockAppItem extends StatefulWidget {
  const _DockAppItem({
    required this.spec,
    required this.isOpen,
    required this.isWorking,
    required this.isFocused,
    required this.onTap,
  });

  final SmartAppTileSpec spec;
  final bool isOpen;
  final bool isWorking;
  final bool isFocused;
  final VoidCallback onTap;

  @override
  State<_DockAppItem> createState() => _DockAppItemState();
}

class _DockAppItemState extends State<_DockAppItem> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 2),
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hover = true),
        onExit: (_) => setState(() => _hover = false),
        child: Tooltip(
          message: '${widget.spec.name} (${widget.spec.neuronUri})',
          child: GestureDetector(
            onTap: widget.onTap,
            child: AnimatedContainer(
              duration: IcMotion.fast,
              width: 28,
              height: 28,
              decoration: BoxDecoration(
                color: widget.isFocused
                    ? c.accentTint
                    : (_hover
                          ? (c.isDark
                                ? const Color(0x1AFBFAF7)
                                : const Color(0x0F17140F))
                          : const Color(0x00000000)),
                borderRadius: BorderRadius.circular(IcRadius.xs),
              ),
              child: Stack(
                alignment: Alignment.center,
                children: [
                  AnimatedScale(
                    scale: _hover ? 1.15 : 1.0,
                    duration: IcMotion.fast,
                    curve: IcMotion.ease,
                    child: AppTile(
                      size: 18,
                      style: AppTileStyle.brand,
                      brandColor: widget.spec.color,
                      glyph: widget.spec.icon,
                    ),
                  ),
                  if (widget.isWorking)
                    Positioned(
                      right: 2,
                      top: 2,
                      child: Container(
                        width: 6,
                        height: 6,
                        decoration: BoxDecoration(
                          color: c.working,
                          shape: BoxShape.circle,
                        ),
                      ),
                    ),
                  if (widget.isOpen)
                    Positioned(
                      bottom: 1,
                      child: Container(
                        width: widget.isFocused ? 8 : 4,
                        height: 2,
                        decoration: BoxDecoration(
                          color: widget.isFocused ? c.accent : c.textStrong,
                          borderRadius: BorderRadius.circular(1),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _StatusTray extends StatelessWidget {
  const _StatusTray({
    required this.notifications,
    required this.tokensToday,
    required this.connected,
    this.onOpenNotifications,
    this.onOpenCompute,
    this.onOpenSettings,
  });

  final int notifications;
  final int tokensToday;
  final bool connected;
  final VoidCallback? onOpenNotifications;
  final VoidCallback? onOpenCompute;
  final VoidCallback? onOpenSettings;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final now = DateTime.now();
    final clock =
        '${now.hour.toString().padLeft(2, '0')}:${now.minute.toString().padLeft(2, '0')}';

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        _BarButton(
          tooltip: 'Compute tokens used today',
          onTap: onOpenCompute ?? () {},
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(LucideIcons.zap, size: 13, color: c.accent),
              const SizedBox(width: 4),
              Text(
                '${(tokensToday / 1000).toStringAsFixed(1)}k',
                style: IcType.text(
                  11.5,
                  weight: FontWeight.w600,
                  color: c.textBody,
                  tnum: true,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 4),
        _BarButton(
          tooltip: connected ? 'Brain connected (gRPC)' : 'Reconnecting…',
          onTap: onOpenSettings ?? () {},
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 7,
                height: 7,
                decoration: BoxDecoration(
                  color: connected ? c.done : c.working,
                  shape: BoxShape.circle,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 4),
        _BarButton(
          tooltip: 'Notifications',
          onTap: onOpenNotifications ?? () {},
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(LucideIcons.bell, size: 14, color: c.textBody),
              if (notifications > 0) ...[
                const SizedBox(width: 4),
                Container(
                  constraints: const BoxConstraints(minWidth: 15),
                  height: 15,
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: c.accent,
                    borderRadius: BorderRadius.circular(IcRadius.pill),
                  ),
                  child: Text(
                    '$notifications',
                    style: IcType.text(
                      10.5,
                      weight: FontWeight.w700,
                      color: c.onAccent,
                      tnum: true,
                    ),
                  ),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(width: 6),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4),
          child: Text(
            clock,
            style: IcType.text(
              12,
              weight: FontWeight.w600,
              color: c.textStrong,
              tnum: true,
            ),
          ),
        ),
      ],
    );
  }
}

class _BarButton extends StatefulWidget {
  const _BarButton({
    required this.child,
    required this.onTap,
    this.tooltip,
    this.selected = false,
  });

  final Widget child;
  final VoidCallback onTap;
  final String? tooltip;
  final bool selected;

  @override
  State<_BarButton> createState() => _BarButtonState();
}

class _BarButtonState extends State<_BarButton> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final button = MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          height: 28,
          padding: const EdgeInsets.symmetric(horizontal: 6),
          decoration: BoxDecoration(
            color: widget.selected
                ? c.accentTint
                : (_hover
                      ? (c.isDark
                            ? const Color(0x1AFBFAF7)
                            : const Color(0x0F17140F))
                      : const Color(0x00000000)),
            borderRadius: BorderRadius.circular(IcRadius.xs),
          ),
          alignment: Alignment.center,
          child: widget.child,
        ),
      ),
    );
    if (widget.tooltip == null) return button;
    return Tooltip(message: widget.tooltip!, child: button);
  }
}
