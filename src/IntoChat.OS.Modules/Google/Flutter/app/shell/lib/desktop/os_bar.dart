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
    this.stylePreset = IcStylePreset.fluentAcrylic,
    this.apps = defaultSmartAppSpecs,
    this.openAppIds = const {'ino'},
    this.workingAppIds = const {},
    this.focusedAppId = 'ino',
    this.paletteOpen = false,
    this.showingDesktop = false,
    this.notifications = 2,
    this.tokensToday = 14800,
    this.connected = true,
    this.onTogglePalette,
    this.onToggleShowDesktop,
    this.onCycleStylePreset,
    this.onLaunchApp,
    this.onSecondaryTapApp,
    this.onHoverTooltip,
    this.onOpenSettings,
    this.onOpenNotifications,
    this.onOpenCompute,
    this.onOpenClock,
  });

  final BarLayout barLayout;
  final IcStylePreset stylePreset;
  final List<SmartAppTileSpec> apps;
  final Set<String> openAppIds;
  final Set<String> workingAppIds;
  final String? focusedAppId;
  final bool paletteOpen;
  final bool showingDesktop;
  final int notifications;
  final int tokensToday;
  final bool connected;
  final VoidCallback? onTogglePalette;
  final VoidCallback? onToggleShowDesktop;
  final VoidCallback? onCycleStylePreset;
  final ValueChanged<String>? onLaunchApp;
  final void Function(String appId, Offset globalPos)? onSecondaryTapApp;
  final void Function(String? label, double anchorX)? onHoverTooltip;
  final VoidCallback? onOpenSettings;
  final VoidCallback? onOpenNotifications;
  final VoidCallback? onOpenCompute;
  final VoidCallback? onOpenClock;

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
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                _BarButton(
                  tooltip: 'Search or ask  ⌘K',
                  selected: paletteOpen,
                  onTap: onTogglePalette ?? () {},
                  child: Icon(
                    LucideIcons.command,
                    size: 16,
                    color: c.textStrong,
                  ),
                ),
                if (onToggleShowDesktop != null) ...[
                  const SizedBox(width: 4),
                  _BarButton(
                    tooltip: showingDesktop
                        ? 'Restore open windows'
                        : 'Show 3D DigitalBrain Desktop',
                    selected: showingDesktop,
                    onTap: onToggleShowDesktop!,
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          LucideIcons.brainCircuit,
                          size: 14,
                          color: showingDesktop ? c.accent : c.textBody,
                        ),
                        const SizedBox(width: 5),
                        Text(
                          '3D Brain',
                          style: IcType.text(
                            11.5,
                            weight: FontWeight.w600,
                            color: showingDesktop ? c.textStrong : c.textBody,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final spec in apps)
                _DockAppItem(
                  key: ValueKey('dock-item-${spec.id}'),
                  spec: spec,
                  isOpen: openAppIds.contains(spec.id),
                  isWorking: workingAppIds.contains(spec.id),
                  isFocused: focusedAppId == spec.id,
                  onTap: () => onLaunchApp?.call(spec.id),
                  onSecondaryTapDown: (d) =>
                      onSecondaryTapApp?.call(spec.id, d.globalPosition),
                  onHoverChange: (hover, globalX) => onHoverTooltip?.call(
                    hover ? '${spec.name} (${spec.neuronUri})' : null,
                    globalX,
                  ),
                ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: _StatusTray(
              stylePreset: stylePreset,
              notifications: notifications,
              tokensToday: tokensToday,
              connected: connected,
              onCycleStylePreset: onCycleStylePreset,
              onOpenNotifications: onOpenNotifications,
              onOpenCompute: onOpenCompute,
              onOpenSettings: onOpenSettings,
              onOpenClock: onOpenClock,
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
    super.key,
    required this.spec,
    required this.isOpen,
    required this.isWorking,
    required this.isFocused,
    required this.onTap,
    this.onSecondaryTapDown,
    this.onHoverChange,
  });

  final SmartAppTileSpec spec;
  final bool isOpen;
  final bool isWorking;
  final bool isFocused;
  final VoidCallback onTap;
  final GestureTapDownCallback? onSecondaryTapDown;
  final void Function(bool hover, double globalX)? onHoverChange;

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
        onEnter: (e) {
          setState(() => _hover = true);
          widget.onHoverChange?.call(true, e.position.dx);
        },
        onExit: (e) {
          setState(() => _hover = false);
          widget.onHoverChange?.call(false, e.position.dx);
        },
        child: Tooltip(
          message: '${widget.spec.name} (${widget.spec.neuronUri})',
          child: GestureDetector(
            onTap: widget.onTap,
            onSecondaryTapDown: widget.onSecondaryTapDown,
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
                    child: IcAppIcon(
                      appId: widget.spec.id,
                      icon: widget.spec.icon,
                      accentHex: widget.spec.accentHex,
                      size: 20,
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
    required this.stylePreset,
    required this.notifications,
    required this.tokensToday,
    required this.connected,
    this.onCycleStylePreset,
    this.onOpenNotifications,
    this.onOpenCompute,
    this.onOpenSettings,
    this.onOpenClock,
  });

  final IcStylePreset stylePreset;
  final int notifications;
  final int tokensToday;
  final bool connected;
  final VoidCallback? onCycleStylePreset;
  final VoidCallback? onOpenNotifications;
  final VoidCallback? onOpenCompute;
  final VoidCallback? onOpenSettings;
  final VoidCallback? onOpenClock;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final now = DateTime.now();
    final clock =
        '${now.hour.toString().padLeft(2, '0')}:${now.minute.toString().padLeft(2, '0')}';

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (onCycleStylePreset != null) ...[
          _BarButton(
            tooltip: 'Switch UI Identity (${stylePreset.label})',
            onTap: onCycleStylePreset!,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(LucideIcons.palette, size: 13, color: c.accent),
                const SizedBox(width: 4),
                Text(
                  stylePreset.label,
                  style: IcType.text(
                    11,
                    weight: FontWeight.w600,
                    color: c.textBody,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 4),
        ],
        _BarButton(
          key: const ValueKey('os-bar-compute'),
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
          key: const ValueKey('os-bar-notifications'),
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
        const SizedBox(width: 4),
        _BarButton(
          key: const ValueKey('os-bar-clock'),
          tooltip: 'Calendar & Schedule',
          onTap: onOpenClock ?? () {},
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
    super.key,
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
