part of 'neuron_view.dart';

/// Inline presentation values never acquire their own load or watch lifecycle.
class _ImmutableView extends StatelessWidget {
  const _ImmutableView({
    required this.name,
    required this.generation,
    required this.root,
    required this.enabled,
    required this.dispatch,
    required this.markdown,
    required this.reference,
    required this.layout,
    this.localPreferences,
  });
  final String name;
  final int generation;
  final Map root;
  final bool enabled;
  final Future<void> Function(Map<String, dynamic>) dispatch;
  final Widget Function(String) markdown;
  final Widget Function(Map, Key) reference;
  final Widget Function(Map<String, dynamic>, List<Widget>, List) layout;
  final LocalPreferences? localPreferences;

  static final _random = Random.secure();
  static String _operationId() {
    final bytes = List.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  static Map<String, dynamic>? _tryParseUiMeta(Object? detail) {
    if (detail is! String || !detail.startsWith('{"_ui":')) return null;
    try {
      final decoded = jsonDecode(detail);
      return decoded is Map ? Map<String, dynamic>.from(decoded) : null;
    } catch (_) {
      return null;
    }
  }

  static CardSource _cardSource(BuildContext context, String sourceName) {
    final c = UiColors.of(context);
    return CardSource(
      sourceName,
      Icon(Icons.auto_awesome, size: 14, color: c.accent),
    );
  }

  /// The one button dispatch for every kind; null when it cannot act.
  /// A `copy:` action is handled here: it copies its payload and dispatches
  /// nothing.
  VoidCallback? _dispatchButton(BuildContext context, Map node) =>
      enabled && node['enabled'] != false && node['action'] is String
      ? () {
          FocusManager.instance.primaryFocus?.unfocus();
          final action = node['action'] as String;
          if (action.startsWith('copy:')) {
            unawaited(
              Clipboard.setData(ClipboardData(text: action.substring(5))).then(
                (_) {
                  if (context.mounted) {
                    showKitToast(context, 'Link copied.');
                  }
                },
                onError: (Object _) {
                  if (context.mounted) {
                    showKitToast(
                      context,
                      'Could not copy the link.',
                      tone: UiTone.error,
                    );
                  }
                },
              ),
            );
            return;
          }
          unawaited(
            dispatch({
              'kind': 'view',
              'name': name,
              'revision': generation,
              'field': node['key'],
              'action': node['action'],
              'operationId': _operationId(),
            }).catchError((Object _) {}),
          );
        }
      : null;

  Map? _findChildByKeySuffix(List<Map> children, String suffix) {
    for (final child in children) {
      final k = child['key'];
      if (k is String && k.endsWith(suffix)) return child;
    }
    return null;
  }

  Widget _buildRichCard(
    BuildContext context,
    String text,
    Map<String, dynamic> meta,
    List<Map> children,
    List<Widget> widgets,
  ) {
    final uiKind = meta['_ui'] as String? ?? '';
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);

    switch (uiKind) {
      case 'spacer':
        final size = (meta['size'] as num?)?.toDouble() ?? 12.0;
        return SizedBox(height: size, width: size);

      case 'grid':
        final cols = ((meta['columns'] as num?)?.toInt() ?? 2).clamp(1, 12);
        final gap = (meta['gap'] as num?)?.toDouble() ?? 12.0;
        if (widgets.isEmpty) return const SizedBox.shrink();
        final rows = <Widget>[];
        for (var i = 0; i < widgets.length; i += cols) {
          rows.add(
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              spacing: gap,
              children: [
                for (var col = 0; col < cols; col++)
                  Expanded(
                    child: i + col < widgets.length
                        ? widgets[i + col]
                        : const SizedBox.shrink(),
                  ),
              ],
            ),
          );
        }
        return Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          spacing: gap,
          children: rows,
        );

      case 'surface':
        final surfaceKind = meta['kind'] as String? ?? 'acrylic';
        final gap = (meta['gap'] as num?)?.toDouble() ?? 12.0;
        final pad = (meta['padding'] as num?)?.toDouble() ?? 16.0;
        final inner = Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          spacing: gap,
          children: [
            if (text.isNotEmpty)
              Text(
                text,
                style: UiType.displayStyle(
                  15,
                  weight: FontWeight.w600,
                  color: c.strong,
                ),
              ),
            ...widgets,
          ],
        );
        if (surfaceKind == 'recessed') {
          return Container(
            width: double.infinity,
            padding: EdgeInsets.all(pad),
            decoration: BoxDecoration(
              color: c.recessed,
              borderRadius: BorderRadius.circular(tokens.card),
              border: Border.all(color: c.hairline),
            ),
            child: inner,
          );
        }
        return SizedBox(
          width: double.infinity,
          child: UiKitSurface(padding: EdgeInsets.all(pad), child: inner),
        );

      case 'textfield':
        final fieldName = meta['name'] as String? ?? '';
        final value = meta['value'] as String? ?? '';
        final placeholder = meta['placeholder'] as String? ?? fieldName;
        final secret = meta['secret'] == true;
        final submitChild = _findChildByKeySuffix(children, '.submit');
        return _ImmutableFieldBox(
          label: fieldName,
          value: value,
          placeholder: placeholder,
          secret: secret,
          enabled: enabled,
          onSubmit: submitChild != null
              ? _dispatchButton(context, submitChild)
              : null,
        );

      case 'switch':
        final label = meta['label'] as String? ?? text;
        final value = meta['value'] == true;
        final swEnabled = meta['enabled'] != false && enabled;
        final locked = meta['locked'] == true;
        final toggleChild = _findChildByKeySuffix(children, '.toggle');
        final onToggle = toggleChild != null
            ? _dispatchButton(context, toggleChild)
            : null;
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            UiKitSwitch(
              value: value,
              locked: locked,
              onChanged: swEnabled && onToggle != null
                  ? (_) => onToggle()
                  : null,
            ),
            if (label.isNotEmpty) ...[
              const SizedBox(width: 10),
              Flexible(
                child: Text(
                  label,
                  style: UiType.textStyle(
                    13,
                    weight: FontWeight.w500,
                    color: c.strong,
                  ),
                ),
              ),
            ],
          ],
        );

      case 'select':
        final label = meta['label'] as String? ?? text;
        final selectedId = meta['selectedId'] as String?;
        final options = (meta['options'] as List? ?? const [])
            .whereType<Map>()
            .toList();
        final btnChildren = children.where((ch) => ch['kind'] == 1).toList();
        return Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (label.isNotEmpty) ...[
              Text(
                label,
                style: UiType.textStyle(
                  12,
                  weight: FontWeight.w600,
                  color: c.muted,
                ),
              ),
              const SizedBox(height: 6),
            ],
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (var i = 0; i < options.length; i++)
                  UiKitButton(
                    label: '${options[i]['label'] ?? options[i]['id'] ?? ''}',
                    compact: true,
                    style: options[i]['id'] == selectedId
                        ? UiKitButtonStyle.primary
                        : UiKitButtonStyle.secondary,
                    onPressed: i < btnChildren.length
                        ? _dispatchButton(context, btnChildren[i])
                        : null,
                  ),
              ],
            ),
          ],
        );

      case 'progress':
        final val = (meta['value'] as num?)?.toDouble() ?? 0.0;
        final label = meta['label'] as String? ?? text;
        final indeterminate = meta['indeterminate'] == true;
        return UiProgress(
          determinate: !indeterminate,
          value: val,
          label: label,
        );

      case 'infobar':
        final title = meta['title'] as String? ?? text;
        final body = meta['body'] as String? ?? '';
        final tone = meta['tone'] as String? ?? 'info';
        final severity = switch (tone) {
          'danger' => 'error',
          'warning' => 'warning',
          'success' => 'success',
          _ => 'info',
        };
        return Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            UiInfoBar(severity: severity, title: title, body: body),
            if (widgets.isNotEmpty) ...[
              const SizedBox(height: 8),
              Wrap(spacing: 8, runSpacing: 6, children: widgets),
            ],
          ],
        );

      case 'datatable':
        final title = meta['title'] as String? ?? text;
        final colLabels = (meta['columns'] as List? ?? const [])
            .map((e) => '$e')
            .toList();
        final rows = (meta['rows'] as List? ?? const [])
            .whereType<List>()
            .map((r) => r.map((cell) => '$cell').toList())
            .toList();
        final columns = [
          for (var cIdx = 0; cIdx < colLabels.length; cIdx++)
            TableColumn(
              colLabels[cIdx],
              numeric: rows.isNotEmpty &&
                  rows.every(
                    (r) =>
                        cIdx < r.length &&
                        double.tryParse(
                              r[cIdx].replaceAll(RegExp(r'[^0-9.\-]'), ''),
                            ) !=
                            null &&
                        RegExp(r'\d').hasMatch(r[cIdx]),
                  ),
            ),
        ];
        final sourceUri = meta['sourceNeuronUri'] as String?;
        final copyNode = _findChildByKeySuffix(children, '.copy');
        final sortNode = _findChildByKeySuffix(children, '.sort');
        final filterNode = _findChildByKeySuffix(children, '.filter');
        final copyAction = copyNode != null
            ? _dispatchButton(context, copyNode)
            : null;
        final sortAction = sortNode != null
            ? _dispatchButton(context, sortNode)
            : null;
        final filterAction = filterNode != null
            ? _dispatchButton(context, filterNode)
            : null;
        return TableCard(
          source: _cardSource(context, sourceUri ?? 'Data Table'),
          data: TableData(
            title: title,
            columns: columns,
            rows: rows,
            from: sourceUri ?? 'DigitalBrain',
            sourceNeuronUri: sourceUri,
          ),
          state: rows.isEmpty ? TableState.empty : TableState.ready,
          onCopyNeuronUri: copyAction != null ? (_) => copyAction() : null,
          onSort: sortAction != null ? (_, _) => sortAction() : null,
          onFilter: filterAction != null ? (_) => filterAction() : null,
        );

      case 'chart':
        final chartKindStr = (meta['kind'] as String? ?? 'bar').toLowerCase();
        final chartKind = chartKindStr == 'line'
            ? ChartKind.line
            : ChartKind.columns;
        final title = meta['title'] as String? ?? text;
        final subtitle = meta['subtitle'] as String?;
        final unit = meta['unit'] as String? ?? '';
        final seriesName = meta['seriesName'] as String? ?? 'Primary';
        final compareName = meta['compareName'] as String? ?? '';
        final sourceUri = meta['sourceNeuronUri'] as String?;
        final points = (meta['points'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (p) => ChartPoint(
                '${p['label'] ?? p['id'] ?? ''}',
                (p['value'] as num?)?.toDouble() ?? 0,
              ),
            )
            .toList();
        final comparePoints = (meta['comparePoints'] as List?)
            ?.whereType<Map>()
            .map((p) => (p['value'] as num?)?.toDouble() ?? 0.0)
            .toList();
        final copyNode = _findChildByKeySuffix(children, '.copy');
        final kindNode = _findChildByKeySuffix(children, '.kind');
        final copyAction = copyNode != null
            ? _dispatchButton(context, copyNode)
            : null;
        final kindAction = kindNode != null
            ? _dispatchButton(context, kindNode)
            : null;
        return ChartCard(
          source: _cardSource(context, subtitle ?? sourceUri ?? 'Chart'),
          data: ChartData(
            title: title,
            points: points,
            from: subtitle ?? sourceUri ?? 'DigitalBrain',
            prefix: unit,
            compare: comparePoints,
            seriesName: seriesName,
            compareName: compareName,
            sourceNeuronUri: sourceUri,
          ),
          state: points.isEmpty ? ChartState.empty : ChartState.ready,
          kind: chartKind,
          onCopyNeuronUri: copyAction != null ? (_) => copyAction() : null,
          onSwitchChartKind: kindAction != null ? (_) => kindAction() : null,
        );

      case 'approval':
        final verb = meta['verb'] as String? ?? 'Execute';
        final target = meta['target'] as String? ?? text;
        final sourceApp = meta['sourceApp'] as String? ?? 'Ino';
        final sourceUri = meta['sourceNeuronUri'] as String?;
        final scopeUri = meta['scopeUri'] as String?;
        final rows = [
          for (final r in (meta['rows'] as List? ?? const []).whereType<Map>())
            ApprovalRow(
              label: '${r['key'] ?? ''}',
              oldValue: 'current',
              newValue: '${r['value'] ?? ''}',
            ),
          if (sourceUri != null && sourceUri.isNotEmpty)
            ApprovalRow(
              label: 'source',
              oldValue: 'neuron',
              newValue: sourceUri,
            ),
          if (scopeUri != null && scopeUri.isNotEmpty)
            ApprovalRow(
              label: 'scope',
              oldValue: 'policy',
              newValue: scopeUri,
            ),
        ];
        final stateStr = (meta['state'] as String? ?? 'pending').toLowerCase();
        final approvalState = switch (stateStr) {
          'approved' || 'settled' || 'done' => ApprovalState.approved,
          'rejected' || 'denied' => ApprovalState.rejected,
          'saving' => ApprovalState.saving,
          'error' => ApprovalState.error,
          _ => ApprovalState.pending,
        };
        final rememberHint = meta['rememberHint'] as String?;
        final approveNode = _findChildByKeySuffix(children, '.approve');
        final rejectNode = _findChildByKeySuffix(children, '.reject');
        final editNode = _findChildByKeySuffix(children, '.edit');
        return ApprovalCard(
          source: _cardSource(context, sourceApp),
          data: ApprovalData(
            title: '$verb $target'.trim(),
            summary: rememberHint ?? (scopeUri ?? 'Requires explicit confirmation'),
            rows: rows.isEmpty
                ? [
                    ApprovalRow(
                      label: target,
                      oldValue: 'pending',
                      newValue: verb,
                    ),
                  ]
                : rows,
            valueLabel: 'Value',
            systemName: sourceApp,
          ),
          state: approvalState,
          onApprove: approveNode != null
              ? _dispatchButton(context, approveNode)
              : null,
          onReject: rejectNode != null
              ? _dispatchButton(context, rejectNode)
              : null,
          onEdited: editNode != null
              ? _dispatchButton(context, editNode)
              : null,
        );

      case 'choice':
        final question = meta['question'] as String? ?? text;
        final selectedIndex = (meta['selectedIndex'] as num?)?.toInt();
        final sourceApp = meta['sourceApp'] as String? ?? 'Ino';
        final options = (meta['options'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (o) => ChoiceOption(
                '${o['label'] ?? ''}',
                detail: o['note'] as String?,
              ),
            )
            .toList();
        final optButtons = children.where((ch) => ch['kind'] == 1).toList();
        return ChoiceCard(
          source: _cardSource(context, sourceApp),
          question: question,
          options: options,
          picked: selectedIndex,
          onPick: (idx) {
            if (idx >= 0 && idx < optButtons.length) {
              _dispatchButton(context, optButtons[idx])?.call();
            }
          },
        );

      case 'draft':
        final channelStr = (meta['channel'] as String? ?? 'email').toLowerCase();
        final channel = switch (channelStr) {
          'slack' => DraftChannel.slack,
          'sms' || 'message' || 'post' => DraftChannel.sms,
          _ => DraftChannel.email,
        };
        final recipient = meta['recipient'] as String? ?? '';
        final subject = meta['subject'] as String?;
        final body = meta['body'] as String? ?? '';
        final sent = meta['sent'] == true;
        final sendNode = _findChildByKeySuffix(children, '.send');
        final cancelNode = _findChildByKeySuffix(children, '.cancel');
        return DraftCard(
          source: _cardSource(context, channel.noun),
          data: DraftData(
            channel: channel,
            to: recipient,
            subject: subject,
            body: body,
            from: 'owner@intochat.os',
          ),
          state: sent ? DraftState.sent : DraftState.pending,
          onSend: sendNode != null ? _dispatchButton(context, sendNode) : null,
          onDiscard: cancelNode != null
              ? _dispatchButton(context, cancelNode)
              : null,
        );

      case 'record':
        final kindStr = meta['kind'] as String? ?? 'Record';
        final title = meta['title'] as String? ?? text;
        final subtitle = meta['subtitle'] as String?;
        final sourceApp = meta['sourceApp'] as String? ?? 'DigitalBrain';
        final fields = <RecordField>[
          for (final f in (meta['fields'] as List? ?? const []).whereType<Map>())
            RecordField('${f['key'] ?? ''}', '${f['value'] ?? ''}'),
        ];
        final openNode = _findChildByKeySuffix(children, '.open');
        return RecordCard(
          source: _cardSource(context, sourceApp),
          data: RecordData(
            objectLabel: kindStr,
            name: title,
            subtitle: subtitle,
            fields: fields,
            linkLabel: 'Open in $sourceApp',
          ),
          onOpen: openNode != null ? _dispatchButton(context, openNode) : null,
        );

      case 'secret':
        final service = meta['service'] as String? ?? text;
        final keyName = meta['keyName'] as String? ?? 'API_KEY';
        final hint = meta['hint'] as String? ?? 'Stored encrypted in your personal vault.';
        final saved = meta['saved'] == true;
        final saveNode = _findChildByKeySuffix(children, '.save');
        final skipNode = _findChildByKeySuffix(children, '.skip');
        return SecretCard(
          source: _cardSource(context, service),
          data: SecretRequestData(
            name: '$service ($keyName)',
            why: hint,
          ),
          state: saved ? SecretState.saved : SecretState.pending,
          onEnter: saveNode != null ? _dispatchButton(context, saveNode) : null,
          onSkip: skipNode != null ? _dispatchButton(context, skipNode) : null,
        );

      case 'job':
        final title = meta['title'] as String? ?? text;
        final currentStep = (meta['currentStep'] as num?)?.toInt() ?? 0;
        final progress = (meta['progress'] as num?)?.toDouble() ?? 0.5;
        final eta = meta['eta'] as String?;
        final done = meta['done'] == true;
        final rawSteps = (meta['steps'] as List? ?? const [])
            .whereType<Map>()
            .toList();
        final steps = <JobStep>[
          for (var i = 0; i < rawSteps.length; i++)
            JobStep(
              '${rawSteps[i]['label'] ?? ''}',
              detail: rawSteps[i]['detail'] as String?,
              status: done || i < currentStep
                  ? StepStatus.done
                  : (i == currentStep ? StepStatus.running : StepStatus.waiting),
            ),
        ];
        final cancelNode = _findChildByKeySuffix(children, '.cancel');
        return JobCard(
          source: _cardSource(context, 'Background Job'),
          data: JobData(
            title: title,
            steps: steps,
            progress: progress,
            elapsed: eta,
          ),
          state: done ? JobState.done : JobState.running,
          onCancel: cancelNode != null
              ? _dispatchButton(context, cancelNode)
              : null,
        );

      case 'brain3d':
        final selectedId = meta['selectedId'] as String?;
        final height = (meta['height'] as num?)?.toDouble() ?? 340.0;
        final interactive = meta['interactive'] != false;
        final showHud = meta['showHud'] != false;
        final rawNodes = (meta['nodes'] as List? ?? const [])
            .whereType<Map>()
            .toList();
        final nodes3d = <BrainNode3D>[
          for (var i = 0; i < rawNodes.length; i++)
            () {
              final n = rawNodes[i];
              final phase =
                  (n['orbitPhase'] as num?)?.toDouble() ??
                  (i * 2 * pi / max(1, rawNodes.length));
              final tilt = (n['orbitTilt'] as num?)?.toDouble() ?? 0.2;
              final orbitR =
                  ((n['orbitRadius'] as num?)?.toDouble() ?? 140.0) / 260.0;
              final pos = i == 0 && orbitR == 0
                  ? (0.0, 0.0, 0.0)
                  : (
                      orbitR * cos(phase),
                      orbitR * sin(tilt) * sin(phase),
                      orbitR * sin(phase),
                    );
              return BrainNode3D(
                id: '${n['id'] ?? ''}',
                neuronUri:
                    '${n['subtitle'] ?? 'neuron://owner/${n['category'] ?? 'os'}/${n['id'] ?? ''}'}',
                label: '${n['label'] ?? ''}',
                module: '${n['category'] ?? 'DigitalBrain'}',
                position: pos,
                status: switch ('${n['status'] ?? 'healthy'}'.toLowerCase()) {
                  'pulse' || 'working' => BrainNodeStatus.working,
                  'attention' || 'warn' || 'warning' => BrainNodeStatus.warning,
                  'error' || 'danger' => BrainNodeStatus.error,
                  _ => BrainNodeStatus.healthy,
                },
              );
            }(),
        ];
        final synapses3d = (meta['synapses'] as List? ?? const [])
            .whereType<Map>()
            .map(
              (s) => BrainSynapse3D(
                '${s['fromId'] ?? ''}',
                '${s['toId'] ?? ''}',
              ),
            )
            .toList();
        final nodeButtons = children.where((ch) => ch['kind'] == 1).toList();
        return SizedBox(
          height: height,
          width: double.infinity,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(tokens.card),
            child: BrainScene3D(
              nodes: nodes3d.isEmpty ? defaultBrainNodes : nodes3d,
              synapses: synapses3d.isEmpty ? defaultBrainSynapses : synapses3d,
              selectedId: selectedId,
              interactive: interactive,
              showHud: showHud,
              onSelectNode: (selectedNode) {
                final idx = nodes3d.indexWhere(
                  (item) => item.id == selectedNode.id,
                );
                if (idx >= 0 && idx < nodeButtons.length) {
                  _dispatchButton(context, nodeButtons[idx])?.call();
                }
              },
            ),
          ),
        );

      case 'neuronRef':
        final refKind = meta['kind'] as String? ?? 'neuron';
        final neuronUri = meta['neuronUri'] as String? ?? text;
        final title = meta['title'] as String?;
        final copyNode = _findChildByKeySuffix(children, '.copy');
        final embeddedRefs = [
          for (var i = 0; i < children.length; i++)
            if (children[i]['kind'] == 5) widgets[i],
        ];
        return UiKitSurface(
          padding: const EdgeInsets.all(12),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  ViewBadge(text: refKind, tone: 2, icon: 'cpu'),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      title ?? neuronUri,
                      style: UiType.tabularStyle(12, color: c.strong),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  if (copyNode != null)
                    UiKitButton(
                      label: 'Copy URI',
                      icon: 'copy',
                      compact: true,
                      style: UiKitButtonStyle.ghost,
                      onPressed: _dispatchButton(context, copyNode),
                    ),
                ],
              ),
              if (embeddedRefs.isNotEmpty) ...[
                const SizedBox(height: 10),
                ...embeddedRefs,
              ],
            ],
          ),
        );

      default:
        break;
    }

    return SizedBox(
      width: double.infinity,
      child: UiKitSurface(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (text.isNotEmpty)
              Text(text, style: Theme.of(context).textTheme.titleLarge),
            ...widgets,
          ],
        ),
      ),
    );
  }

  /// [slotted]: the node fills an explicit Row/Column extent.
  Widget _node(
    BuildContext context,
    Map node,
    int depth, {
    bool slotted = false,
  }) {
    if (depth > 32) return const Text('This component cannot be displayed.');
    final key = ValueKey('view:$name:${node['key']}');
    final children = (node['children'] as List? ?? const [])
        .whereType<Map>()
        .take(512)
        .toList();
    // Built on demand so a List builds only its visible rows.
    final extents = node['kind'] == 2 || node['kind'] == 3
        ? node['extents'] as List? ?? const []
        : const [];
    late final widgets = [
      for (var i = 0; i < children.length; i++)
        _node(
          context,
          children[i],
          depth + 1,
          slotted: switch (extents.elementAtOrNull(i)) {
            final num extent => extent > 0,
            _ => false,
          },
        ),
    ];
    final text = node['text'] as String? ?? '';
    Widget content;
    switch (node['kind']) {
      case 0:
        final meta = _tryParseUiMeta(node['detail']);
        if (meta != null) {
          final c = UiColors.of(context);
          final uiKind = meta['_ui'] as String? ?? '';
          if (uiKind == 'heading') {
            final level = (meta['level'] as num?)?.toInt() ?? 2;
            final subtitle = meta['subtitle'] as String?;
            final fontSize = switch (level) {
              1 => 22.0,
              2 => 17.0,
              _ => 15.0,
            };
            content = Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  text,
                  style: UiType.displayStyle(
                    fontSize,
                    weight: FontWeight.w700,
                    color: c.strong,
                  ),
                ),
                if (subtitle != null && subtitle.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    subtitle,
                    style: UiType.textStyle(12.5, color: c.muted),
                  ),
                ],
              ],
            );
          } else {
            final tone = meta['tone'] as String? ?? 'default';
            final tabular = meta['tabular'] == true;
            final mono = meta['mono'] == true;
            final size = (meta['size'] as num?)?.toDouble() ?? 13.0;
            final color = switch (tone) {
              'strong' => c.strong,
              'muted' => c.muted,
              'subtle' => c.subtle,
              'accent' => c.accent,
              'success' => c.done,
              'warning' => c.working,
              'danger' => c.error,
              _ => c.body,
            };
            final style = mono
                ? UiType.tabularStyle(size, color: color)
                : UiType.textStyle(size, color: color, tnum: tabular);
            content = SelectionArea(child: Text(text, style: style));
          }
        } else {
          content = markdown(text);
        }
      case 1:
        final forceCompact = node['detail'] == 'small';
        // Server slots shorter than the 48px button get the compact one.
        content = LayoutBuilder(
          builder: (context, constraints) => viewButton(
            node,
            _dispatchButton(context, node),
            compact: forceCompact || constraints.maxHeight < 48,
          ),
        );
        // Only an explicit extent stretches a button; elsewhere it keeps
        // its content width, capped by the space it is given.
        if (!slotted) {
          content = Align(
            alignment: AlignmentDirectional.centerStart,
            heightFactor: 1,
            child: content,
          );
        }
      case 2:
      case 3:
        if (node['kind'] == 2 &&
            node['key'] == 'settings' &&
            children.length >= 2 &&
            children[0]['key'] == 'tabs') {
          content = _buildSettingsDesktopView(
            context,
            node,
            children,
            widgets,
            depth,
          );
          break;
        }
        content = layout(
          {
            'mode': node['kind'] == 3
                ? 'row'
                : (node['extents'] as List? ?? const []).isEmpty
                ? 'list'
                : 'column',
            'extents': node['extents'],
            'gap': node['gap'],
            'followEnd': node['followEnd'],
            'stretch': const {
              'view',
              'split',
              'files-full',
              'browse-split',
            }.contains(node['key']),
          },
          widgets,
          children,
        );
      case 4:
        final meta = _tryParseUiMeta(node['detail']);
        if (meta != null) {
          content = _buildRichCard(context, text, meta, children, widgets);
        } else {
          content = SizedBox(
            width: double.infinity,
            child: UiKitSurface(
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (text.isNotEmpty)
                      Text(text, style: Theme.of(context).textTheme.titleLarge),
                    if (node['detail'] case final String detail
                        when detail.isNotEmpty)
                      markdown(detail),
                    ...widgets,
                  ],
                ),
              ),
            ),
          );
        }
      case 5:
        final target = node['reference'];
        if (target is Map &&
            target['kind'] is String &&
            target['name'] is String) {
          return reference(target, key);
        }
        content = const Text('This component cannot be displayed.');
      case 6:
        content = ViewMenu(
          node: node,
          action: (node) => _dispatchButton(context, node),
          reference: (child) => switch (child['reference']) {
            final Map target
                when target['kind'] is String && target['name'] is String =>
              reference(target, ValueKey('view:$name:${child['key']}')),
            _ => const SizedBox.shrink(),
          },
        );
      case 7:
        content = ViewModal(
          node: node,
          children: [
            // Slotted buttons are not aligned, so they fill the modal width.
            for (final child in children)
              (
                child,
                child['kind'] == 1
                    ? _node(context, child, depth + 1, slotted: true)
                    : widgets[children.indexOf(child)],
              ),
          ],
          action: (node) => _dispatchButton(context, node),
        );
      case 8:
        content = ViewToast(
          node: node,
          generation: generation,
          action: (node) => _dispatchButton(context, node),
        );
      case 9:
        content = ViewList(
          count: children.length,
          row: (i) => _node(context, children[i], depth + 1),
        );
      case 10:
        content = ViewBadge(
          text: text,
          tone: node['tone'],
          icon: node['icon'] is String ? node['icon'] as String : null,
        );
      case 11:
        content = ViewAvatar(initials: text);
      case 12:
        content = ViewDivider(label: text);
      case 13:
        content = ViewTabs(
          node: node,
          action: (node) => _dispatchButton(context, node),
        );
      default:
        content = const Text('This component cannot be displayed.');
    }
    return KeyedSubtree(key: key, child: content);
  }

  static IconData _settingsIconFor(String tabKey) => switch (tabKey) {
    'tab-general' => LucideIcons.slidersHorizontal,
    'tab-models' => LucideIcons.cpu,
    'tab-keys' => LucideIcons.keyRound,
    'tab-connections' => LucideIcons.plug,
    'tab-modules' => LucideIcons.boxes,
    'tab-apps' => LucideIcons.layoutGrid,
    'tab-variables' => LucideIcons.braces,
    'tab-notifications' => LucideIcons.bell,
    'tab-archived' => LucideIcons.archive,
    'tab-account' => LucideIcons.userCheck,
    'tab-danger' => LucideIcons.triangleAlert,
    'tab-billing' => LucideIcons.creditCard,
    _ => LucideIcons.settings,
  };

  Widget _buildSettingsDesktopView(
    BuildContext context,
    Map node,
    List<Map> children,
    List<Widget> widgets,
    int depth,
  ) {
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < 680) {
          return layout(
            {
              'mode': 'column',
              'extents': node['extents'],
              'gap': node['gap'],
              'followEnd': node['followEnd'],
            },
            widgets,
            children,
          );
        }
        final c = UiColors.of(context);
        final tabsNode = children[0];
        final tabItems = (tabsNode['children'] as List? ?? const [])
            .whereType<Map>()
            .toList();
        final bodyNode = children[1];
        final bodyKey = bodyNode['key'] as String? ?? '';
        final bodyChildren = (bodyNode['children'] as List? ?? const [])
            .whereType<Map>()
            .toList();
        final bodyWidgets = [
          for (final child in bodyChildren) _node(context, child, depth + 2),
        ];

        final headingText = bodyChildren.isNotEmpty
            ? bodyChildren[0]['text'] as String? ?? ''
            : '';
        final descriptionText = bodyChildren.length > 1
            ? bodyChildren[1]['text'] as String? ?? ''
            : '';
        final restWidgets = bodyWidgets.length > 2
            ? bodyWidgets.sublist(2)
            : bodyWidgets;

        final currentTheme = localPreferences?.read(PreferenceStore.localTheme);

        return Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 232,
              decoration: BoxDecoration(
                color: c.recessed,
                border: Border(right: BorderSide(color: c.hairline)),
              ),
              padding: const EdgeInsets.fromLTRB(12, 16, 12, 16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(8, 0, 8, 14),
                    child: Row(
                      children: [
                        Container(
                          width: 26,
                          height: 26,
                          decoration: BoxDecoration(
                            color: c.accentTint,
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Icon(
                            LucideIcons.settings,
                            size: 14,
                            color: c.accent,
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'System Settings',
                                style: UiType.textStyle(
                                  13,
                                  weight: FontWeight.w700,
                                  color: c.strong,
                                ),
                              ),
                              Text(
                                'neuron://local/apps/settings',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: UiType.tabularStyle(10.5, color: c.muted),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                  Expanded(
                    child: ListView.separated(
                      itemCount: tabItems.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 2),
                      itemBuilder: (context, index) {
                        final tab = tabItems[index];
                        final tabKey = tab['key'] as String? ?? 'tab-$index';
                        final label = tab['text'] as String? ?? '';
                        final selected = tab['selected'] == true;
                        final onTap = _dispatchButton(context, tab);
                        final isDanger = tabKey == 'tab-danger';
                        return KeyedSubtree(
                          key: ValueKey('view:$name:$tabKey'),
                          child: GestureDetector(
                            onTap: onTap,
                            child: MouseRegion(
                              cursor: onTap != null
                                  ? SystemMouseCursors.click
                                  : SystemMouseCursors.basic,
                              child: AnimatedContainer(
                                duration: IcMotion.fast,
                                height: 34,
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                ),
                                decoration: BoxDecoration(
                                  color: selected
                                      ? c.accentTint
                                      : Colors.transparent,
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: Row(
                                  children: [
                                    Icon(
                                      _settingsIconFor(tabKey),
                                      size: 15,
                                      color: isDanger
                                          ? c.error
                                          : (selected ? c.accent : c.muted),
                                    ),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: Text(
                                        label,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: UiType.textStyle(
                                          13,
                                          weight: selected
                                              ? FontWeight.w600
                                              : FontWeight.w500,
                                          color: onTap == null && !selected
                                              ? c.subtle
                                              : (isDanger
                                                    ? c.error
                                                    : (selected
                                                          ? c.strong
                                                          : c.body)),
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
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
            Expanded(
              child: ColoredBox(
                color: c.page,
                child: SingleChildScrollView(
                  key: ValueKey('settings-scroll:$bodyKey'),
                  padding: const EdgeInsets.fromLTRB(36, 28, 36, 48),
                  child: Align(
                    alignment: Alignment.topLeft,
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 820),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          if (headingText.isNotEmpty)
                            Text(
                              headingText,
                              style: UiType.displayStyle(
                                22,
                                weight: FontWeight.w700,
                                color: c.strong,
                              ),
                            ),
                          if (descriptionText.isNotEmpty) ...[
                            const SizedBox(height: 6),
                            Text(
                              descriptionText,
                              style: UiType.textStyle(13, color: c.muted),
                            ),
                          ],
                          const SizedBox(height: 20),
                          if (bodyKey == 'tab-body-general' &&
                              localPreferences != null) ...[
                            Text(
                              'UI IDENTITY & GLASS MATERIALITY',
                              style: UiType.textStyle(
                                11,
                                weight: FontWeight.w700,
                                color: c.muted,
                              ),
                            ),
                            const SizedBox(height: 10),
                            Wrap(
                              spacing: 10,
                              runSpacing: 10,
                              children: [
                                for (final preset in IcStylePreset.values)
                                  _buildPresetCard(
                                    context,
                                    c,
                                    preset,
                                    currentTheme == preset.id ||
                                        (currentTheme == 'intochat' &&
                                            preset ==
                                                IcStylePreset.fluentAcrylic),
                                  ),
                              ],
                            ),
                            const SizedBox(height: 22),
                          ],
                          UiKitSurface(
                            padding: const EdgeInsets.all(20),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              spacing: 14,
                              children: restWidgets,
                            ),
                          ),
                          if (widgets.length > 2) ...[
                            const SizedBox(height: 14),
                            for (var i = 2; i < widgets.length; i++) widgets[i],
                          ],
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildPresetCard(
    BuildContext context,
    UiColors c,
    IcStylePreset preset,
    bool active,
  ) {
    final palette = IcColors.forPreset(preset, dark: true);
    return GestureDetector(
      onTap: () {
        unawaited(
          localPreferences?.write(PreferenceStore.localTheme, preset.id) ??
              Future.value(),
        );
      },
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        child: AnimatedContainer(
          duration: IcMotion.fast,
          width: 152,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: active ? c.accentTint : c.surface,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: active ? c.accent : c.hairline,
              width: active ? 1.5 : 1,
            ),
            boxShadow: active ? UiShadows.sm(c) : null,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    width: 18,
                    height: 18,
                    decoration: BoxDecoration(
                      color: palette.surfacePage,
                      borderRadius: BorderRadius.circular(5),
                      border: Border.all(color: palette.borderInput),
                    ),
                    alignment: Alignment.center,
                    child: Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: palette.accent,
                        shape: BoxShape.circle,
                      ),
                    ),
                  ),
                  const Spacer(),
                  if (palette.isGlass)
                    const TagPill(label: 'Glass', tone: TagTone.accent),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                preset.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: UiType.textStyle(
                  12.5,
                  weight: FontWeight.w600,
                  color: c.strong,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                preset.description,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: UiType.textStyle(11, color: c.muted),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => _node(context, root, 0);
}

class _ImmutableFieldBox extends StatefulWidget {
  const _ImmutableFieldBox({
    required this.label,
    required this.value,
    required this.placeholder,
    required this.secret,
    required this.enabled,
    this.onSubmit,
  });

  final String label;
  final String value;
  final String placeholder;
  final bool secret;
  final bool enabled;
  final VoidCallback? onSubmit;

  @override
  State<_ImmutableFieldBox> createState() => _ImmutableFieldBoxState();
}

class _ImmutableFieldBoxState extends State<_ImmutableFieldBox> {
  late final TextEditingController _controller = TextEditingController(
    text: widget.value,
  );
  final FocusNode _focus = FocusNode();

  @override
  void didUpdateWidget(_ImmutableFieldBox oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.value != widget.value && _controller.text != widget.value) {
      _controller.text = widget.value;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return UiKitField(
      label: widget.label,
      placeholder: widget.placeholder,
      controller: _controller,
      focusNode: _focus,
      secret: widget.secret,
      enabled: widget.enabled,
      onSubmitted: widget.onSubmit != null ? (_) => widget.onSubmit!() : null,
    );
  }
}
