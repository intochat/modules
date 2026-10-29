import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart' hide TableColumn;
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../components/kit/kit_toast.dart';
import '../components/kit/ui_kit_button.dart';
import '../design/brain_scene_3d.dart';
import '../design/cards/cards.dart';
import '../design/controls.dart';
import '../design/tokens.dart';
import '../models/ui_part.dart';
import '../theme/ui_theme.dart';
import 'ui_gallery_preview.dart';

/// Canonical alias matching `design/apps/os/lib/apps/gallery/gallery_app.dart`.
typedef GalleryApp = UiGalleryScreen;

class GallerySample {
  const GallerySample({
    required this.id,
    required this.title,
    required this.subtitle,
    required this.category,
    required this.icon,
    required this.contract,
    required this.signal,
    required this.csharpCode,
    required this.flutterCode,
  });

  final String id;
  final String title;
  final String subtitle;
  final String category;
  final IconData icon;
  final String contract;
  final String signal;
  final String csharpCode;
  final String flutterCode;
}

const gallerySamples = <GallerySample>[
  GallerySample(
    id: 'themes',
    title: 'Color & Acrylic Themes',
    subtitle:
        'Switch between Fluent Acrylic, Spatial Glass, Cyber Synaptic, Warm Paper & Linear Carbon.',
    category: 'Design',
    icon: LucideIcons.palette,
    contract: 'IntoChat.OS.Identity.IUserSettings',
    signal: 'UserSettingsChanged',
    csharpCode: '''[On("settings", Contract = typeof(IUserSettings))]
public void OnSettingsChanged(UserSettingsChanged? changed)
{
    if (changed is null) return;
    State = State with { Theme = changed.Theme };
}''',
    flutterCode: '''final colors = IcColors.forPreset(
  os.stylePreset,
  dark: isDark,
);
return IcTheme(colors: colors, child: const Desktop());''',
  ),
  GallerySample(
    id: 'typography',
    title: 'Typography & Tabular Figures',
    subtitle:
        'Inter and InterDisplay scale with strict tabular numerals (tnum) for live telemetry.',
    category: 'Design',
    icon: LucideIcons.type,
    contract: 'IntoChat.OS.Modules.Flutter.Text.IText',
    signal: 'TextChanged',
    csharpCode: '''new Column([
    new Heading("WinUI-Grade Typography", Level: 1),
    new Text("1,248,920 tokens · 99.98% uptime", Tone: TextTone.Muted, Tabular: true),
])''',
    flutterCode: '''Text(
  '1,248,920 tokens',
  style: IcType.heading(28, color: c.textStrong),
);
Text(
  'neuron://nordlys/apps.ino/main',
  style: IcType.text(13, color: c.textMuted, tnum: true),
);''',
  ),
  GallerySample(
    id: 'iconography',
    title: 'Iconography & Status Pairing',
    subtitle:
        'Lucide vector glyphs paired with semantic status tokens—never bare color dots.',
    category: 'Design',
    icon: LucideIcons.sparkles,
    contract: 'IntoChat.OS.Modules.Flutter.InfoBar.IInfoBar',
    signal: 'InfoBarChanged',
    csharpCode: '''new Row([
    new TagPill("Connected", BadgeTone.Success, Icon: "circle-check"),
    new TagPill("Syncing", BadgeTone.Warning, Icon: "loader-circle"),
    new TagPill("Needs approval", BadgeTone.Accent, Icon: "hand"),
])''',
    flutterCode: '''Row(
  children: [
    TagPill(label: 'Connected', tone: TagTone.done, icon: LucideIcons.circleCheck),
    TagPill(label: 'Working', tone: TagTone.working, icon: LucideIcons.loaderCircle),
    TagPill(label: 'Needs you', tone: TagTone.accent, icon: LucideIcons.hand),
  ],
)''',
  ),
  GallerySample(
    id: 'acrylic',
    title: 'AcrylicBrush & Glass Elevation',
    subtitle:
        'Translucent multi-layer surfaces with Gaussian blur and 1px specular borders.',
    category: 'Design',
    icon: LucideIcons.layers,
    contract: 'IntoChat.OS.Modules.Flutter.Surface.ISurface',
    signal: 'SurfaceChanged',
    csharpCode: '''new Surface(
    SurfaceKind.Acrylic,
    [new Text("Translucent Acrylic Panel")],
    Title: "Fluent Acrylic Surface"
)''',
    flutterCode: '''IcAcrylicBox(
  radius: IcRadius.card,
  padding: const EdgeInsets.all(20),
  shadows: IcShadow.lg(c),
  child: Text('Acrylic Surface', style: IcType.heading(18, color: c.textStrong)),
)''',
  ),
  GallerySample(
    id: 'buttons',
    title: 'Buttons & Action Controls',
    subtitle:
        'Primary, Secondary, Danger, and Small buttons with keyboard ring & spring press.',
    category: 'Basic input',
    icon: LucideIcons.mousePointerClick,
    contract: 'IntoChat.OS.Modules.Flutter.Button.IButton',
    signal: 'ButtonChanged',
    csharpCode: '''new Row([
    new Button("Approve & Deploy", new ActionCall("deploy"), Style: ButtonStyle.Primary, Icon: "rocket"),
    new Button("Inspect Logs", new ActionCall("inspect"), Style: ButtonStyle.Secondary),
    new Button("Rollback", new ActionCall("rollback"), Tone: ButtonTone.Danger, Style: ButtonStyle.Danger),
])''',
    flutterCode: '''Row(
  children: [
    Btn(label: 'Approve & Deploy', primary: true, icon: LucideIcons.rocket, onTap: () {}),
    const SizedBox(width: 8),
    Btn(label: 'Inspect Logs', onTap: () {}),
    const SizedBox(width: 8),
    Btn(label: 'Rollback', danger: true, onTap: () {}),
  ],
)''',
  ),
  GallerySample(
    id: 'inputs',
    title: 'Text Fields & Toggles',
    subtitle:
        'Desktop-first text inputs, search fields, and instant-save switches.',
    category: 'Basic input',
    icon: LucideIcons.textCursorInput,
    contract: 'IntoChat.OS.Modules.Flutter.TextField.ITextField',
    signal: 'TextFieldChanged',
    csharpCode: '''new Column([
    new TextField("display-name", Value: State.DisplayName, Placeholder: "Enter neuron:// address..."),
    new Switch("Auto-improve background subscriptions", State.AutoImprove, new ActionCall("toggle-auto")),
])''',
    flutterCode: '''IcSwitch(
  value: enabled,
  onChanged: (v) => setState(() => enabled = v),
)''',
  ),
  GallerySample(
    id: 'approval-card',
    title: 'ApprovalCard & ChoiceCard (Chat LFP)',
    subtitle:
        'Inline keyboard-first cards rendered directly inside Ino chat sessions.',
    category: 'Chat & Applets',
    icon: LucideIcons.checkCheck,
    contract: 'IntoChat.OS.Confirmation.IConfirmationInbox',
    signal: 'ConfirmationRequested',
    csharpCode: '''new ApprovalCardNode(
    Id: "card-1",
    Verb: "Update",
    Target: "Nordlys Q4 renewal close date -> 15 Nov",
    Rows: [new ApprovalRowNode("Nordlys Q4 Enterprise", "480,000 NOK · 15 Nov 2026")],
    SourceApp: "Salesforce",
    SourceNeuronUri: "neuron://nordlys/salesforce.crm/opp-402",
    ApproveAction: new ActionCall("approve-card", ["card-1"])
)''',
    flutterCode: '''ApprovalCard(
  source: CardSource('Salesforce', Icon(LucideIcons.cloud, size: 14)),
  data: ApprovalData(
    title: 'Update close date on Nordlys Q4 renewal to 15 Nov?',
    summary: 'Ino matched Kari\\'s email to your open Salesforce opportunity.',
    valueLabel: 'Close date',
    rows: [
      ApprovalRow(label: 'Nordlys Q4 Enterprise', amount: '480,000 NOK', oldValue: '30 Oct 2026', newValue: '15 Nov 2026'),
    ],
  ),
  state: ApprovalState.pending,
)''',
  ),
  GallerySample(
    id: 'data-cards',
    title: 'TableCard & ChartCard',
    subtitle:
        'Tabular data grids and comparative series charts with 3:1 contrast guarantees.',
    category: 'Collections',
    icon: LucideIcons.chartColumn,
    contract: 'IntoChat.OS.Modules.Flutter.Chart.IChart',
    signal: 'ChartChanged',
    csharpCode: '''new Chart(
    Kind: "bar",
    Title: "Active Neuron Signals per Minute",
    Points: [new ChartPointNode("ino", "Ino", 48), new ChartPointNode("aspire", "Aspire", 64)],
    SourceNeuronUri: "neuron://nordlys/microsoft.aspire/telemetry"
)''',
    flutterCode: '''ChartCard(
  source: CardSource('Aspire Telemetry', Icon(LucideIcons.activity, size: 14)),
  state: ChartState.ready,
  data: const ChartData(
    title: 'Compute Tokens by Module (7d)',
    from: 'neuron://nordlys/os.identity/compute',
    points: [
      ChartPoint('Mon', 42),
      ChartPoint('Tue', 58),
      ChartPoint('Wed', 51),
      ChartPoint('Thu', 74),
      ChartPoint('Fri', 89),
    ],
    compare: [35, 40, 44, 49, 52],
    seriesName: 'This week',
    compareName: 'Last week',
  ),
)''',
  ),
  GallerySample(
    id: 'brain-3d',
    title: '3D DigitalBrain & Synapse Scene',
    subtitle:
        'Interactive 3D orbital neural graph with live signal photon sparks and NeuronId HUD.',
    category: '3D Brain & Neurons',
    icon: LucideIcons.brainCircuit,
    contract: 'IntoChat.OS.Modules.Flame.Scene.IScene',
    signal: 'SceneChanged',
    csharpCode: '''new BrainSceneNode(
    Nodes: [new BrainGraphNode("ino", "Ino Assistant", "IntoChat.OS.Apps", "neuron://nordlys/apps.ino/main", "working")],
    Synapses: [new BrainGraphSynapse("ino", "memory")],
    SelectedId: State.SelectedNodeId,
    SelectAction: new ActionCall("inspect")
)''',
    flutterCode: '''BrainScene3D(
  selectedId: selectedNeuronId,
  onSelectNode: (node) => os.toast('Selected \${node.neuronUri}'),
)''',
  ),
  GallerySample(
    id: 'neuron-paths',
    title: 'Distributed NeuronId Addressing',
    subtitle:
        'Canonical neuron:// URIs across Files, UI controls, and cross-brain Circle grants.',
    category: '3D Brain & Neurons',
    icon: LucideIcons.network,
    contract: 'DigitalBrain.NeuronId',
    signal: 'SynapseRouted',
    csharpCode: '''// Every file, control, and app in DigitalBrain has a canonical URI:
// neuron://{brainId}/{module}.{neuron}/{key}
new NeuronRef("table", "neuron://nordlys/apps/ino/table", Title: "Opportunities Table")''',
    flutterCode: '''// Copy canonical Neuron ID or human virtual path:
copyNeuronId(os, selectedFileNodes);
// -> neuron://nordlys/storage.files/30-projects/nordlys-as/context.md''',
  ),
];

/// WinUI 3 Gallery-inspired interactive component, LFP card, and theme showcase.
final class UiGalleryScreen extends StatefulWidget {
  const UiGalleryScreen({
    super.key,
    this.onButtonPressed,
    this.initialPreset,
    this.initialBarLayout,
    this.onStylePresetChanged,
    this.onBarLayoutChanged,
    this.onDarkModeChanged,
  });

  final ValueChanged<UiButtonPart>? onButtonPressed;
  final IcStylePreset? initialPreset;
  final BarLayout? initialBarLayout;
  final ValueChanged<IcStylePreset>? onStylePresetChanged;
  final ValueChanged<BarLayout>? onBarLayoutChanged;
  final ValueChanged<bool>? onDarkModeChanged;

  @override
  State<UiGalleryScreen> createState() => _UiGalleryScreenState();
}

final class _UiGalleryScreenState extends State<UiGalleryScreen> {
  String _nav = 'Home';
  GallerySample? _activeSample;
  GalleryEntry? _activeEntry;
  bool _railCollapsed = false;

  late IcStylePreset _stylePreset =
      widget.initialPreset ?? IcStylePreset.fluentAcrylic;
  late BarLayout _barLayout =
      widget.initialBarLayout ?? BarLayout.floatingGlass;
  bool _isDark = true;

  final _search = TextEditingController();
  final _searchFocus = FocusNode();
  final _demoInput = TextEditingController(
    text: 'neuron://nordlys/apps.ino/main',
  );
  final _demoInputFocus = FocusNode();

  // Interactive sample knobs
  bool _knobEnabled = true;
  bool _knobSmall = false;
  bool _knobSwitch = true;
  TagTone _knobTone = TagTone.accent;
  UiKitButtonStyle _knobStyle = UiKitButtonStyle.primary;
  int _codeTab = 0; // 0 = C# App<TState>, 1 = Flutter UI Kit, 2 = gRPC Contract & [Signal]
  ApprovalState _approvalState = ApprovalState.pending;
  String _selectedBrainNode = 'ino';

  static const _categories = [
    ('Home', LucideIcons.house),
    ('Design', LucideIcons.palette),
    ('Basic input', LucideIcons.mousePointerClick),
    ('Collections', LucideIcons.table),
    ('Chat & Applets', LucideIcons.sparkles),
    ('3D Brain & Neurons', LucideIcons.brainCircuit),
  ];

  @override
  void dispose() {
    _search.dispose();
    _searchFocus.dispose();
    _demoInput.dispose();
    _demoInputFocus.dispose();
    super.dispose();
  }

  void _setPreset(IcStylePreset preset) {
    setState(() {
      _stylePreset = preset;
      _barLayout = preset.defaultBarLayout;
      if (preset == IcStylePreset.warmPaper) {
        _isDark = false;
      } else if (!_isDark && preset != IcStylePreset.warmPaper) {
        _isDark = true;
      }
    });
    widget.onStylePresetChanged?.call(preset);
  }

  void _setBarLayout(BarLayout layout) {
    setState(() => _barLayout = layout);
    widget.onBarLayoutChanged?.call(layout);
  }

  void _setDark(bool dark) {
    setState(() => _isDark = dark);
    widget.onDarkModeChanged?.call(dark);
  }

  void _toast(BuildContext context, String message) {
    showKitToast(context, message);
  }

  @override
  Widget build(BuildContext context) {
    final icColors = IcColors.forPreset(_stylePreset, dark: _isDark);
    final presetName = switch (_stylePreset) {
      IcStylePreset.fluentAcrylic => 'fluent-acrylic',
      IcStylePreset.spatialGlass => 'spatial-glass',
      IcStylePreset.cyberSynaptic => 'cyber-synaptic',
      IcStylePreset.warmPaper => 'warm-paper',
      IcStylePreset.linearCarbon => 'linear-carbon',
    };
    final themeData = _isDark
        ? UiTheme.dark(presetName)
        : UiTheme.light(presetName);

    return Theme(
      data: themeData,
      child: IcTheme(
        colors: icColors,
        child: Material(
          key: const Key('ui_gallery_screen'),
          color: icColors.surfacePage,
          child: LayoutBuilder(
            builder: (context, constraints) {
              final narrow = constraints.maxWidth < 720;
              if (narrow) {
                return (_activeSample != null || _activeEntry != null)
                    ? _buildDetailArea(context, icColors, narrow: true)
                    : _buildNavigationRail(context, icColors, fullWidth: true);
              }
              return Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (!_railCollapsed)
                    _buildNavigationRail(context, icColors)
                  else
                    Container(
                      width: 48,
                      decoration: BoxDecoration(
                        color: icColors.surfaceSunk,
                        border: Border(
                          right: BorderSide(color: icColors.borderHairline),
                        ),
                      ),
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Column(
                        children: [
                          IconButton(
                            tooltip: 'Expand navigation rail',
                            icon: Icon(
                              LucideIcons.panelLeftOpen,
                              size: 16,
                              color: icColors.textStrong,
                            ),
                            onPressed: () =>
                                setState(() => _railCollapsed = false),
                          ),
                          const SizedBox(height: 8),
                          for (final (cat, icon) in _categories)
                            IconButton(
                              tooltip: cat,
                              icon: Icon(
                                icon,
                                size: 16,
                                color:
                                    _nav == cat &&
                                        _activeSample == null &&
                                        _activeEntry == null
                                    ? icColors.accent
                                    : icColors.textMuted,
                              ),
                              onPressed: () => setState(() {
                                _nav = cat;
                                _activeSample = null;
                                _activeEntry = null;
                              }),
                            ),
                        ],
                      ),
                    ),
                  Expanded(
                    child: (_activeSample != null || _activeEntry != null)
                        ? _buildDetailArea(context, icColors, narrow: false)
                        : _buildHomeOrCategory(context, icColors),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  Widget _buildNavigationRail(
    BuildContext context,
    IcColors c, {
    bool fullWidth = false,
  }) {
    final q = _search.text.trim().toLowerCase();
    final matchingSamples = gallerySamples.where((s) {
      if (q.isEmpty) return true;
      return s.title.toLowerCase().contains(q) ||
          s.subtitle.toLowerCase().contains(q) ||
          s.contract.toLowerCase().contains(q);
    }).toList();

    final matchingEntries = galleryEntries.where((e) {
      if (_nav != 'Home' &&
          !_categories.any((cat) => cat.$1 == _nav) &&
          e.category != _nav) {
        return false;
      }
      if (q.isEmpty) {
        if (_nav != 'Home' && !_categories.any((cat) => cat.$1 == _nav)) {
          return e.category == _nav;
        }
        return true;
      }
      return '${e.title} ${e.api} ${e.description} ${e.category}'
          .toLowerCase()
          .contains(q);
    }).toList();

    final categoryOptions = <String>[
      'Home',
      for (final (cat, _) in _categories)
        if (cat != 'Home') cat,
      ...galleryEntries.map((e) => e.category).toSet(),
    ];

    return Container(
      width: fullWidth ? double.infinity : 244,
      decoration: BoxDecoration(
        color: c.surfaceSunk,
        border: fullWidth
            ? null
            : Border(right: BorderSide(color: c.borderHairline)),
      ),
      padding: const EdgeInsets.fromLTRB(12, 14, 12, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Top rail header + collapse button
          Row(
            children: [
              Icon(LucideIcons.layoutGrid, size: 15, color: c.accent),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  'INTOCHAT UI KIT',
                  style: IcType.text(
                    11,
                    weight: FontWeight.w700,
                    color: c.textMuted,
                  ),
                ),
              ),
              if (!fullWidth)
                GestureDetector(
                  onTap: () => setState(() => _railCollapsed = true),
                  child: MouseRegion(
                    cursor: SystemMouseCursors.click,
                    child: Icon(
                      LucideIcons.panelLeftClose,
                      size: 15,
                      color: c.textMuted,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 10),
          // Search box
          SizedBox(
            height: 36,
            child: TextField(
              key: const Key('gallery_search'),
              controller: _search,
              focusNode: _searchFocus,
              style: IcType.text(13, color: c.textStrong),
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                hintText: 'Search controls & samples…',
                hintStyle: IcType.text(12.5, color: c.textMuted),
                prefixIcon: Icon(
                  LucideIcons.search,
                  size: 15,
                  color: c.textMuted,
                ),
                isDense: true,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 8,
                ),
                suffixIcon: q.isEmpty
                    ? null
                    : IconButton(
                        tooltip: 'Clear search',
                        icon: Icon(LucideIcons.x, size: 14, color: c.textMuted),
                        onPressed: () => setState(_search.clear),
                      ),
              ),
            ),
          ),
          const SizedBox(height: 8),
          // Category filter dropdown (supports both WinUI 3 categories & neuron catalog categories)
          DropdownButtonFormField<String>(
            key: const Key('gallery_category'),
            initialValue: categoryOptions.contains(_nav) ? _nav : 'Home',
            isExpanded: true,
            dropdownColor: c.surfaceCard,
            style: IcType.text(12.5, color: c.textStrong),
            decoration: const InputDecoration(
              isDense: true,
              contentPadding: EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            ),
            items: [
              for (final cat in categoryOptions)
                DropdownMenuItem(
                  value: cat,
                  child: Text(
                    cat,
                    style: IcType.text(12.5, color: c.textStrong),
                  ),
                ),
            ],
            onChanged: (value) {
              if (value == null) return;
              setState(() {
                _nav = value;
                _activeSample = null;
                _activeEntry = null;
              });
            },
          ),
          const SizedBox(height: 10),
          for (final (cat, icon) in _categories)
            _NavRow(
              label: cat,
              icon: icon,
              selected:
                  _nav == cat && _activeSample == null && _activeEntry == null,
              onTap: () => setState(() {
                _nav = cat;
                _activeSample = null;
                _activeEntry = null;
              }),
            ),
          const SizedBox(height: 10),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
            child: Text(
              'SAMPLES & NEURONS',
              style: IcType.text(
                11,
                weight: FontWeight.w700,
                color: c.textMuted,
              ),
            ),
          ),
          const SizedBox(height: 4),
          Expanded(
            child: (matchingSamples.isEmpty && matchingEntries.isEmpty)
                ? Center(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            LucideIcons.searchX,
                            size: 24,
                            color: c.textMuted,
                          ),
                          const SizedBox(height: 8),
                          Text(
                            'No components found',
                            style: IcType.text(
                              13,
                              weight: FontWeight.w600,
                              color: c.textStrong,
                            ),
                          ),
                        ],
                      ),
                    ),
                  )
                : ListView(
                    children: [
                      if (_categories.any((cat) => cat.$1 == _nav))
                        for (final s in matchingSamples)
                          _NavRow(
                            key: Key('gallery_sample_${s.id}'),
                            label: s.title,
                            icon: s.icon,
                            selected: _activeSample?.id == s.id,
                            compact: true,
                            onTap: () => setState(() {
                              _activeSample = s;
                              _activeEntry = null;
                            }),
                          ),
                      for (final entry in matchingEntries)
                        _NavRow(
                          key: Key('gallery_entry_${entry.id}'),
                          label: entry.title,
                          icon: entry.icon,
                          selected: _activeEntry?.id == entry.id,
                          compact: true,
                          onTap: () => setState(() {
                            _activeEntry = entry;
                            _activeSample = null;
                          }),
                        ),
                    ],
                  ),
          ),
          const SizedBox(height: 8),
          // Active theme badge at bottom of rail
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: c.surfaceCard,
              borderRadius: BorderRadius.circular(IcRadius.button),
              border: Border.all(color: c.borderHairline),
            ),
            child: Row(
              children: [
                Icon(LucideIcons.sparkles, size: 15, color: c.accent),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    _stylePreset.label,
                    style: IcType.text(
                      12.5,
                      weight: FontWeight.w600,
                      color: c.textStrong,
                    ),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                TagPill(
                  label: c.isDark ? 'Dark' : 'Light',
                  tone: TagTone.accent,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHomeOrCategory(BuildContext context, IcColors c) {
    final q = _search.text.trim().toLowerCase();
    final filteredSamples = gallerySamples.where((s) {
      if (q.isNotEmpty) {
        return s.title.toLowerCase().contains(q) ||
            s.subtitle.toLowerCase().contains(q) ||
            s.contract.toLowerCase().contains(q);
      }
      if (_nav == 'Home') return true;
      return s.category == _nav;
    }).toList();

    final filteredEntries = galleryEntries.where((e) {
      if (q.isNotEmpty) {
        return '${e.title} ${e.api} ${e.description} ${e.category}'
            .toLowerCase()
            .contains(q);
      }
      if (_nav == 'Home') return true;
      return e.category == _nav;
    }).toList();

    return ListView(
      padding: const EdgeInsets.fromLTRB(32, 26, 32, 40),
      children: [
        if (_nav == 'Home' && q.isEmpty) ...[
          // WinUI 3 Gallery Hero Banner
          Container(
            constraints: const BoxConstraints(minHeight: 216),
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(IcRadius.card),
              border: Border.all(
                color: c.isGlass ? c.specularBorder : c.borderHairline,
              ),
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  c.accent.withValues(alpha: c.isDark ? 0.28 : 0.16),
                  c.secondaryGlow.withValues(alpha: c.isDark ? 0.16 : 0.08),
                  c.surfaceCard,
                ],
              ),
            ),
            child: Stack(
              children: [
                const Positioned(
                  right: -40,
                  top: -20,
                  bottom: -20,
                  width: 420,
                  child: BrainScene3D(
                    interactive: false,
                    showHud: false,
                    ambientOpacity: 0.85,
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const TagPill(
                        label:
                            'DigitalBrain UI Kit · gRPC Multiplexed · Flutter LFP',
                        tone: TagTone.accent,
                        icon: LucideIcons.layers,
                      ),
                      const SizedBox(height: 10),
                      Text(
                        'IntoChat UI Gallery',
                        style: IcType.heading(
                          30,
                          weight: FontWeight.w800,
                          color: c.textStrong,
                        ),
                      ),
                      const SizedBox(height: 6),
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 480),
                        child: Text(
                          'WinUI 3-inspired controls, live inline chat applets, switchable Acrylic & Glassmorphic identities, and 3D neural telemetry.',
                          style: IcType.text(
                            14,
                            color: c.textBody,
                            height: 1.45,
                          ),
                        ),
                      ),
                      const SizedBox(height: 18),
                      SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        child: Row(
                          children: [
                            for (final preset in IcStylePreset.values)
                              Padding(
                                padding: const EdgeInsets.only(right: 8),
                                child: Btn(
                                  label: preset.label,
                                  small: true,
                                  primary: _stylePreset == preset,
                                  icon: _stylePreset == preset
                                      ? LucideIcons.check
                                      : LucideIcons.sparkles,
                                  onTap: () => _setPreset(preset),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),
          // Top WinUI 3 Quick Action Tiles
          Row(
            children: [
              Expanded(
                child: _HeroActionTile(
                  icon: LucideIcons.palette,
                  title: '5 Live UI Identities',
                  subtitle: 'Switch colors, Acrylic blur & bar layout on the fly.',
                  onTap: () => setState(() => _activeSample = gallerySamples[0]),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _HeroActionTile(
                  icon: LucideIcons.brainCircuit,
                  title: '3D Brain Visualizer',
                  subtitle: 'Inspect live neurons, synapses & signal pulses.',
                  onTap: () => setState(() => _activeSample = gallerySamples[8]),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _HeroActionTile(
                  icon: LucideIcons.messagesSquare,
                  title: 'Chat Applet Runtime',
                  subtitle: 'Render interactive C# & Flutter LFP cards in chat.',
                  onTap: () => setState(() => _activeSample = gallerySamples[6]),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _HeroActionTile(
                  icon: LucideIcons.network,
                  title: 'NeuronId Addressing',
                  subtitle: 'Canonical neuron:// URIs across Files & OS.',
                  onTap: () => setState(() => _activeSample = gallerySamples[9]),
                ),
              ),
            ],
          ),
          const SizedBox(height: 28),
        ],
        Wrap(
          spacing: 10,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          alignment: WrapAlignment.spaceBetween,
          children: [
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Flexible(
                  child: Text(
                    _nav == 'Home' ? 'All controls & samples' : _nav,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: IcType.heading(20, color: c.textStrong),
                  ),
                ),
                const SizedBox(width: 10),
                TagPill(
                  label:
                      '${filteredSamples.length + filteredEntries.length} items',
                  tone: TagTone.neutral,
                ),
              ],
            ),
            Btn(
              label: c.isDark ? 'Switch to Light' : 'Switch to Dark',
              small: true,
              icon: c.isDark ? LucideIcons.sun : LucideIcons.moon,
              onTap: () => _setDark(!c.isDark),
            ),
          ],
        ),
        const SizedBox(height: 14),
        if (filteredSamples.isEmpty && filteredEntries.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 48),
            child: Center(
              child: Text(
                'No components found',
                style: IcType.text(14, color: c.textMuted),
              ),
            ),
          )
        else
          Wrap(
            spacing: 14,
            runSpacing: 14,
            children: [
              for (final s in filteredSamples)
                SizedBox(
                  width: 310,
                  child: _SampleCard(
                    sample: s,
                    onTap: () => setState(() {
                      _activeSample = s;
                      _activeEntry = null;
                    }),
                  ),
                ),
              for (final entry in filteredEntries)
                SizedBox(
                  width: 310,
                  child: _EntryCard(
                    entry: entry,
                    onTap: () => setState(() {
                      _activeEntry = entry;
                      _activeSample = null;
                    }),
                  ),
                ),
            ],
          ),
      ],
    );
  }

  Widget _buildDetailArea(
    BuildContext context,
    IcColors c, {
    required bool narrow,
  }) {
    if (_activeEntry != null) {
      return _buildEntryDetail(context, c, _activeEntry!, narrow: narrow);
    }
    return _buildSampleDetail(context, c, _activeSample!, narrow: narrow);
  }

  Widget _buildEntryDetail(
    BuildContext context,
    IcColors c,
    GalleryEntry entry, {
    required bool narrow,
  }) {
    final isNeuron = entry.id.startsWith('neuron:');
    final kind = isNeuron ? entry.id.substring(7) : entry.id;
    final contract = isNeuron ? NodeContracts.tryResolve(kind) : null;
    final neuronUri = 'neuron://nordlys/flutter.$kind/sample-1';

    final code = switch (_codeTab) {
      0 => '''[On]
public View Render() => new(
    "${entry.title}",
    new NeuronRef("$kind", "$neuronUri", Title: "${entry.title}")
);''',
      1 => entry.api,
      _ =>
        'Contract: ${contract?.contract ?? 'IntoChat.OS.Modules.Flutter.IView'}\nSignal:   ${contract?.changed ?? 'ViewChanged'}\nURI:      $neuronUri',
    };

    return ListView(
      key: ValueKey('gallery_detail_${entry.id}'),
      padding: EdgeInsets.fromLTRB(
        narrow ? 20 : 32,
        24,
        narrow ? 20 : 32,
        40,
      ),
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Btn(
              key: const Key('gallery_back'),
              label: 'Back to Gallery',
              small: true,
              icon: LucideIcons.arrowLeft,
              onTap: () => setState(() => _activeEntry = null),
            ),
            TagPill(label: entry.category, tone: TagTone.neutral),
            if (contract != null)
              TagPill(
                label: contract.contract,
                tone: TagTone.accent,
                icon: LucideIcons.cpu,
              ),
          ],
        ),
        const SizedBox(height: 16),
        Text(
          entry.title,
          style: IcType.heading(28, weight: FontWeight.w800, color: c.textStrong),
        ),
        const SizedBox(height: 6),
        Text(entry.description, style: IcType.text(14, color: c.textMuted)),
        const SizedBox(height: 8),
        Text(
          isNeuron
              ? (entry.category == 'Composable neurons'
                    ? 'Composable neuron · dedicated renderer'
                    : 'Neuron kind · fallback renderer')
              : 'Presentation widget · not a neuron definition',
          style: IcType.text(12, weight: FontWeight.w600, color: c.accent),
        ),
        const SizedBox(height: 20),
        GalleryPreview(
          key: ValueKey(entry.id),
          entry: entry,
          onButtonPressed: widget.onButtonPressed,
        ),
        const SizedBox(height: 20),
        _buildCodeInspectorBox(context, c, code),
      ],
    );
  }

  Widget _buildSampleDetail(
    BuildContext context,
    IcColors c,
    GallerySample s, {
    required bool narrow,
  }) {
    final code = switch (_codeTab) {
      0 => s.csharpCode,
      1 => s.flutterCode,
      _ =>
        'Contract: ${s.contract}\nSignal:   ${s.signal}\nStream:   NeuronChannel.watch(brainId, [WatchSourceSpec("${s.contract}", key, ["${s.signal}"])])',
    };

    return ListView(
      padding: EdgeInsets.fromLTRB(
        narrow ? 20 : 32,
        24,
        narrow ? 20 : 32,
        40,
      ),
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Btn(
              key: const Key('gallery_back'),
              label: 'Back to Gallery',
              small: true,
              icon: LucideIcons.arrowLeft,
              onTap: () => setState(() => _activeSample = null),
            ),
            TagPill(label: s.category, tone: TagTone.neutral),
            TagPill(
              label: s.contract,
              tone: TagTone.accent,
              icon: LucideIcons.cpu,
            ),
            TagPill(
              label: '[Signal] ${s.signal}',
              tone: TagTone.done,
              icon: LucideIcons.zap,
            ),
          ],
        ),
        const SizedBox(height: 16),
        Text(
          s.title,
          style: IcType.heading(
            28,
            weight: FontWeight.w800,
            color: c.textStrong,
          ),
        ),
        const SizedBox(height: 6),
        Text(s.subtitle, style: IcType.text(14, color: c.textMuted)),
        const SizedBox(height: 20),

        // Interactive Playground + Right Property Knobs (WinUI 3 Pattern)
        Container(
          decoration: BoxDecoration(
            color: c.surfaceCard,
            borderRadius: BorderRadius.circular(IcRadius.card),
            border: Border.all(color: c.borderHairline),
          ),
          child: narrow
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(20),
                      child: _buildLivePreview(context, c, s),
                    ),
                    _buildPropertyKnobsPanel(context, c),
                  ],
                )
              : IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Expanded(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: _buildLivePreview(context, c, s),
                        ),
                      ),
                      _buildPropertyKnobsPanel(context, c),
                    ],
                  ),
                ),
        ),
        const SizedBox(height: 20),
        _buildCodeInspectorBox(context, c, code),
      ],
    );
  }

  Widget _buildPropertyKnobsPanel(BuildContext context, IcColors c) {
    return Container(
      width: 248,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: c.surfaceSunk,
        border: Border(left: BorderSide(color: c.borderHairline)),
        borderRadius: const BorderRadius.horizontal(
          right: Radius.circular(IcRadius.card),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'PROPERTIES & KNOBS',
            style: IcType.text(
              11,
              weight: FontWeight.w700,
              color: c.textMuted,
            ),
          ),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Enabled', style: IcType.text(13, color: c.textBody)),
              IcSwitch(
                value: _knobEnabled,
                onChanged: (v) => setState(() => _knobEnabled = v),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Small / Compact', style: IcType.text(13, color: c.textBody)),
              IcSwitch(
                value: _knobSmall,
                onChanged: (v) => setState(() => _knobSmall = v),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Dark mode', style: IcType.text(13, color: c.textBody)),
              IcSwitch(value: c.isDark, onChanged: _setDark),
            ],
          ),
          const SizedBox(height: 14),
          Text(
            'TONE & STYLE',
            style: IcType.text(
              11,
              weight: FontWeight.w700,
              color: c.textMuted,
            ),
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 4,
            runSpacing: 4,
            children: [
              for (final tone in [
                TagTone.accent,
                TagTone.done,
                TagTone.working,
                TagTone.error,
              ])
                Btn(
                  label: tone.name,
                  small: true,
                  primary: _knobTone == tone,
                  onTap: () => setState(() => _knobTone = tone),
                ),
            ],
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 4,
            runSpacing: 4,
            children: [
              for (final style in [
                UiKitButtonStyle.primary,
                UiKitButtonStyle.secondary,
                UiKitButtonStyle.ghost,
                UiKitButtonStyle.danger,
              ])
                Btn(
                  label: style.name,
                  small: true,
                  primary: _knobStyle == style,
                  onTap: () => setState(() => _knobStyle = style),
                ),
            ],
          ),
          const SizedBox(height: 14),
          Text(
            'APPROVAL STATE',
            style: IcType.text(
              11,
              weight: FontWeight.w700,
              color: c.textMuted,
            ),
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 4,
            runSpacing: 4,
            children: [
              for (final st in [
                ApprovalState.pending,
                ApprovalState.approved,
                ApprovalState.rejected,
              ])
                Btn(
                  label: st.name,
                  small: true,
                  primary: _approvalState == st,
                  onTap: () => setState(() => _approvalState = st),
                ),
            ],
          ),
          const SizedBox(height: 14),
          Text(
            'IDENTITY PRESET',
            style: IcType.text(
              11,
              weight: FontWeight.w700,
              color: c.textMuted,
            ),
          ),
          const SizedBox(height: 8),
          for (final p in IcStylePreset.values)
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Btn(
                label: p.label,
                small: true,
                primary: _stylePreset == p,
                onTap: () => _setPreset(p),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildCodeInspectorBox(
    BuildContext context,
    IcColors c,
    String code,
  ) {
    return Container(
      decoration: BoxDecoration(
        color: c.surfaceCard,
        borderRadius: BorderRadius.circular(IcRadius.card),
        border: Border.all(color: c.borderHairline),
      ),
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Wrap(
            spacing: 8,
            runSpacing: 8,
            alignment: WrapAlignment.spaceBetween,
            children: [
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  Btn(
                    label: 'C# App<TState>',
                    small: true,
                    primary: _codeTab == 0,
                    icon: LucideIcons.code,
                    onTap: () => setState(() => _codeTab = 0),
                  ),
                  Btn(
                    label: 'Flutter UI Kit',
                    small: true,
                    primary: _codeTab == 1,
                    icon: LucideIcons.layers,
                    onTap: () => setState(() => _codeTab = 1),
                  ),
                  Btn(
                    label: 'gRPC Contract & [Signal]',
                    small: true,
                    primary: _codeTab == 2,
                    icon: LucideIcons.radio,
                    onTap: () => setState(() => _codeTab = 2),
                  ),
                ],
              ),
              Btn(
                label: 'Copy snippet',
                small: true,
                icon: LucideIcons.copy,
                onTap: () {
                  Clipboard.setData(ClipboardData(text: code));
                  _toast(context, 'Copied snippet to clipboard.');
                },
              ),
            ],
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: c.surfaceSunk,
              borderRadius: BorderRadius.circular(IcRadius.button),
              border: Border.all(color: c.borderHairline),
            ),
            child: SelectableText(
              code,
              style: IcType.text(
                13,
                color: c.textStrong,
                tnum: true,
                height: 1.5,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildLivePreview(
    BuildContext context,
    IcColors c,
    GallerySample s,
  ) {
    switch (s.id) {
      case 'themes':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Live Identity & Bar Layout Presets',
              style: IcType.heading(18, color: c.textStrong),
            ),
            const SizedBox(height: 6),
            Text(
              'Active: ${_stylePreset.label} · Blur sigma: ${c.blurSigma.toStringAsFixed(0)}px · Bar layout: ${_barLayout.label}',
              style: IcType.text(13, color: c.textMuted, tnum: true),
            ),
            const SizedBox(height: 16),
            Wrap(
              spacing: 10,
              runSpacing: 10,
              children: [
                for (final p in IcStylePreset.values)
                  Btn(
                    label: '${p.label} (${p.defaultBarLayout.label})',
                    primary: _stylePreset == p,
                    small: _knobSmall,
                    icon: LucideIcons.sparkles,
                    onTap: _knobEnabled ? () => _setPreset(p) : null,
                  ),
              ],
            ),
            const SizedBox(height: 16),
            Text(
              'Override Bar & Dock Positioning:',
              style: IcType.text(
                12.5,
                weight: FontWeight.w600,
                color: c.textMuted,
              ),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final layout in BarLayout.values)
                  Btn(
                    label: layout.label,
                    small: true,
                    primary: _barLayout == layout,
                    onTap: _knobEnabled ? () => _setBarLayout(layout) : null,
                  ),
              ],
            ),
          ],
        );

      case 'typography':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Display 40 · 1,492,048 tokens',
              style: IcType.heading(
                36,
                weight: FontWeight.w800,
                color: c.textStrong,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Page Title 28 · Nordlys AS DigitalBrain',
              style: IcType.heading(24, color: c.textStrong),
            ),
            const SizedBox(height: 8),
            Text(
              'Card Title 16 · Q4 Renewal Opportunity',
              style: IcType.text(
                16,
                weight: FontWeight.w600,
                color: c.textStrong,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              'Body 14 · Every number in IntoChat OS uses FontFeature.tabularFigures() so live gRPC streams never jitter.',
              style: IcType.text(14, color: c.textBody),
            ),
            const SizedBox(height: 6),
            Text(
              'Tabular telemetry · 00:42.198 · neuron://nordlys/apps.ino/main · 99.98%',
              style: IcType.text(12.5, color: c.textMuted, tnum: true),
            ),
          ],
        );

      case 'iconography':
        return Wrap(
          spacing: 10,
          runSpacing: 10,
          children: [
            TagPill(
              label: 'Selected Tone (${_knobTone.name})',
              tone: _knobTone,
              icon: LucideIcons.sparkles,
            ),
            const TagPill(
              label: 'Done / Connected',
              tone: TagTone.done,
              icon: LucideIcons.circleCheck,
            ),
            const TagPill(
              label: 'Working / Syncing',
              tone: TagTone.working,
              icon: LucideIcons.loaderCircle,
            ),
            const TagPill(
              label: 'Needs your approval',
              tone: TagTone.accent,
              icon: LucideIcons.hand,
            ),
            const TagPill(
              label: 'Failed / Alert',
              tone: TagTone.error,
              icon: LucideIcons.triangleAlert,
            ),
            const TagPill(
              label: 'Idle / Standby',
              tone: TagTone.neutral,
              icon: LucideIcons.clock,
            ),
          ],
        );

      case 'acrylic':
        return SizedBox(
          height: 160,
          child: Stack(
            children: [
              const Positioned.fill(
                child: BrainScene3D(interactive: false, showHud: false),
              ),
              Center(
                child: IcAcrylicBox(
                  radius: IcRadius.card,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 24,
                    vertical: 18,
                  ),
                  shadows: IcShadow.lg(c),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        'WinUI 3 Acrylic & Glass Surface',
                        style: IcType.heading(18, color: c.textStrong),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Real-time Gaussian blur (sigma ${c.blurSigma.toStringAsFixed(0)}) over the live 3D DigitalBrain.',
                        style: IcType.text(13, color: c.textBody),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        );

      case 'buttons':
        return Wrap(
          spacing: 10,
          runSpacing: 10,
          children: [
            UiKitButton(
              label: 'Configured (${_knobStyle.name})',
              style: _knobStyle,
              compact: _knobSmall,
              icon: 'sparkles',
              onPressed: _knobEnabled
                  ? () => _toast(context, 'Configured button triggered.')
                  : null,
            ),
            Btn(
              label: 'Primary Action',
              primary: true,
              small: _knobSmall,
              icon: LucideIcons.rocket,
              onTap: _knobEnabled
                  ? () => _toast(context, 'Primary action triggered.')
                  : null,
            ),
            Btn(
              label: 'Secondary Action',
              small: _knobSmall,
              icon: LucideIcons.slidersHorizontal,
              onTap: _knobEnabled
                  ? () => _toast(context, 'Secondary action triggered.')
                  : null,
            ),
            Btn(
              label: 'Destructive Action',
              danger: true,
              small: _knobSmall,
              icon: LucideIcons.trash2,
              onTap: _knobEnabled
                  ? () => _toast(context, 'Destructive action triggered.')
                  : null,
            ),
          ],
        );

      case 'inputs':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
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
                controller: _demoInput,
                focusNode: _demoInputFocus,
                placeholder: 'Enter neuron:// address…',
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                IcSwitch(
                  value: _knobSwitch,
                  onChanged: _knobEnabled
                      ? (v) => setState(() => _knobSwitch = v)
                      : null,
                ),
                const SizedBox(width: 10),
                Text(
                  _knobSwitch
                      ? 'Live background [Signal] subscription enabled'
                      : 'Subscription paused',
                  style: IcType.text(13.5, color: c.textBody),
                ),
              ],
            ),
          ],
        );

      case 'approval-card':
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ApprovalCard(
              source: CardSource(
                'Salesforce · neuron://nordlys/salesforce.crm/opp-402',
                Icon(LucideIcons.cloud, size: 14, color: c.accent),
              ),
              data: ApprovalData(
                title:
                    'Update close date on Nordlys Q4 renewal to 15 Nov 2026?',
                summary:
                    'Ino matched Kari Holm\'s email to your open Salesforce opportunity.',
                valueLabel: 'Close date',
                resultText: 'Close date updated in Salesforce',
                rows: [
                  ApprovalRow(
                    label: 'Nordlys Q4 Enterprise Renewal',
                    amount: '480,000 NOK',
                    oldValue: '30 Oct 2026',
                    newValue: '15 Nov 2026',
                  ),
                ],
              ),
              state: _approvalState,
              onApprove: () =>
                  setState(() => _approvalState = ApprovalState.approved),
              onReject: () =>
                  setState(() => _approvalState = ApprovalState.rejected),
            ),
            if (_approvalState != ApprovalState.pending) ...[
              const SizedBox(height: 10),
              Btn(
                label: 'Reset Card State',
                small: true,
                icon: LucideIcons.rotateCcw,
                onTap: () =>
                    setState(() => _approvalState = ApprovalState.pending),
              ),
            ],
          ],
        );

      case 'data-cards':
        return ChartCard(
          source: CardSource(
            'Aspire · neuron://nordlys/microsoft.aspire/telemetry',
            Icon(LucideIcons.activity, size: 14, color: c.accent),
          ),
          state: ChartState.ready,
          data: const ChartData(
            title: 'Active Neuron Signals per Minute',
            from: 'Aspire OTLP collector · 27 gRPC neuron contracts',
            points: [
              ChartPoint('Ino', 48),
              ChartPoint('Aspire', 64),
              ChartPoint('CRM', 31),
              ChartPoint('Gmail', 27),
              ChartPoint('Memory', 24),
            ],
            compare: [36, 52, 28, 22, 18],
            seriesName: 'Current (sig/min)',
            compareName: '1h avg',
          ),
        );

      case 'brain-3d':
        return SizedBox(
          height: 300,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(IcRadius.button),
            child: BrainScene3D(
              selectedId: _selectedBrainNode,
              onSelectNode: (node) {
                setState(() => _selectedBrainNode = node.id);
                _toast(context, 'Inspecting ${node.neuronUri}');
              },
            ),
          ),
        );

      case 'neuron-paths':
        const samplePaths = [
          (
            'context.md',
            'neuron://nordlys/storage.files/30-projects/nordlys-as/context.md',
          ),
          (
            'opportunities.table',
            'neuron://nordlys/apps/ino/table',
          ),
          (
            'revenue-q4.chart',
            'neuron://nordlys/apps/datavisualizer/views/main/chart',
          ),
        ];
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Live Nodes with Canonical NeuronId Addressing',
              style: IcType.text(
                14,
                weight: FontWeight.w600,
                color: c.textStrong,
              ),
            ),
            const SizedBox(height: 10),
            for (final (name, uri) in samplePaths)
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 10,
                  ),
                  decoration: BoxDecoration(
                    color: c.surfaceSunk,
                    borderRadius: BorderRadius.circular(IcRadius.button),
                    border: Border.all(color: c.borderHairline),
                  ),
                  child: Row(
                    children: [
                      Icon(LucideIcons.fileCode, size: 16, color: c.accent),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              name,
                              style: IcType.text(
                                13,
                                weight: FontWeight.w600,
                                color: c.textStrong,
                              ),
                            ),
                            Text(
                              uri,
                              style: IcType.text(
                                12,
                                color: c.textMuted,
                                tnum: true,
                              ),
                            ),
                          ],
                        ),
                      ),
                      Btn(
                        label: 'Copy Neuron ID',
                        small: true,
                        icon: LucideIcons.cpu,
                        onTap: () {
                          Clipboard.setData(ClipboardData(text: uri));
                          _toast(context, 'Copied $uri');
                        },
                      ),
                    ],
                  ),
                ),
              ),
          ],
        );

      default:
        return const SizedBox.shrink();
    }
  }
}

class _NavRow extends StatefulWidget {
  const _NavRow({
    super.key,
    required this.label,
    required this.icon,
    required this.selected,
    required this.onTap,
    this.compact = false,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;
  final bool compact;

  @override
  State<_NavRow> createState() => _NavRowState();
}

class _NavRowState extends State<_NavRow> {
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
        child: Container(
          height: widget.compact ? 30 : 34,
          margin: const EdgeInsets.only(bottom: 2),
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: widget.selected
                ? c.accentTint
                : (_hover ? c.surfaceCard : const Color(0x00000000)),
            borderRadius: BorderRadius.circular(IcRadius.sm),
          ),
          child: Row(
            children: [
              if (widget.selected)
                Container(
                  width: 3,
                  height: 16,
                  margin: const EdgeInsets.only(right: 7),
                  decoration: BoxDecoration(
                    color: c.accent,
                    borderRadius: BorderRadius.circular(2),
                  ),
                ),
              Icon(
                widget.icon,
                size: widget.compact ? 14 : 16,
                color: widget.selected ? c.accent : c.textMuted,
              ),
              const SizedBox(width: 9),
              Expanded(
                child: Text(
                  widget.label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: IcType.text(
                    widget.compact ? 12.5 : 13.5,
                    weight: widget.selected ? FontWeight.w600 : FontWeight.w500,
                    color: widget.selected ? c.textStrong : c.textBody,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HeroActionTile extends StatefulWidget {
  const _HeroActionTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  State<_HeroActionTile> createState() => _HeroActionTileState();
}

class _HeroActionTileState extends State<_HeroActionTile> {
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
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: _hover ? c.surfaceSunk : c.surfaceCard,
            borderRadius: BorderRadius.circular(IcRadius.card),
            border: Border.all(color: _hover ? c.accent : c.borderHairline),
            boxShadow: _hover ? IcShadow.md(c) : IcShadow.sm(c),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  color: c.accentTint,
                  borderRadius: BorderRadius.circular(IcRadius.sm),
                ),
                child: Icon(widget.icon, size: 18, color: c.accent),
              ),
              const SizedBox(height: 10),
              Text(
                widget.title,
                style: IcType.text(
                  14,
                  weight: FontWeight.w600,
                  color: c.textStrong,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                widget.subtitle,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: IcType.text(12, color: c.textMuted, height: 1.35),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SampleCard extends StatefulWidget {
  const _SampleCard({required this.sample, required this.onTap});

  final GallerySample sample;
  final VoidCallback onTap;

  @override
  State<_SampleCard> createState() => _SampleCardState();
}

class _SampleCardState extends State<_SampleCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final s = widget.sample;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: _hover ? c.surfaceSunk : c.surfaceCard,
            borderRadius: BorderRadius.circular(IcRadius.card),
            border: Border.all(
              color: _hover ? c.borderInput : c.borderHairline,
            ),
            boxShadow: _hover ? IcShadow.md(c) : null,
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: c.accentTint,
                  borderRadius: BorderRadius.circular(IcRadius.button),
                ),
                child: Icon(s.icon, size: 20, color: c.accent),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      s.title,
                      style: IcType.text(
                        14,
                        weight: FontWeight.w600,
                        color: c.textStrong,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      s.subtitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: IcType.text(
                        12.5,
                        color: c.textMuted,
                        height: 1.35,
                      ),
                    ),
                    const SizedBox(height: 8),
                    TagPill(
                      label: s.signal,
                      tone: TagTone.neutral,
                      icon: LucideIcons.zap,
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

class _EntryCard extends StatefulWidget {
  const _EntryCard({required this.entry, required this.onTap});

  final GalleryEntry entry;
  final VoidCallback onTap;

  @override
  State<_EntryCard> createState() => _EntryCardState();
}

class _EntryCardState extends State<_EntryCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final e = widget.entry;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: _hover ? c.surfaceSunk : c.surfaceCard,
            borderRadius: BorderRadius.circular(IcRadius.card),
            border: Border.all(
              color: _hover ? c.borderInput : c.borderHairline,
            ),
            boxShadow: _hover ? IcShadow.md(c) : null,
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: c.accentTint,
                  borderRadius: BorderRadius.circular(IcRadius.button),
                ),
                child: Icon(e.icon, size: 20, color: c.accent),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      e.title,
                      style: IcType.text(
                        14,
                        weight: FontWeight.w600,
                        color: c.textStrong,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      e.description,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: IcType.text(
                        12.5,
                        color: c.textMuted,
                        height: 1.35,
                      ),
                    ),
                    const SizedBox(height: 8),
                    TagPill(label: e.category, tone: TagTone.neutral),
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
