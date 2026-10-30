import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'smart_desktop.dart';
import 'window_frame.dart';

class CommandPaletteEntry {
  const CommandPaletteEntry({
    required this.id,
    required this.label,
    required this.kind,
    required this.icon,
    required this.onRun,
    this.detail,
    this.appId,
    this.accentHex,
  });

  final String id;
  final String label;
  final String kind;
  final String? detail;
  final IconData icon;
  final VoidCallback onRun;
  final String? appId;
  final String? accentHex;
}

/// Keyboard-first Raycast-style Command Palette (`⌘K` / `/`) across apps,
/// settings, UI identities, and Ino prompts.
class CommandPalette extends StatefulWidget {
  const CommandPalette({
    super.key,
    required this.onClose,
    this.onLaunchApp,
    this.onAskIno,
    this.onSelectPreset,
    this.onCycleBarLayout,
  });

  final VoidCallback onClose;
  final ValueChanged<String>? onLaunchApp;
  final ValueChanged<String>? onAskIno;
  final ValueChanged<IcStylePreset>? onSelectPreset;
  final VoidCallback? onCycleBarLayout;

  @override
  State<CommandPalette> createState() => _CommandPaletteState();
}

class _CommandPaletteState extends State<CommandPalette> {
  final _text = TextEditingController();
  final _focus = FocusNode();
  int _selectedIndex = 0;

  @override
  void initState() {
    super.initState();
    _text.addListener(() => setState(() => _selectedIndex = 0));
    WidgetsBinding.instance.addPostFrameCallback((_) => _focus.requestFocus());
  }

  @override
  void dispose() {
    _text.dispose();
    _focus.dispose();
    super.dispose();
  }

  List<CommandPaletteEntry> _buildEntries() {
    final q = _text.text.trim().toLowerCase();
    final all = <CommandPaletteEntry>[
      for (final spec in defaultSmartAppSpecs)
        CommandPaletteEntry(
          id: 'app:${spec.id}',
          label: 'Open ${spec.name}',
          kind: spec.category,
          detail: spec.neuronUri,
          icon: spec.icon,
          appId: spec.id,
          accentHex: spec.accentHex,
          onRun: () {
            widget.onClose();
            widget.onLaunchApp?.call(spec.id);
          },
        ),
      for (final preset in IcStylePreset.values)
        CommandPaletteEntry(
          id: 'preset:${preset.name}',
          label: 'UI Identity: ${preset.label}',
          kind: 'Theme',
          detail: preset.description,
          icon: LucideIcons.palette,
          onRun: () {
            widget.onSelectPreset?.call(preset);
            widget.onClose();
          },
        ),
      CommandPaletteEntry(
        id: 'bar:cycle',
        label: 'Cycle OS Bar Layout',
        kind: 'Desktop & Dock',
        detail: 'Floating Glass / Split Top / Classic Bottom / Compact Pill',
        icon: LucideIcons.panelBottom,
        onRun: () {
          widget.onCycleBarLayout?.call();
          widget.onClose();
        },
      ),
    ];

    final filtered = q.isEmpty
        ? all
        : all
              .where(
                (e) =>
                    e.label.toLowerCase().contains(q) ||
                    e.kind.toLowerCase().contains(q) ||
                    (e.detail?.toLowerCase().contains(q) ?? false),
              )
              .toList();

    if (q.isNotEmpty) {
      filtered.insert(
        0,
        CommandPaletteEntry(
          id: 'ask-ino',
          label: 'Ask Ino: "${_text.text.trim()}"',
          kind: 'Ino Assistant',
          detail: 'neuron://local/apps/ino',
          icon: LucideIcons.sparkles,
          appId: 'ino',
          onRun: () {
            final prompt = _text.text.trim();
            widget.onClose();
            widget.onAskIno?.call(prompt);
          },
        ),
      );
    }

    return filtered;
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final entries = _buildEntries();
    final safeIndex = entries.isEmpty
        ? 0
        : _selectedIndex.clamp(0, entries.length - 1);

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: widget.onClose,
      child: Container(
        color: const Color(0x55000000),
        alignment: const Alignment(0, -0.35),
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: GestureDetector(
          onTap: () {},
          child: Focus(
            onKeyEvent: (node, event) {
              if (event is! KeyDownEvent) return KeyEventResult.ignored;
              if (event.logicalKey == LogicalKeyboardKey.escape) {
                widget.onClose();
                return KeyEventResult.handled;
              }
              if (event.logicalKey == LogicalKeyboardKey.arrowDown &&
                  entries.isNotEmpty) {
                setState(
                  () => _selectedIndex = (_selectedIndex + 1) % entries.length,
                );
                return KeyEventResult.handled;
              }
              if (event.logicalKey == LogicalKeyboardKey.arrowUp &&
                  entries.isNotEmpty) {
                setState(
                  () => _selectedIndex =
                      (_selectedIndex - 1 + entries.length) % entries.length,
                );
                return KeyEventResult.handled;
              }
              if (event.logicalKey == LogicalKeyboardKey.enter &&
                  entries.isNotEmpty) {
                entries[safeIndex].onRun();
                return KeyEventResult.handled;
              }
              return KeyEventResult.ignored;
            },
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560, maxHeight: 420),
              child: IcAcrylicBox(
                radius: IcRadius.panel,
                padding: EdgeInsets.zero,
                shadows: IcShadow.xl(c),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 12, 16, 10),
                      child: Row(
                        children: [
                          Icon(LucideIcons.search, size: 16, color: c.accent),
                          const SizedBox(width: 10),
                          Expanded(
                            child: TextField(
                              key: const ValueKey('command-palette-input'),
                              controller: _text,
                              focusNode: _focus,
                              style: IcType.text(14, color: c.textStrong),
                              decoration: InputDecoration(
                                isDense: true,
                                border: InputBorder.none,
                                hintText:
                                    'Search apps, switch UI identity, or ask Ino…',
                                hintStyle: IcType.text(14, color: c.textMuted),
                              ),
                              onSubmitted: (_) {
                                if (entries.isNotEmpty) {
                                  entries[safeIndex].onRun();
                                }
                              },
                            ),
                          ),
                          const TagPill(label: 'ESC', tone: TagTone.neutral),
                        ],
                      ),
                    ),
                    Container(height: 1, color: c.borderHairline),
                    Flexible(
                      child: ListView.builder(
                        shrinkWrap: true,
                        padding: const EdgeInsets.all(8),
                        itemCount: entries.length,
                        itemBuilder: (context, index) {
                          final item = entries[index];
                          final selected = index == safeIndex;
                          return GestureDetector(
                            onTap: item.onRun,
                            child: MouseRegion(
                              cursor: SystemMouseCursors.click,
                              onEnter: (_) =>
                                  setState(() => _selectedIndex = index),
                              child: Container(
                                height: 38,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                ),
                                decoration: BoxDecoration(
                                  color: selected
                                      ? c.accentTint
                                      : const Color(0x00000000),
                                  borderRadius: BorderRadius.circular(
                                    IcRadius.sm,
                                  ),
                                ),
                                child: Row(
                                  children: [
                                    if (item.appId != null)
                                      IcAppIcon(
                                        appId: item.appId!,
                                        icon: item.icon,
                                        accentHex: item.accentHex,
                                        size: 20,
                                      )
                                    else
                                      Icon(
                                        item.icon,
                                        size: 15,
                                        color: selected ? c.accent : c.textBody,
                                      ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: Text(
                                        item.label,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: IcType.text(
                                          13,
                                          weight: selected
                                              ? FontWeight.w600
                                              : FontWeight.w500,
                                          color: c.textStrong,
                                        ),
                                      ),
                                    ),
                                    if (item.detail != null) ...[
                                      const SizedBox(width: 8),
                                      Flexible(
                                        child: Text(
                                          item.detail!,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: IcType.text(
                                            11.5,
                                            color: c.textMuted,
                                            tnum: true,
                                          ),
                                        ),
                                      ),
                                    ],
                                    const SizedBox(width: 8),
                                    TagPill(
                                      label: item.kind,
                                      tone: selected
                                          ? TagTone.accent
                                          : TagTone.neutral,
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          );
                        },
                      ),
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

/// Keyboard Window Switcher (`⌥` + `` ` ``) for cycling open Acrylic windows.
class WindowSwitcher extends StatelessWidget {
  const WindowSwitcher({
    super.key,
    required this.windows,
    required this.selectedIndex,
    required this.onSelect,
  });

  final List<DesktopWindowState> windows;
  final int selectedIndex;
  final ValueChanged<DesktopWindowState> onSelect;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    if (windows.isEmpty) return const SizedBox.shrink();
    final safeIdx = selectedIndex.clamp(0, windows.length - 1);

    return Center(
      child: IcAcrylicBox(
        radius: IcRadius.panel,
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
        shadows: IcShadow.xl(c),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < windows.length; i++)
              Padding(
                padding: EdgeInsets.only(right: i == windows.length - 1 ? 0 : 12),
                child: GestureDetector(
                  onTap: () => onSelect(windows[i]),
                  child: Container(
                    width: 120,
                    padding: const EdgeInsets.symmetric(
                      vertical: 12,
                      horizontal: 10,
                    ),
                    decoration: BoxDecoration(
                      color: i == safeIdx ? c.accentTint : c.surfaceSunk,
                      borderRadius: BorderRadius.circular(IcRadius.card),
                      border: Border.all(
                        color: i == safeIdx ? c.accent : c.borderHairline,
                      ),
                    ),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        IcAppIcon(
                          appId: windows[i].appId,
                          accentHex: windows[i].accentHex,
                          size: 36,
                        ),
                        const SizedBox(height: 8),
                        Text(
                          windows[i].title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: IcType.text(
                            12,
                            weight: FontWeight.w600,
                            color: c.textStrong,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          windows[i].neuronUri,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: IcType.text(10, color: c.textMuted, tnum: true),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
