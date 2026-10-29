import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter/material.dart';

/// Micro-credits as dollars with up to six decimals: 1500 micros -> "$0.0015".
String computeDollars(int micros) {
  final dollars = micros / 1e6;
  return '\$${dollars.toStringAsFixed(6).replaceFirst(RegExp(r'\.?0+$'), '')}';
}

String computeTime(DateTime? value) {
  if (value == null) return 'Time unavailable';
  final local = value.toLocal();
  String two(int v) => v.toString().padLeft(2, '0');
  return '${local.year}-${two(local.month)}-${two(local.day)} ${two(local.hour)}:${two(local.minute)}';
}

class ComputeUsagePanel extends StatefulWidget {
  const ComputeUsagePanel({
    super.key,
    required this.client,
    required this.workspaceId,
    required this.workspaceName,
    required this.onClose,
    this.refreshSignal,
  });
  final IntoChatOSUiClient? client;
  final String workspaceId, workspaceName;
  final VoidCallback onClose;
  final Listenable? refreshSignal;
  @override
  State<ComputeUsagePanel> createState() => _ComputeUsagePanelState();
}

class _ComputeUsagePanelState extends State<ComputeUsagePanel> {
  ComputeSummary? _summary;
  List<ComputeCost> _recent = const [];
  String _grouping = 'app';
  String? _error;
  bool _busy = false, _pendingRefresh = false;
  int _generation = 0;
  Timer? _autoRefresh;

  @override
  void initState() {
    super.initState();
    widget.refreshSignal?.addListener(_refresh);
    // Cancellation can prevent the final receipt reaching chat. Recover committed
    // usage while this panel remains open, without depending on that event.
    _autoRefresh = Timer.periodic(
      const Duration(seconds: 30),
      (_) => _refresh(),
    );
    unawaited(_load());
  }

  void _refresh() => unawaited(_load());

  @override
  void didUpdateWidget(ComputeUsagePanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.refreshSignal != widget.refreshSignal) {
      oldWidget.refreshSignal?.removeListener(_refresh);
      widget.refreshSignal?.addListener(_refresh);
    }
    if (oldWidget.workspaceId != widget.workspaceId ||
        oldWidget.client != widget.client) {
      _generation++;
      _summary = null;
      _recent = const [];
      _grouping = 'app';
      _error = null;
      _busy = _pendingRefresh = false;
      unawaited(_load());
    }
  }

  @override
  void dispose() {
    _generation++;
    _autoRefresh?.cancel();
    widget.refreshSignal?.removeListener(_refresh);
    super.dispose();
  }

  Future<void> _load() async {
    final client = widget.client;
    if (client == null) return;
    if (_busy) {
      _pendingRefresh = true;
      return;
    }
    final generation = ++_generation;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final summary = await client.readCompute(
        widget.workspaceId,
        by: _grouping,
        since: DateTime.now().subtract(const Duration(days: 30)),
      );
      if (mounted && generation == _generation) {
        setState(() => _summary = summary);
      }
    } catch (_) {
      if (mounted && generation == _generation) {
        setState(
          () => _error =
              'Could not load usage. Displayed totals may be out of date.',
        );
      }
    }
    try {
      final recent = await client.readComputeRecent(widget.workspaceId);
      if (mounted && generation == _generation) {
        setState(() => _recent = recent);
      }
    } catch (_) {
      // Recent costs are supplemental; the grouped totals already carry the error state.
    }
    if (mounted && generation == _generation) {
      setState(() => _busy = false);
      if (_pendingRefresh) {
        _pendingRefresh = false;
        unawaited(_load());
      }
    }
  }

  Widget _overview(BuildContext context) {
    final summary = _summary;
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 4, 20, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Brain account · ${widget.workspaceName}',
            style: Theme.of(context).textTheme.labelMedium,
          ),
          const SizedBox(height: 12),
          if (summary == null)
            if (_error == null)
              const Text('Loading compute…')
            else
              Text(_error!)
          else
            Wrap(
              spacing: 32,
              runSpacing: 12,
              children: [
                _amount(context, 'Balance', summary.balanceMicros),
                _amount(context, 'Spent · last 30 days', summary.spentMicros),
              ],
            ),
          if (summary != null &&
              _accountingCounts(
                summary.pendingCount,
                summary.unpricedCount,
                summary.unknownCount,
              ).isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                _accountingCounts(
                  summary.pendingCount,
                  summary.unpricedCount,
                  summary.unknownCount,
                ),
              ),
            ),
          if (summary?.knownListPriceMicros case final int value)
            Text('Known list price ${computeDollars(value)}'),
        ],
      ),
    );
  }

  Widget _amount(BuildContext context, String label, int micros) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(label, style: Theme.of(context).textTheme.labelMedium),
      Text(
        computeDollars(micros),
        style: Theme.of(context).textTheme.titleMedium,
      ),
    ],
  );

  Widget _groupingRow(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(20, 4, 20, 8),
    child: Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final group in const [
          ['origin', 'Origin'],
          ['app', 'App'],
          ['synapse', 'Synapse'],
          ['neuron', 'Neuron'],
          ['kind', 'Kind'],
        ])
          ChoiceChip(
            key: Key('compute-group-${group[0]}'),
            label: Text(group[1]),
            selected: _grouping == group[0],
            onSelected: (bool selected) {
              if (!selected || _grouping == group[0]) return;
              setState(() => _grouping = group[0]);
              unawaited(_load());
            },
          ),
      ],
    ),
  );

  Widget _breakdown(BuildContext context) {
    final summary = _summary;
    if (summary == null) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Spend by $_grouping',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 8),
          if (summary.rows.isEmpty)
            const Text('No usage recorded yet.')
          else
            for (final row in summary.rows)
              Row(
                children: [
                  Expanded(
                    child: Text(
                      row.key,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  Text(
                    '${row.count} calls',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(width: 12),
                  Text(
                    computeDollars(row.micros),
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ],
              ),
        ],
      ),
    );
  }

  Widget _recentSection(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(20, 8, 20, 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('Recent', style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 8),
        if (_recent.isEmpty)
          const Text('No recent usage.')
        else
          for (final cost in _recent)
            _CostRow(
              cost: cost,
              onTrace: cost.traceId.isEmpty
                  ? null
                  : () => showDialog<void>(
                      context: context,
                      builder: (_) => _ComputeTraceDialog(
                        client: widget.client!,
                        brainId: widget.workspaceId,
                        traceId: cost.traceId,
                      ),
                    ),
            ),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) {
    final client = widget.client;
    final header = Padding(
      padding: const EdgeInsets.fromLTRB(12, 8, 8, 4),
      child: Row(
        children: [
          Expanded(
            child: Text(
              'Compute',
              style: Theme.of(context).textTheme.titleLarge,
            ),
          ),
          IconButton(
            tooltip: 'Refresh usage',
            onPressed: _busy ? null : _refresh,
            icon: const Icon(Icons.refresh),
          ),
          IconButton(
            tooltip: 'Close Compute',
            onPressed: widget.onClose,
            icon: const Icon(Icons.close),
          ),
        ],
      ),
    );
    if (client == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          header,
          const Expanded(
            child: Center(child: Text('Not connected to a brain')),
          ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        header,
        if (_busy) const LinearProgressIndicator(minHeight: 2),
        Expanded(
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _overview(context),
                _groupingRow(context),
                _breakdown(context),
                const Divider(height: 1),
                _recentSection(context),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

String _accountingCounts(int? pending, int? unpriced, int? unknown) => [
  if (pending != null && pending > 0) '$pending pending',
  if (unpriced != null && unpriced > 0) '$unpriced unpriced',
  if (unknown != null && unknown > 0) '$unknown unknown',
].join(' · ');

class _CostRow extends StatelessWidget {
  const _CostRow({required this.cost, this.onTrace});
  final ComputeCost cost;
  final VoidCallback? onTrace;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${cost.item} · ${cost.kind == 'externalprovider' ? 'external provider' : cost.kind}${cost.synapse == null ? '' : ' · ${cost.synapse}'}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              Text(
                computeTime(cost.at),
                style: Theme.of(context).textTheme.bodySmall,
              ),
              if (cost.fundingKind == 'ownconnection')
                Text(
                  'Personal connection · platform charge ${computeDollars(cost.micros)}',
                ),
              if (cost.isPending) const Text('Pending accounting'),
              if (cost.chargePolicy == 'informational')
                const Text('Informational usage · excluded from totals'),
              if (cost.pricingStatus == 'unpriced')
                const Text('Unpriced provider usage')
              else if (cost.listPriceMicros case final int value)
                Text('List price ${computeDollars(value)}')
              else if (!cost.isPending)
                const Text('Pricing details unavailable'),
              if (cost.isPending && cost.estimatedListPriceMicros != null)
                Text(
                  'Estimated provider price ${computeDollars(cost.estimatedListPriceMicros!)}',
                ),
              if (cost.outcome case final String outcome)
                Text(switch (outcome) {
                  'succeeded' => 'Succeeded',
                  'httpfailed' => 'Provider returned an error',
                  'transportfailed' => 'Transport failed',
                  'cancelledbeforedispatch' => 'Cancelled before dispatch',
                  'cancelledafterdispatch' => 'Cancelled after dispatch',
                  'uncertain' => 'Outcome unknown',
                  _ => 'Outcome unavailable',
                }),
            ],
          ),
        ),
        const SizedBox(width: 12),
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(
              computeDollars(cost.micros),
              style: Theme.of(context).textTheme.bodySmall,
            ),
            if (onTrace != null)
              Tooltip(
                message: 'View trace ${cost.traceId}',
                child: TextButton(
                  onPressed: onTrace,
                  child: const Text('View trace'),
                ),
              ),
          ],
        ),
      ],
    ),
  );
}

class _ComputeTraceDialog extends StatefulWidget {
  const _ComputeTraceDialog({
    required this.client,
    required this.brainId,
    required this.traceId,
  });
  final IntoChatOSUiClient client;
  final String brainId, traceId;
  @override
  State<_ComputeTraceDialog> createState() => _ComputeTraceDialogState();
}

class _ComputeTraceDialogState extends State<_ComputeTraceDialog> {
  ComputeTracePage? _snapshot;
  final List<ComputeCost> _rows = [];
  String? _continuation, _error;
  String _status = 'detailunavailable';
  bool _busy = false;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _generation++;
    super.dispose();
  }

  Future<void> _load({bool more = false}) async {
    if (_busy) return;
    final generation = ++_generation;
    final cursor = more ? _continuation : null;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final page = await widget.client.readComputeTrace(
        widget.brainId,
        widget.traceId,
        continuationToken: cursor,
      );
      if (!mounted || generation != _generation) return;
      final available =
          page.status == 'complete' || page.status == 'incomplete';
      if (more &&
          available &&
          (page.snapshotWatermark != _snapshot?.snapshotWatermark ||
              page.continuationToken == cursor)) {
        throw StateError('Trace snapshot changed. Refresh trace.');
      }
      setState(() {
        if (!more || !available) {
          _rows.clear();
          _snapshot = page;
        }
        _rows.addAll(page.rows);
        _continuation = available ? page.continuationToken : null;
        _status = page.status;
      });
    } catch (error) {
      if (mounted && generation == _generation) {
        setState(
          () => _error = error is StateError
              ? error.message.toString()
              : 'Could not load trace detail. Try again.',
        );
      }
    } finally {
      if (mounted && generation == _generation) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final snapshot = _snapshot;
    final available = _status == 'complete' || _status == 'incomplete';
    return AlertDialog(
      title: Text('Trace ${widget.traceId}'),
      content: SizedBox(
        width: 640,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (_busy) const LinearProgressIndicator(),
              if (snapshot != null) ...[
                Text(switch (_status) {
                  'complete' =>
                    _continuation == null
                        ? 'Trace detail complete'
                        : 'Trace recorded · more rows available',
                  'incomplete' =>
                    'Trace incomplete. Some detail is not available yet.',
                  'expired' => 'Trace detail expired.',
                  _ => 'Trace detail unavailable for this history.',
                }),
                if (available) ...[
                  const SizedBox(height: 8),
                  Text('Charged ${computeDollars(snapshot.chargedMicros)}'),
                  Text(
                    'Known list price ${computeDollars(snapshot.knownListPriceMicros)}',
                  ),
                  if (_accountingCounts(
                    snapshot.pendingCount,
                    snapshot.unpricedCount,
                    snapshot.unknownCount,
                  ).isNotEmpty)
                    Text(
                      _accountingCounts(
                        snapshot.pendingCount,
                        snapshot.unpricedCount,
                        snapshot.unknownCount,
                      ),
                    ),
                ],
                const SizedBox(height: 12),
                for (final cost in _rows) _CostRow(cost: cost),
              ],
              if (_error != null) Text(_error!),
              if (_continuation != null)
                TextButton(
                  key: const Key('compute-trace-more'),
                  onPressed: _busy ? null : () => _load(more: true),
                  child: const Text('Load more'),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => _load(),
          child: const Text('Refresh trace'),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Close'),
        ),
      ],
    );
  }
}
