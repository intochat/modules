import 'dart:math' as math;
import 'dart:ui';

import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'os_bar.dart';
import 'smart_desktop.dart';

enum DesktopPopoverKind { none, clock, tokens, notifications, dockMenu }

enum DesktopSystemSheetKind { none, secure, profile, install, reconsent, conflict, twoFactor }

@immutable
class DesktopWindowSummary {
  const DesktopWindowSummary({
    required this.windowId,
    required this.numericId,
    required this.appId,
    required this.title,
    this.minimized = false,
    this.isPinned = true,
  });

  final String windowId;
  final int numericId;
  final String appId;
  final String title;
  final bool minimized;
  final bool isPinned;
}

/// Unclipped hover tooltip rendered above the `36px` `OsBar`.
class BarTooltip extends StatelessWidget {
  const BarTooltip({
    super.key,
    required this.screen,
    required this.barLayout,
    this.label,
    this.anchorX = 0,
  });

  final Size screen;
  final BarLayout barLayout;
  final String? label;
  final double anchorX;

  @override
  Widget build(BuildContext context) {
    final text = label;
    if (text == null || text.isEmpty) return const SizedBox.shrink();
    final c = IcTheme.of(context);
    final bottomOffset =
        kOsBarHeight + (barLayout == BarLayout.classicBottom ? 8.0 : 14.0);
    return Stack(
      children: [
        Positioned(
          left: (anchorX - 80).clamp(8.0, math.max(8.0, screen.width - 180.0)),
          bottom: bottomOffset,
          child: IgnorePointer(
            child: Container(
              key: const ValueKey('os-bar-tooltip'),
              height: 24,
              padding: const EdgeInsets.symmetric(horizontal: 8),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: c.surfaceElevated,
                borderRadius: BorderRadius.circular(IcRadius.xs),
                border: Border.all(color: c.borderHairline),
                boxShadow: IcShadow.sm(c),
              ),
              child: Text(
                text,
                style: IcType.text(
                  12,
                  weight: FontWeight.w500,
                  color: c.textStrong,
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// Acrylic flyout popover for Clock, Tokens & Compute, Notifications, and
/// Right-Click Dock Context Menu.
class DesktopBarPopover extends StatelessWidget {
  const DesktopBarPopover({
    super.key,
    required this.kind,
    required this.screen,
    required this.barLayout,
    this.anchorX = 0,
    this.dockAppId,
    this.tokensToday = 14800,
    this.notificationsCount = 2,
    this.pinnedAppIds = const {'ino', 'files', 'creator', 'gallery', 'post', 'settings'},
    this.windows = const [],
    required this.onClose,
    this.onOpenSettings,
    this.onOpenComputeSettings,
    this.onClearNotifications,
    this.onFocusWindow,
    this.onNewWindowForApp,
    this.onTogglePinApp,
    this.onQuitApp,
  });

  final DesktopPopoverKind kind;
  final Size screen;
  final BarLayout barLayout;
  final double anchorX;
  final String? dockAppId;
  final int tokensToday;
  final int notificationsCount;
  final Set<String> pinnedAppIds;
  final List<DesktopWindowSummary> windows;
  final VoidCallback onClose;
  final VoidCallback? onOpenSettings;
  final VoidCallback? onOpenComputeSettings;
  final VoidCallback? onClearNotifications;
  final ValueChanged<String>? onFocusWindow;
  final ValueChanged<String>? onNewWindowForApp;
  final ValueChanged<String>? onTogglePinApp;
  final ValueChanged<String>? onQuitApp;

  @override
  Widget build(BuildContext context) {
    if (kind == DesktopPopoverKind.none) return const SizedBox.shrink();
    final c = IcTheme.of(context);

    final (double width, Widget content, Key popoverKey) = switch (kind) {
      DesktopPopoverKind.clock => (
        268.0,
        const _ClockPopover(),
        const ValueKey('desktop-popover-clock'),
      ),
      DesktopPopoverKind.tokens => (
        340.0,
        _TokensPopover(
          tokensToday: tokensToday,
          onOpenCompute: () {
            onClose();
            if (onOpenComputeSettings != null) {
              onOpenComputeSettings!();
            } else {
              onOpenSettings?.call();
            }
          },
        ),
        const ValueKey('desktop-popover-tokens'),
      ),
      DesktopPopoverKind.notifications => (
        360.0,
        _NotificationsPopover(
          notificationsCount: notificationsCount,
          onFocusIno: () {
            onClose();
            for (final w in windows) {
              if (w.appId == 'ino') {
                onFocusWindow?.call(w.windowId);
                return;
              }
            }
            onNewWindowForApp?.call('ino');
          },
          onOpenSettings: () {
            onClose();
            onOpenSettings?.call();
          },
          onMarkAllRead: () {
            onClearNotifications?.call();
            onClose();
          },
        ),
        const ValueKey('desktop-popover-notifications'),
      ),
      DesktopPopoverKind.dockMenu => (
        228.0,
        _DockContextMenu(
          appId: dockAppId ?? 'ino',
          isPinned: pinnedAppIds.contains(dockAppId ?? 'ino'),
          appWindows: [
            for (final w in windows)
              if (w.appId == (dockAppId ?? 'ino')) w,
          ],
          onFocusWindow: (winId) {
            onClose();
            onFocusWindow?.call(winId);
          },
          onNewWindow: () {
            final target = dockAppId ?? 'ino';
            onClose();
            onNewWindowForApp?.call(target);
          },
          onTogglePin: () {
            final target = dockAppId ?? 'ino';
            onTogglePinApp?.call(target);
            onClose();
          },
          onOpenSettings: () {
            onClose();
            onOpenSettings?.call();
          },
          onQuit: () {
            final target = dockAppId ?? 'ino';
            onClose();
            onQuitApp?.call(target);
          },
        ),
        const ValueKey('desktop-popover-dock-menu'),
      ),
      DesktopPopoverKind.none => (0.0, const SizedBox.shrink(), const ValueKey('none')),
    };

    final double left = kind == DesktopPopoverKind.dockMenu
        ? (anchorX - width / 2).clamp(8.0, math.max(8.0, screen.width - width - 8.0))
        : math.max(8.0, screen.width - width - 12.0);
    final bottomOffset =
        kOsBarHeight + (barLayout == BarLayout.classicBottom ? 8.0 : 14.0);

    return Positioned(
      left: left,
      bottom: bottomOffset,
      width: width,
      child: ClipRRect(
        key: popoverKey,
        borderRadius: BorderRadius.circular(IcRadius.card),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
          child: Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: c.surfaceElevated.withValues(alpha: 0.94),
              borderRadius: BorderRadius.circular(IcRadius.card),
              border: Border.all(
                color: c.isGlass ? c.specularBorder : c.borderHairline,
              ),
              boxShadow: IcShadow.lg(c),
            ),
            child: content,
          ),
        ),
      ),
    );
  }
}

class _ClockPopover extends StatelessWidget {
  const _ClockPopover();

  static const _weekdays = [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ];

  static const _months = [
    'January',
    'February',
    'March',
    'April',
    'May',
    'June',
    'July',
    'August',
    'September',
    'October',
    'November',
    'December',
  ];

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final now = DateTime.now();
    final headerText =
        '${_weekdays[now.weekday - 1]}, ${now.day} ${_months[now.month - 1]}';
    final firstOfMonth = DateTime(now.year, now.month, 1);
    final startOffset = firstOfMonth.weekday - 1; // 0 = Mon .. 6 = Sun
    final daysInMonth = DateUtils.getDaysInMonth(now.year, now.month);
    final cells = <int?>[
      for (var i = 0; i < startOffset; i++) null,
      for (var d = 1; d <= daysInMonth; d++) d,
    ];
    while (cells.length % 7 != 0) {
      cells.add(null);
    }

    return Padding(
      padding: const EdgeInsets.all(6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            headerText,
            style: IcType.text(
              14,
              weight: FontWeight.w600,
              color: c.textStrong,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            '${_months[now.month - 1]} ${now.year} · DigitalBrain Schedule',
            style: IcType.text(12, color: c.textMuted),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              for (final d in const ['M', 'T', 'W', 'T', 'F', 'S', 'S'])
                Expanded(
                  child: Center(
                    child: Text(
                      d,
                      style: IcType.text(
                        11,
                        weight: FontWeight.w600,
                        color: c.textSubtle,
                      ),
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 4),
          for (var r = 0; r < cells.length ~/ 7; r++)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                children: [
                  for (var col = 0; col < 7; col++)
                    Expanded(
                      child: Builder(
                        builder: (_) {
                          final day = cells[r * 7 + col];
                          if (day == null) return const SizedBox(height: 26);
                          final today = day == now.day;
                          return Container(
                            height: 26,
                            alignment: Alignment.center,
                            decoration: BoxDecoration(
                              color: today
                                  ? c.accent
                                  : const Color(0x00000000),
                              borderRadius: BorderRadius.circular(IcRadius.xs),
                            ),
                            child: Text(
                              '$day',
                              style: IcType.text(
                                12,
                                weight: today
                                    ? FontWeight.w600
                                    : FontWeight.w400,
                                color: today ? c.onAccent : c.textBody,
                                tnum: true,
                              ),
                            ),
                          );
                        },
                      ),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class _TokensPopover extends StatelessWidget {
  const _TokensPopover({
    required this.tokensToday,
    required this.onOpenCompute,
  });

  final int tokensToday;
  final VoidCallback onOpenCompute;

  static String _fmt(int n) {
    final s = n.toString();
    final buf = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) buf.write(',');
      buf.write(s[i]);
    }
    return buf.toString();
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    const sessions = [
      ('Nordlys Q4 Renewal', '6,420'),
      ('Morning Brief', '4,110'),
      ('Invoice check', '2,890'),
      ('Other sessions', '1,380'),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(8, 4, 8, 6),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Tokens today',
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    12,
                    weight: FontWeight.w600,
                    color: c.textMuted,
                  ),
                ),
              ),
              Text(
                _fmt(tokensToday),
                style: IcType.text(
                  15,
                  weight: FontWeight.w600,
                  color: c.textStrong,
                  tnum: true,
                ),
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Estimated cost',
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(12, color: c.textMuted),
                ),
              ),
              Text(
                '~\$0.18 · 412k/mo',
                style: IcType.text(12, color: c.textMuted, tnum: true),
              ),
            ],
          ),
        ),
        const _PopoverDivider(),
        for (final s in sessions)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    s.$1,
                    overflow: TextOverflow.ellipsis,
                    style: IcType.text(12.5, color: c.textBody),
                  ),
                ),
                Text(
                  s.$2,
                  style: IcType.text(12, color: c.textMuted, tnum: true),
                ),
              ],
            ),
          ),
        const _PopoverDivider(),
        _PopoverItem(
          key: const ValueKey('popover-tokens-open-compute'),
          icon: LucideIcons.coins,
          label: 'Credits and usage',
          trailing: 'Compute',
          onTap: onOpenCompute,
        ),
      ],
    );
  }
}

class _NotificationsPopover extends StatelessWidget {
  const _NotificationsPopover({
    required this.notificationsCount,
    required this.onFocusIno,
    required this.onOpenSettings,
    required this.onMarkAllRead,
  });

  final int notificationsCount;
  final VoidCallback onFocusIno;
  final VoidCallback onOpenSettings;
  final VoidCallback onMarkAllRead;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final items = [
      (
        LucideIcons.hand,
        c.working,
        'Needs you · Approve Nordlys close date',
        'Move CloseDate to 15 Nov in Salesforce',
        '2m',
      ),
      (
        LucideIcons.sparkles,
        c.accent,
        'Working · Morning Brief finished',
        '3 meetings, 2 renewals, 1 overdue invoice',
        '18m',
      ),
      (
        LucideIcons.triangleAlert,
        c.error,
        'Error · Tripletex session expired',
        'Sign in again so Ino can read Q4 invoices',
        '1h',
      ),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(8, 4, 8, 6),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Notifications',
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    12,
                    weight: FontWeight.w600,
                    color: c.textMuted,
                  ),
                ),
              ),
              if (notificationsCount > 0)
                GestureDetector(
                  key: const ValueKey('popover-notifications-mark-read'),
                  onTap: onMarkAllRead,
                  child: MouseRegion(
                    cursor: SystemMouseCursors.click,
                    child: Text(
                      'Mark all as read',
                      style: IcType.text(
                        12,
                        weight: FontWeight.w500,
                        color: c.accent,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
        for (final it in items)
          _PopoverRow(
            onTap: onFocusIno,
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Icon(it.$1, size: 15, color: it.$2),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          it.$3,
                          style: IcType.text(
                            13,
                            weight: FontWeight.w600,
                            color: c.textStrong,
                          ),
                        ),
                        const SizedBox(height: 1),
                        Text(
                          it.$4,
                          style: IcType.text(12, color: c.textMuted),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(
                    it.$5,
                    style: IcType.text(11.5, color: c.textSubtle, tnum: true),
                  ),
                ],
              ),
            ),
          ),
        const _PopoverDivider(),
        _PopoverItem(
          key: const ValueKey('popover-notifications-settings'),
          icon: LucideIcons.bell,
          label: 'Notification settings',
          onTap: onOpenSettings,
        ),
      ],
    );
  }
}

class _DockContextMenu extends StatelessWidget {
  const _DockContextMenu({
    required this.appId,
    required this.isPinned,
    required this.appWindows,
    required this.onFocusWindow,
    required this.onNewWindow,
    required this.onTogglePin,
    required this.onOpenSettings,
    required this.onQuit,
  });

  final String appId;
  final bool isPinned;
  final List<DesktopWindowSummary> appWindows;
  final ValueChanged<String> onFocusWindow;
  final VoidCallback onNewWindow;
  final VoidCallback onTogglePin;
  final VoidCallback onOpenSettings;
  final VoidCallback onQuit;

  String get _appName {
    for (final s in defaultSmartAppSpecs) {
      if (s.id == appId) return s.name;
    }
    return appId.isEmpty
        ? 'App'
        : '${appId[0].toUpperCase()}${appId.substring(1)}';
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(8, 4, 8, 6),
          child: Row(
            children: [
              IcAppIcon(appId: appId, size: 18),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  _appName,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    12.5,
                    weight: FontWeight.w700,
                    color: c.textStrong,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (appWindows.isNotEmpty) ...[
          for (final w in appWindows)
            _PopoverItem(
              icon: LucideIcons.appWindow,
              label: w.title,
              onTap: () => onFocusWindow(w.windowId),
            ),
          const _PopoverDivider(),
        ],
        _PopoverItem(
          key: const ValueKey('dock-menu-new-window'),
          icon: LucideIcons.plus,
          label: 'New window',
          onTap: onNewWindow,
        ),
        _PopoverItem(
          key: const ValueKey('dock-menu-pin'),
          icon: isPinned ? LucideIcons.pinOff : LucideIcons.pin,
          label: isPinned ? 'Unpin from Dock' : 'Pin to Dock',
          onTap: onTogglePin,
        ),
        _PopoverItem(
          key: const ValueKey('dock-menu-settings'),
          icon: LucideIcons.slidersHorizontal,
          label: 'App settings…',
          onTap: onOpenSettings,
        ),
        if (appWindows.isNotEmpty) ...[
          const _PopoverDivider(),
          _PopoverItem(
            key: const ValueKey('dock-menu-quit'),
            icon: LucideIcons.x,
            label: 'Quit $_appName',
            danger: true,
            onTap: onQuit,
          ),
        ],
      ],
    );
  }
}

class _PopoverDivider extends StatelessWidget {
  const _PopoverDivider();

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return Container(
      height: 1,
      margin: const EdgeInsets.symmetric(vertical: 6),
      color: c.borderHairline,
    );
  }
}

class _PopoverItem extends StatelessWidget {
  const _PopoverItem({
    super.key,
    required this.icon,
    required this.label,
    required this.onTap,
    this.trailing,
    this.danger = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final String? trailing;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final fg = danger ? c.error : c.textStrong;
    return _PopoverRow(
      onTap: onTap,
      child: SizedBox(
        height: 28,
        child: Row(
          children: [
            Icon(icon, size: 15, color: danger ? c.error : c.textMuted),
            const SizedBox(width: 9),
            Expanded(
              child: Text(
                label,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(13, weight: FontWeight.w500, color: fg),
              ),
            ),
            if (trailing != null)
              Text(trailing!, style: IcType.text(12, color: c.textMuted)),
          ],
        ),
      ),
    );
  }
}

class _PopoverRow extends StatefulWidget {
  const _PopoverRow({required this.child, required this.onTap});

  final Widget child;
  final VoidCallback onTap;

  @override
  State<_PopoverRow> createState() => _PopoverRowState();
}

class _PopoverRowState extends State<_PopoverRow> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: widget.onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
          decoration: BoxDecoration(
            color: _hover ? c.surfaceSunk : const Color(0x00000000),
            borderRadius: BorderRadius.circular(IcRadius.sm),
          ),
          child: widget.child,
        ),
      ),
    );
  }
}

/// `Alt+\`` (`⌥\``) visual MRU window thumbnail switcher with `IcAppIcon` previews.
class AltBacktickWindowSwitcher extends StatelessWidget {
  const AltBacktickWindowSwitcher({
    super.key,
    required this.windows,
    required this.selectedIndex,
    required this.onSelectWindow,
    required this.onCancel,
  });

  final List<DesktopWindowSummary> windows;
  final int selectedIndex;
  final ValueChanged<DesktopWindowSummary> onSelectWindow;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    if (windows.isEmpty) return const SizedBox.shrink();
    final c = IcTheme.of(context);

    return GestureDetector(
      key: const ValueKey('desktop-window-switcher'),
      behavior: HitTestBehavior.opaque,
      onTap: onCancel,
      child: ColoredBox(
        color: Color.fromRGBO(0, 0, 0, c.isDark ? 0.42 : 0.22),
        child: Center(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(IcRadius.window),
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 20, sigmaY: 20),
              child: Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: c.surfaceElevated.withValues(alpha: 0.94),
                  borderRadius: BorderRadius.circular(IcRadius.window),
                  border: Border.all(
                    color: c.isGlass ? c.specularBorder : c.borderHairline,
                  ),
                  boxShadow: IcShadow.xl(c),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        for (var i = 0; i < windows.length; i++) ...[
                          if (i > 0) const SizedBox(width: 10),
                          _WindowThumb(
                            window: windows[i],
                            selected: i == selectedIndex,
                            onTap: () => onSelectWindow(windows[i]),
                          ),
                        ],
                      ],
                    ),
                    const SizedBox(height: 10),
                    Text(
                      '⌥`   Release ⌥ to switch  ·  Esc to cancel',
                      style: IcType.text(12, color: c.textMuted),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _WindowThumb extends StatefulWidget {
  const _WindowThumb({
    required this.window,
    required this.selected,
    required this.onTap,
  });

  final DesktopWindowSummary window;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_WindowThumb> createState() => _WindowThumbState();
}

class _WindowThumbState extends State<_WindowThumb> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final sel = widget.selected;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          width: 168,
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: sel
                ? c.accentTint
                : (_hover ? c.surfaceSunk : c.surfaceCard),
            borderRadius: BorderRadius.circular(IcRadius.card),
            border: Border.all(
              color: sel ? c.accent : c.borderHairline,
              width: sel ? 2 : 1,
            ),
          ),
          child: Column(
            children: [
              Container(
                height: 84,
                decoration: BoxDecoration(
                  color: c.surfacePage,
                  borderRadius: BorderRadius.circular(IcRadius.sm),
                  border: Border.all(color: c.borderHairline),
                ),
                child: Column(
                  children: [
                    Container(
                      height: 16,
                      padding: const EdgeInsets.symmetric(horizontal: 6),
                      decoration: BoxDecoration(
                        color: c.titleBarFocused,
                        borderRadius: const BorderRadius.vertical(
                          top: Radius.circular(IcRadius.sm - 1),
                        ),
                      ),
                      child: Row(
                        children: [
                          IcAppIcon(appId: widget.window.appId, size: 10),
                          const SizedBox(width: 4),
                          Expanded(
                            child: Text(
                              widget.window.title,
                              overflow: TextOverflow.ellipsis,
                              style: IcType.text(9.5, color: c.textMuted),
                            ),
                          ),
                        ],
                      ),
                    ),
                    Expanded(
                      child: Center(
                        child: IcAppIcon(appId: widget.window.appId, size: 30),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              Text(
                widget.window.title,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(
                  12.5,
                  weight: FontWeight.w600,
                  color: c.textStrong,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Declarative `os://` System Window & Modal Sheet overlay supporting:
/// - `os://window/secure` (Secure Credentials Vault, `480×360`)
/// - `os://window/profile` (Profile & Identity, `540×620`)
/// - `os://sheet/install` (2-Step Install by Link Sheet)
/// - `os://sheet/reconsent` (Update Capability Re-Consent Sheet)
/// - `os://sheet/conflict` (Two-Version File Conflict Sheet)
/// - `os://sheet/2fa` (Hardware TOTP 2FA Sheet)
class DesktopSystemModalSheet extends StatefulWidget {
  const DesktopSystemModalSheet({
    super.key,
    required this.kind,
    required this.onClose,
    this.onOpenSheet,
  });

  final DesktopSystemSheetKind kind;
  final VoidCallback onClose;
  final ValueChanged<DesktopSystemSheetKind>? onOpenSheet;

  static DesktopSystemSheetKind parseUri(String uriString) {
    final lower = uriString.toLowerCase();
    if (lower.contains('secure')) return DesktopSystemSheetKind.secure;
    if (lower.contains('profile')) return DesktopSystemSheetKind.profile;
    if (lower.contains('install')) return DesktopSystemSheetKind.install;
    if (lower.contains('reconsent')) return DesktopSystemSheetKind.reconsent;
    if (lower.contains('conflict')) return DesktopSystemSheetKind.conflict;
    if (lower.contains('2fa')) return DesktopSystemSheetKind.twoFactor;
    return DesktopSystemSheetKind.none;
  }

  @override
  State<DesktopSystemModalSheet> createState() => _DesktopSystemModalSheetState();
}

class _DesktopSystemModalSheetState extends State<DesktopSystemModalSheet> {
  int _installStep = 0;
  bool _secretSaved = false;
  final _secretController = TextEditingController(text: 'sk_live_nordlys_9948104a');
  final _secretFocus = FocusNode();

  @override
  void dispose() {
    _secretController.dispose();
    _secretFocus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.kind == DesktopSystemSheetKind.none) return const SizedBox.shrink();
    final c = IcTheme.of(context);
    final (String title, String appId, double width, Widget body) = switch (widget.kind) {
      DesktopSystemSheetKind.secure => (
        'Secure · Zero-Leakage Credential Vault',
        'settings',
        480.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 4,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                const TagPill(label: 'Zero LLM Exposure', tone: TagTone.done, icon: LucideIcons.shieldCheck),
                Text('neuron://local/vault/secrets', style: IcType.code(11.5, color: c.textMuted)),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              'Secrets entered here are encrypted directly into your personal Vault grain and never appear in chat transcripts or LLM prompts.',
              style: IcType.text(12.5, color: c.textBody),
            ),
            const SizedBox(height: 12),
            Container(
              height: 36,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              decoration: BoxDecoration(
                color: c.surfaceSunk,
                borderRadius: BorderRadius.circular(IcRadius.input),
                border: Border.all(color: c.borderInput),
              ),
              alignment: Alignment.centerLeft,
              child: Field(
                controller: _secretController,
                focusNode: _secretFocus,
                obscure: true,
                placeholder: 'Paste secret token…',
              ),
            ),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                Btn(label: 'Cancel', small: true, onTap: widget.onClose),
                const SizedBox(width: 8),
                Btn(
                  key: const ValueKey('sheet-secure-save'),
                  label: _secretSaved ? 'Saved to Vault ✓' : 'Seal in Vault',
                  small: true,
                  primary: true,
                  icon: LucideIcons.keyRound,
                  onTap: () => setState(() => _secretSaved = true),
                ),
              ],
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.profile => (
        'Profile · DigitalBrain Identity & Sessions',
        'circle',
        540.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                const IcAppIcon(appId: 'circle', size: 44),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Jeff Tilley (@jeff)', style: IcType.heading(18, color: c.textStrong)),
                      Text('neuron://nordlys/os.profile/profile · Oslo, Norway', style: IcType.code(11.5, color: c.textMuted)),
                    ],
                  ),
                ),
                const TagPill(label: '2FA Active', tone: TagTone.done, icon: LucideIcons.shieldCheck),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Btn(
                  key: const ValueKey('sheet-profile-2fa'),
                  label: 'Configure 2FA (TOTP)',
                  small: true,
                  icon: LucideIcons.keyRound,
                  onTap: () => widget.onOpenSheet?.call(DesktopSystemSheetKind.twoFactor),
                ),
                const SizedBox(width: 8),
                Btn(label: 'Close', small: true, primary: true, onTap: widget.onClose),
              ],
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.install => (
        _installStep == 0 ? 'Install Smart App · Step 1 of 2 (Manifest)' : 'Install Smart App · Step 2 of 2 (Capabilities)',
        'creator',
        500.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                const IcAppIcon(appId: 'datavisualizer', size: 40),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Tripletex Accounting Sync (v1.4.0)', style: IcType.text(14, weight: FontWeight.w700, color: c.textStrong)),
                      Text('Publisher: Nordlys Digital · Verified C# App<TState>', style: IcType.text(12, color: c.textMuted)),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text(
              _installStep == 0
                  ? 'Step 1: Verify publisher signature and BDD [Scenario] gate (14/14 passing).'
                  : 'Step 2: Grant read-only access to Files (30-projects) and Postgres (nordlys_crm).',
              style: IcType.text(12.5, color: c.textBody),
            ),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                Btn(label: 'Cancel', small: true, onTap: widget.onClose),
                const SizedBox(width: 8),
                Btn(
                  key: const ValueKey('sheet-install-next'),
                  label: _installStep == 0 ? 'Review Capabilities →' : 'Install & Pin to Dock',
                  small: true,
                  primary: true,
                  icon: LucideIcons.packagePlus,
                  onTap: () {
                    if (_installStep == 0) {
                      setState(() => _installStep = 1);
                    } else {
                      widget.onClose();
                    }
                  },
                ),
              ],
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.reconsent => (
        'Update Re-Consent · Capability Diff',
        'settings',
        480.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            const TagPill(label: '+1 New Capability Requested', tone: TagTone.working, icon: LucideIcons.shieldAlert),
            const SizedBox(height: 10),
            Text('Tripletex Sync v1.5.0 requests write access to Salesforce Opportunities (gated by LFP ApprovalCard).', style: IcType.text(12.5, color: c.textBody)),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                Btn(label: 'Keep v1.4.0', small: true, onTap: widget.onClose),
                const SizedBox(width: 8),
                Btn(key: const ValueKey('sheet-reconsent-approve'), label: 'Approve & Update', small: true, primary: true, onTap: widget.onClose),
              ],
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.conflict => (
        'Two-Version File Conflict · nordlys-q4-renewal.md',
        'files',
        520.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Both your local editor (11:40) and Ino (11:42) modified CloseDate in nordlys-q4-renewal.md.', style: IcType.text(12.5, color: c.textBody)),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                Btn(label: 'Keep Local', small: true, onTap: widget.onClose),
                const SizedBox(width: 8),
                Btn(label: 'Save Both', small: true, onTap: widget.onClose),
                const SizedBox(width: 8),
                Btn(key: const ValueKey('sheet-conflict-keep-ino'), label: 'Keep Ino Version (15 Nov)', small: true, primary: true, onTap: widget.onClose),
              ],
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.twoFactor => (
        'Two-Factor Authentication (TOTP)',
        'settings',
        440.0,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Hardware authenticator & recovery keys are active for neuron://nordlys/os.profile.', style: IcType.text(12.5, color: c.textBody)),
            const SizedBox(height: 14),
            Align(
              alignment: Alignment.centerRight,
              child: Btn(label: 'Done', small: true, primary: true, onTap: widget.onClose),
            ),
          ],
        ),
      ),
      DesktopSystemSheetKind.none => ('', 'settings', 400.0, const SizedBox.shrink()),
    };

    return GestureDetector(
      key: ValueKey('desktop-system-sheet-${widget.kind.name}'),
      behavior: HitTestBehavior.opaque,
      onTap: widget.onClose,
      child: ColoredBox(
        color: const Color(0x66000000),
        child: Center(
          child: GestureDetector(
            onTap: () {},
            child: ClipRRect(
              borderRadius: BorderRadius.circular(IcRadius.window),
              child: BackdropFilter(
                filter: ImageFilter.blur(sigmaX: 24, sigmaY: 24),
                child: Container(
                  width: width,
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: c.surfaceElevated.withValues(alpha: 0.96),
                    borderRadius: BorderRadius.circular(IcRadius.window),
                    border: Border.all(color: c.isGlass ? c.specularBorder : c.borderHairline),
                    boxShadow: IcShadow.xl(c),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          IcAppIcon(appId: appId, size: 24),
                          const SizedBox(width: 10),
                          Expanded(child: Text(title, style: IcType.text(14, weight: FontWeight.w700, color: c.textStrong))),
                          IconButton(
                            key: const ValueKey('desktop-system-sheet-close'),
                            icon: Icon(LucideIcons.x, size: 16, color: c.textMuted),
                            onPressed: widget.onClose,
                          ),
                        ],
                      ),
                      const Divider(height: 20),
                      body,
                    ],
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
