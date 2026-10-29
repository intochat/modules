import 'package:flutter/material.dart';

import '../components/kit/ui_kit_button.dart';
import '../components/kit/ui_kit_icon_button.dart';
import '../theme/ui_colors.dart';
import '../theme/ui_theme.dart';
import '../theme/ui_tokens.dart';
import 'chat_app_card.dart';
import 'chat_copy.dart';
import 'chat_message_toolbar.dart';

/// Renders a chat projection; input and execution belong to composed neurons.
///
/// Figma `.message-column`: user messages are right-aligned bubbles, answers
/// are plain text with a toolbar. A failed answer shows fixed copy, never the
/// error it carries.
class UiChatTranscript extends StatefulWidget {
  const UiChatTranscript({
    super.key,
    required this.messages,
    required this.markdown,
    this.onRetry,
    this.onOpenApp,
  });

  final List<Map<String, dynamic>> messages;
  final Widget Function(String) markdown;

  /// Sends the given user message again; offered on the last answer only.
  final void Function(String text)? onRetry;

  /// Dispatches a card's workspace action.
  final void Function(Map<String, dynamic> action)? onOpenApp;

  @override
  State<UiChatTranscript> createState() => _UiChatTranscriptState();
}

class _UiChatTranscriptState extends State<UiChatTranscript> {
  final _scroll = ScrollController();
  bool _following = true;
  bool _far = false;

  @override
  void initState() {
    super.initState();
    _scroll.addListener(_positionChanged);
    _follow();
  }

  @override
  void didUpdateWidget(UiChatTranscript oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (_following) _follow();
  }

  void _positionChanged() {
    final position = _scroll.position;
    final following = position.extentAfter < 64;
    // Jump to latest appears only more than one viewport from the end.
    final far = position.extentAfter > position.viewportDimension;
    if (following != _following || far != _far) {
      setState(() {
        _following = following;
        _far = far;
      });
    }
  }

  void _follow() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _scroll.hasClients) {
        _scroll.jumpTo(_scroll.position.maxScrollExtent);
      }
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  static bool _isUser(Map message) =>
      message['role'] == 0 ||
      message['role'] == 'user' ||
      message['role'] == 'User';

  @override
  Widget build(BuildContext context) {
    final c = UiColors.of(context);
    if (widget.messages.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            ChatCopy.empty,
            textAlign: TextAlign.center,
            style: UiType.of(context).body.copyWith(color: c.muted),
          ),
        ),
      );
    }
    final lastAssistant = widget.messages.lastIndexWhere(
      (message) => !_isUser(message),
    );
    return Stack(
      children: [
        Positioned.fill(
          child: Scrollbar(
            controller: _scroll,
            child: SingleChildScrollView(
              controller: _scroll,
              padding: const EdgeInsets.fromLTRB(24, 24, 24, 64),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 760),
                  child: LayoutBuilder(
                    builder: (context, constraints) => Column(
                      key: const ValueKey('transcript-column'),
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (var i = 0; i < widget.messages.length; i++)
                          Padding(
                            key: ValueKey(widget.messages[i]['id']),
                            padding: EdgeInsets.only(
                              bottom: i == widget.messages.length - 1 ? 0 : 28,
                            ),
                            child: _isUser(widget.messages[i])
                                ? _user(
                                    context,
                                    widget.messages[i],
                                    constraints.maxWidth,
                                  )
                                : _assistant(
                                    context,
                                    widget.messages[i],
                                    constraints.maxWidth,
                                    i == lastAssistant ? _retryFor(i) : null,
                                  ),
                          ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
        if (_far)
          Positioned(
            bottom: 12,
            left: 0,
            right: 0,
            child: Center(
              child: UiKitIconButton(
                icon: 'arrow-down',
                label: ChatCopy.jumpToLatest,
                style: UiKitIconButtonStyle.floating,
                size: 36,
                iconSize: 17,
                onPressed: () {
                  setState(() {
                    _following = true;
                    _far = false;
                  });
                  _follow();
                },
              ),
            ),
          ),
      ],
    );
  }

  // Retry resends the user message this answer replied to.
  VoidCallback? _retryFor(int index) {
    final retry = widget.onRetry;
    if (retry == null || index == 0) return null;
    final previous = widget.messages[index - 1];
    if (!_isUser(previous)) return null;
    final text = previous['text'] as String? ?? '';
    if (text.trim().isEmpty) return null;
    return () => retry(text);
  }

  Widget _user(BuildContext context, Map message, double width) {
    final c = UiColors.of(context);
    final tokens = UiTokens.of(context);
    final round = Radius.circular(tokens.surfaceRadius);
    return Align(
      alignment: Alignment.centerRight,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: width * 0.72),
        child: Container(
          key: ValueKey('${message['id']}/bubble'),
          padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 16),
          decoration: BoxDecoration(
            color: c.userBubble,
            borderRadius: BorderRadius.only(
              topLeft: round,
              topRight: round,
              bottomLeft: round,
              bottomRight: Radius.circular(tokens.radiusSmall * 0.75),
            ),
          ),
          // Plain text: what the person typed is never interpreted as markup.
          child: Text(
            message['text'] as String? ?? '',
            style: UiType.of(context).body.copyWith(color: c.userText),
          ),
        ),
      ),
    );
  }

  Widget _assistant(
    BuildContext context,
    Map message,
    double width,
    VoidCallback? retry,
  ) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final text = message['text'] as String? ?? '';
    final status = message['status'] as String? ?? 'completed';
    final app = message['app'];
    final model = message['model'] as String?;
    final activities = (message['activities'] as List? ?? const [])
        .whereType<Map>()
        .toList();
    final failed =
        status != 'running' &&
        status != 'completed' &&
        status != 'cancelled' &&
        status != 'interrupted';
    return Align(
      alignment: Alignment.centerLeft,
      // Figma `.assistant-message`: min(100%, 680px), so the toolbar spans it.
      child: SizedBox(
        width: width < 680 ? width : 680,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (activities.isNotEmpty)
              _activities(context, message, activities),
            if (text.isNotEmpty)
              DefaultTextStyle.merge(
                style: type.body.copyWith(color: c.body),
                child: widget.markdown(text),
              ),
            if (app is Map) ...[
              if (text.isNotEmpty) const SizedBox(height: 14),
              ChatAppCard(card: app, onOpen: widget.onOpenApp),
            ],
            if (status == 'running')
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  ChatCopy.responding,
                  style: type.body.copyWith(color: c.muted),
                ),
              ),
            if (status == 'cancelled' || status == 'interrupted')
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Text(
                  status == 'cancelled'
                      ? ChatCopy.stopped
                      : ChatCopy.interrupted,
                  style: type.body.copyWith(color: c.muted),
                ),
              ),
            if (failed)
              Padding(
                padding: const EdgeInsets.only(top: 12),
                child: Wrap(
                  spacing: 12,
                  runSpacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      ChatCopy.failed,
                      style: type.body.copyWith(color: c.error),
                    ),
                    if (retry != null)
                      UiKitButton(
                        label: ChatCopy.retry,
                        icon: 'refresh',
                        compact: true,
                        style: UiKitButtonStyle.secondary,
                        onPressed: retry,
                      ),
                  ],
                ),
              ),
            // A posted card (no model) has nothing to copy or retry.
            if (status == 'completed' &&
                (model != null || (text.isNotEmpty && app is! Map)))
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: ChatMessageToolbar(
                  text: text,
                  model: model,
                  onRetry: retry,
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _activities(BuildContext context, Map message, List<Map> items) {
    final c = UiColors.of(context);
    final type = UiType.of(context);
    final failed = items.where((item) => item['error'] != null).length;
    return Material(
      type: MaterialType.transparency,
      child: ExpansionTile(
        key: PageStorageKey('${message['id']}/activities'),
        tilePadding: EdgeInsets.zero,
        iconColor: c.muted,
        collapsedIconColor: c.muted,
        leading: Icon(
          failed > 0 ? Icons.error_outline : Icons.history,
          size: 18,
          color: failed > 0 ? c.error : c.muted,
        ),
        title: Text(
          '${items.length} ${items.length == 1 ? 'activity' : 'activities'}'
          '${failed > 0 ? ' · $failed failed' : ''}',
          style: type.caption.copyWith(color: c.muted),
        ),
        children: [
          for (final activity in items)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Material(
                type: MaterialType.transparency,
                child: ExpansionTile(
                  key: PageStorageKey('${message['id']}/${activity['id']}'),
                  tilePadding: const EdgeInsets.symmetric(horizontal: 8),
                  leading: Icon(
                    activity['error'] != null
                        ? Icons.error_outline
                        : activity['result'] != null
                        ? Icons.check_circle_outline
                        : Icons.hourglass_empty,
                    size: 18,
                    color: c.muted,
                  ),
                  title: Text(
                    '${activity['title'] ?? 'Activity'}',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: type.body.copyWith(color: c.body),
                  ),
                  children: [
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 260),
                      child: SingleChildScrollView(
                        key: PageStorageKey(
                          '${message['id']}/${activity['id']}/details',
                        ),
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Text('Input'),
                            SelectableText(
                              '${activity['input'] ?? ''}',
                              style: type.meta,
                            ),
                            if (activity['result'] != null) ...[
                              const SizedBox(height: 12),
                              const Text('Result'),
                              SelectableText(
                                '${activity['result']}',
                                style: type.meta,
                              ),
                            ],
                            // A failed step shows fixed copy, never the error text it carries.
                            if (activity['error'] != null) ...[
                              const SizedBox(height: 12),
                              Text(
                                ChatCopy.stepFailed,
                                style: type.meta.copyWith(color: c.error),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
