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
  });
  final String name;
  final int generation;
  final Map root;
  final bool enabled;
  final Future<void> Function(Map<String, dynamic>) dispatch;
  final Widget Function(String) markdown;
  final Widget Function(Map, Key) reference;
  final Widget Function(Map<String, dynamic>, List<Widget>, List) layout;

  static final _random = Random.secure();
  static String _operationId() {
    final bytes = List.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
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
        content = markdown(text);
      case 1:
        // Server slots shorter than the 48px button get the 36px compact one.
        content = LayoutBuilder(
          builder: (context, constraints) => viewButton(
            node,
            _dispatchButton(context, node),
            compact: constraints.maxHeight < 48,
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
          },
          widgets,
          children,
        );
      case 4:
        content = SizedBox(
          width: double.infinity,
          child: UiKitSurface(
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
        );
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
        content = ViewBadge(text: text, tone: node['tone']);
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

  @override
  Widget build(BuildContext context) => _node(context, root, 0);
}
