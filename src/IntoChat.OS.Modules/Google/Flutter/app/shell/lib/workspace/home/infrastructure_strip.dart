import 'dart:async';

import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter/material.dart';

enum InfrastructureHealth { green, amber, red }

// A silo row carries its Orleans membership status as the state (Active, Joining, Dead...); a
// connection row carries its connection status. A health, when one is given, decides first.
InfrastructureHealth infrastructureHealth(String state, String? health) {
  if (health == 'Healthy' || state == 'Active' || state == 'Connected') {
    return InfrastructureHealth.green;
  }
  if (health == 'Unhealthy' ||
      state == 'Dead' ||
      state == 'Stopping' ||
      state == 'ShuttingDown' ||
      state == 'Exited' ||
      state == 'FailedToStart' ||
      state == 'Stopped' ||
      state == 'Disconnected' ||
      state == 'Failing') {
    return InfrastructureHealth.red;
  }
  return InfrastructureHealth.amber;
}

Color infrastructureColor(InfrastructureHealth health) => switch (health) {
  InfrastructureHealth.green => Colors.green,
  InfrastructureHealth.amber => Colors.amber,
  InfrastructureHealth.red => Colors.red,
};

// One chip per silo, colored by health. The strip listens to the cluster's own signal: a silo changing
// state animates its chip, a silo that leaves the cluster loses it, nothing is polled.
class InfrastructureStrip extends StatefulWidget {
  const InfrastructureStrip({super.key, this.watch, this.onTap});

  final Stream<DeploymentChanged> Function()? watch;
  final ValueChanged<String>? onTap;

  @override
  State<InfrastructureStrip> createState() => _InfrastructureStripState();
}

class _InfrastructureStripState extends State<InfrastructureStrip> {
  DeploymentChanged? _deployment;
  String? _error;
  StreamSubscription<DeploymentChanged>? _subscription;

  @override
  void initState() {
    super.initState();
    _subscription = widget.watch?.call().listen(
      (cluster) => setState(() {
        _deployment = cluster;
        _error = null;
      }),
      onError: (Object error) => setState(() => _error = error.toString()),
    );
  }

  @override
  void dispose() {
    unawaited(_subscription?.cancel());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final motion = MediaQuery.of(context).disableAnimations
        ? Duration.zero
        : const Duration(milliseconds: 300);
    final cluster = _deployment;
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        if (_error != null)
          Text(
            _error!,
            key: const ValueKey('infrastructure-error'),
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        if (cluster != null && cluster.stage.toLowerCase() != 'steady')
          Chip(
            key: const ValueKey('cluster-stage'),
            label: Text('${cluster.stage} ${_short(cluster.sha)}'.trim()),
          ),
        for (final silo in cluster?.silos ?? const <SiloState>[])
          ActionChip(
            key: ValueKey('silo-${silo.id}'),
            avatar: AnimatedSwitcher(
              duration: motion,
              child: CircleAvatar(
                key: ValueKey(
                  'silo-dot-${silo.id}-${silo.state}-${silo.health}',
                ),
                radius: 5,
                backgroundColor: infrastructureColor(
                  infrastructureHealth(silo.state, silo.health),
                ),
              ),
            ),
            label: Text(silo.id),
            tooltip:
                '${silo.state}${silo.health == null ? '' : ' (${silo.health})'} ${_short(silo.sha)}'
                    .trim(),
            onPressed: widget.onTap == null
                ? null
                : () => widget.onTap!(silo.id),
          ),
      ],
    );
  }

  static String _short(String sha) =>
      sha.length > 7 ? sha.substring(0, 7) : sha;
}
