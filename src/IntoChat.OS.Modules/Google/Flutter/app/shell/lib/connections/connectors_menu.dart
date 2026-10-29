import 'package:flutter/material.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

typedef OpenUrl = Future<void> Function(Uri url);

final class ConnectorsMenu extends StatelessWidget {
  const ConnectorsMenu({
    super.key,
    required this.connectors,
    required this.onSelect,
  });
  final List<Map<String, dynamic>> connectors;
  final void Function(String connectorId) onSelect;

  /// Figma `.connection-row`: 48 high, radius 10, padding 7x9, plug icon
  /// and a strong 13 title.
  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final connector in connectors)
          InkWell(
            key: Key('connector_${connector['id']}'),
            borderRadius: BorderRadius.circular(
              UiTokens.of(context).controlRadius,
            ),
            hoverColor: c.surface,
            onTap: () => onSelect('${connector['id']}'),
            child: ConstrainedBox(
              constraints: const BoxConstraints(minHeight: 48),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 7),
                child: Row(
                  children: [
                    Icon(uiIcon('plug'), size: uiIconSize, color: c.muted),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        '${connector['displayName']}',
                        style: type.rowTitle,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
