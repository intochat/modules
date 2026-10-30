import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

class SmartAppTileSpec {
  const SmartAppTileSpec({
    required this.id,
    required this.name,
    required this.icon,
    required this.color,
    required this.neuronUri,
  });

  final String id;
  final String name;
  final IconData icon;
  final Color color;
  final String neuronUri;
}

const defaultSmartAppSpecs = <SmartAppTileSpec>[
  SmartAppTileSpec(
    id: 'ino',
    name: 'Ino',
    icon: LucideIcons.sparkles,
    color: Color(0xFF4F8CFF),
    neuronUri: 'neuron://local/apps/ino',
  ),
  SmartAppTileSpec(
    id: 'gallery',
    name: 'UI Gallery',
    icon: LucideIcons.layoutTemplate,
    color: Color(0xFF8B5CF6),
    neuronUri: 'neuron://local/apps/gallery',
  ),
  SmartAppTileSpec(
    id: 'creator',
    name: 'Creator',
    icon: LucideIcons.brainCircuit,
    color: Color(0xFF22D3EE),
    neuronUri: 'neuron://local/apps/creator',
  ),
  SmartAppTileSpec(
    id: 'files',
    name: 'Files',
    icon: LucideIcons.folder,
    color: Color(0xFFF59E0B),
    neuronUri: 'neuron://local/apps/files',
  ),
  SmartAppTileSpec(
    id: 'post',
    name: 'Post',
    icon: LucideIcons.send,
    color: Color(0xFF34D399),
    neuronUri: 'neuron://local/apps/post',
  ),
  SmartAppTileSpec(
    id: 'settings',
    name: 'Settings',
    icon: LucideIcons.settings,
    color: Color(0xFF94A3B8),
    neuronUri: 'neuron://local/apps/settings',
  ),
];

/// Full-bleed interactive `BrainScene3D` orbital neural graph + glassmorphic
/// `IcAcrylicBox` Smart Cards (`Ino · DigitalBrain OS` launcher card, `Live
/// Smart App Signal` approval card, `UI Identity & Glass Theme` live switcher
/// card, and `Smart Apps & Workspace` grid with `neuron://` secondary-click copy).
class SmartDesktop extends StatefulWidget {
  const SmartDesktop({
    super.key,
    this.hasOpenWindows = false,
    this.stylePreset = IcStylePreset.fluentAcrylic,
    this.barLayout = BarLayout.floatingGlass,
    this.tokensToday = 14800,
    this.apps = defaultSmartAppSpecs,
    this.onLaunchApp,
    this.onOpenPalette,
    this.onApproveSignal,
    this.onSelectPreset,
    this.onCycleBarLayout,
    this.onToggleDark,
    this.onToast,
  });

  final bool hasOpenWindows;
  final IcStylePreset stylePreset;
  final BarLayout barLayout;
  final int tokensToday;
  final List<SmartAppTileSpec> apps;
  final ValueChanged<String>? onLaunchApp;
  final VoidCallback? onOpenPalette;
  final VoidCallback? onApproveSignal;
  final ValueChanged<IcStylePreset>? onSelectPreset;
  final VoidCallback? onCycleBarLayout;
  final VoidCallback? onToggleDark;
  final ValueChanged<String>? onToast;

  @override
  State<SmartDesktop> createState() => _SmartDesktopState();
}

class _SmartDesktopState extends State<SmartDesktop> {
  String _selectedNeuronId = 'ino';
  bool _briefApproved = false;

  void _launch(String appId) {
    widget.onLaunchApp?.call(appId);
  }

  void _toast(String msg) {
    if (widget.onToast != null) {
      widget.onToast!(msg);
    } else if (mounted) {
      showKitToast(context, msg);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final hasOpenWindows = widget.hasOpenWindows;

    return Stack(
      children: [
        // 1. Live Interactive 3D DigitalBrain Layer on the Desktop
        Positioned.fill(
          child: BrainScene3D(
            selectedId: _selectedNeuronId,
            ambientOpacity: hasOpenWindows ? 0.72 : 0.96,
            showHud: !hasOpenWindows,
            onSelectNode: (node) {
              setState(() => _selectedNeuronId = node.id);
              if (node.appId != null) {
                _launch(node.appId == 'chat' ? 'ino' : node.appId!);
              } else {
                _toast(
                  'Neuron ${node.neuronUri} (${node.signalsPerMin} sig/min)',
                );
              }
            },
          ),
        ),

        // 2. Smart Desktop Glass Cards Layer (stays interactive around floating windows)
        AnimatedOpacity(
          duration: IcMotion.base,
          opacity: hasOpenWindows ? 0.88 : 1.0,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(24, 24, 24, 56),
            child: LayoutBuilder(
                builder: (context, constraints) {
                  final compact = constraints.maxWidth < 860;
                  final leftCards = SizedBox(
                    width: compact ? constraints.maxWidth : 360,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _buildLauncherCard(c),
                        const SizedBox(height: 14),
                        _buildSignalApprovalCard(c),
                      ],
                    ),
                  );
                  final rightCards = SizedBox(
                    width: compact ? constraints.maxWidth : 340,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _buildIdentitySwitcherCard(c),
                        const SizedBox(height: 14),
                        _buildSmartAppGridCard(c),
                      ],
                    ),
                  );

                  if (compact) {
                    return SingleChildScrollView(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          leftCards,
                          const SizedBox(height: 14),
                          rightCards,
                        ],
                      ),
                    );
                  }

                  return Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [leftCards, const Spacer(), rightCards],
                  );
                },
              ),
            ),
          ),
        ],
      );
    }

  Widget _buildLauncherCard(IcColors c) {
    return IcAcrylicBox(
      radius: IcRadius.card,
      padding: const EdgeInsets.all(18),
      shadows: IcShadow.lg(c),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 30,
                height: 30,
                decoration: BoxDecoration(
                  color: c.accent,
                  borderRadius: BorderRadius.circular(IcRadius.sm),
                ),
                child: Icon(LucideIcons.sparkles, size: 16, color: c.onAccent),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Ino · DigitalBrain OS',
                      style: IcType.text(
                        14,
                        weight: FontWeight.w700,
                        color: c.textStrong,
                      ),
                    ),
                    Text(
                      'neuron://local/apps/ino',
                      style: IcType.text(11.5, color: c.textMuted, tnum: true),
                    ),
                  ],
                ),
              ),
              const TagPill(
                label: 'Online',
                tone: TagTone.done,
                icon: LucideIcons.circleCheck,
              ),
            ],
          ),
          const SizedBox(height: 14),
          GestureDetector(
            onTap: widget.onOpenPalette,
            child: MouseRegion(
              cursor: SystemMouseCursors.click,
              child: Container(
                height: 38,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                decoration: BoxDecoration(
                  color: c.surfaceSunk,
                  borderRadius: BorderRadius.circular(IcRadius.button),
                  border: Border.all(color: c.borderInput),
                ),
                child: Row(
                  children: [
                    Icon(LucideIcons.search, size: 15, color: c.textMuted),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        'Ask Ino or create a smart app…',
                        style: IcType.text(13, color: c.textMuted),
                      ),
                    ),
                    const TagPill(label: '/ or ⌘K', tone: TagTone.neutral),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              Btn(
                label: 'UI Gallery',
                small: true,
                primary: true,
                icon: LucideIcons.layoutTemplate,
                onTap: () => _launch('gallery'),
              ),
              Btn(
                label: '3D Creator',
                small: true,
                icon: LucideIcons.brainCircuit,
                onTap: () => _launch('creator'),
              ),
              Btn(
                label: 'Open Ino',
                small: true,
                icon: LucideIcons.messagesSquare,
                onTap: () => _launch('ino'),
              ),
              Btn(
                label: 'Files',
                small: true,
                icon: LucideIcons.folder,
                onTap: () => _launch('files'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildSignalApprovalCard(IcColors c) {
    return IcAcrylicBox(
      radius: IcRadius.card,
      padding: const EdgeInsets.all(18),
      shadows: IcShadow.md(c),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.radio, size: 15, color: c.accent),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  'LIVE SMART APP SIGNAL',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    11,
                    weight: FontWeight.w700,
                    color: c.textMuted,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              TagPill(
                label: _briefApproved ? 'Approved' : 'Needs you',
                tone: _briefApproved ? TagTone.done : TagTone.accent,
                icon: _briefApproved ? LucideIcons.check : LucideIcons.hand,
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            'Nordlys Q4 Renewal · Close date shift',
            style: IcType.text(
              14,
              weight: FontWeight.w600,
              color: c.textStrong,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Salesforce sync detected an email from Kari Holm confirming 15 Nov 2026 (480,000 NOK).',
            style: IcType.text(12.5, color: c.textBody, height: 1.4),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              Btn(
                label: _briefApproved ? 'Updated in CRM' : 'Approve & Sync',
                small: true,
                primary: !_briefApproved,
                icon: LucideIcons.check,
                onTap: _briefApproved
                    ? null
                    : () {
                        setState(() => _briefApproved = true);
                        widget.onApproveSignal?.call();
                        _toast(
                          'Approved signal on neuron://local/os.confirmations/inbox',
                        );
                      },
              ),
              Btn(
                label: 'Open in Ino',
                small: true,
                onTap: () => _launch('ino'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildIdentitySwitcherCard(IcColors c) {
    return IcAcrylicBox(
      radius: IcRadius.card,
      padding: const EdgeInsets.all(18),
      shadows: IcShadow.lg(c),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.palette, size: 15, color: c.accent),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  'UI IDENTITY & GLASS THEME',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    11,
                    weight: FontWeight.w700,
                    color: c.textMuted,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              GestureDetector(
                onTap: widget.onToggleDark,
                child: MouseRegion(
                  cursor: SystemMouseCursors.click,
                  child: TagPill(
                    label: c.isDark ? 'Dark' : 'Light',
                    tone: TagTone.accent,
                    icon: c.isDark ? LucideIcons.moon : LucideIcons.sun,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          for (final preset in IcStylePreset.values)
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: _ThemePresetRow(
                preset: preset,
                selected: widget.stylePreset == preset,
                onTap: () => widget.onSelectPreset?.call(preset),
              ),
            ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: Text(
                  'Bar layout',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(12, color: c.textMuted),
                ),
              ),
              const SizedBox(width: 8),
              GestureDetector(
                onTap: widget.onCycleBarLayout,
                child: MouseRegion(
                  cursor: SystemMouseCursors.click,
                  child: TagPill(
                    label: widget.barLayout.label,
                    tone: TagTone.neutral,
                    icon: LucideIcons.panelBottom,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildSmartAppGridCard(IcColors c) {
    return IcAcrylicBox(
      radius: IcRadius.card,
      padding: const EdgeInsets.all(16),
      shadows: IcShadow.md(c),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(LucideIcons.layoutGrid, size: 15, color: c.accent),
              const SizedBox(width: 7),
              Expanded(
                child: Text(
                  'SMART APPS & WORKSPACE',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    11,
                    weight: FontWeight.w700,
                    color: c.textMuted,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Text(
                '${(widget.tokensToday / 1000).toStringAsFixed(1)}k tok',
                style: IcType.text(
                  11.5,
                  weight: FontWeight.w600,
                  color: c.accent,
                  tnum: true,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: [
              for (final spec in widget.apps)
                _SmartAppTile(
                  spec: spec,
                  onTap: () => _launch(spec.id),
                  onCopyUri: () {
                    Clipboard.setData(ClipboardData(text: spec.neuronUri));
                    _toast('Copied ${spec.neuronUri}');
                  },
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ThemePresetRow extends StatefulWidget {
  const _ThemePresetRow({
    required this.preset,
    required this.selected,
    required this.onTap,
  });

  final IcStylePreset preset;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_ThemePresetRow> createState() => _ThemePresetRowState();
}

class _ThemePresetRowState extends State<_ThemePresetRow> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
          decoration: BoxDecoration(
            color: widget.selected
                ? c.accentTint
                : (_hover ? c.surfaceSunk : const Color(0x00000000)),
            borderRadius: BorderRadius.circular(IcRadius.sm),
            border: Border.all(
              color: widget.selected ? c.accent : const Color(0x00000000),
            ),
          ),
          child: Row(
            children: [
              Icon(
                widget.selected ? LucideIcons.circleCheck : LucideIcons.circle,
                size: 14,
                color: widget.selected ? c.accent : c.textMuted,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  widget.preset.label,
                  style: IcType.text(
                    13,
                    weight: widget.selected ? FontWeight.w600 : FontWeight.w500,
                    color: widget.selected ? c.textStrong : c.textBody,
                  ),
                ),
              ),
              Text(
                widget.preset.defaultBarLayout.label.split(' ').first,
                style: IcType.text(11, color: c.textMuted),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SmartAppTile extends StatefulWidget {
  const _SmartAppTile({
    required this.spec,
    required this.onTap,
    required this.onCopyUri,
  });

  final SmartAppTileSpec spec;
  final VoidCallback onTap;
  final VoidCallback onCopyUri;

  @override
  State<_SmartAppTile> createState() => _SmartAppTileState();
}

class _SmartAppTileState extends State<_SmartAppTile> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        onSecondaryTap: widget.onCopyUri,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          width: 94,
          padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 6),
          decoration: BoxDecoration(
            color: _hover ? c.accentTint : c.surfaceSunk,
            borderRadius: BorderRadius.circular(IcRadius.button),
            border: Border.all(color: _hover ? c.accent : c.borderHairline),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              AppTile(
                size: 28,
                style: AppTileStyle.brand,
                brandColor: widget.spec.color,
                glyph: widget.spec.icon,
              ),
              const SizedBox(height: 6),
              Text(
                widget.spec.name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(
                  12,
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
