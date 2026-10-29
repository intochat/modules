import 'package:flutter/material.dart';
import 'package:web/web.dart' as web;

import '../../theme/ui_tokens.dart';

class EmbeddedBrowser extends StatelessWidget {
  const EmbeddedBrowser({
    super.key,
    this.uri = '',
    required this.onConnect,
    required this.onDisconnect,
    this.onUnavailable,
  });

  final String uri;
  final Future<void> Function(int, String)? onConnect;
  final Future<void> Function(String)? onDisconnect;
  final Future<void> Function(String)? onUnavailable;

  @override
  Widget build(BuildContext context) {
    final page = Uri.tryParse(uri);
    if (page == null ||
        !page.hasAuthority ||
        page.host.isEmpty ||
        (page.scheme != 'http' && page.scheme != 'https')) {
      return const Center(
        child: Text('Choose an HTTP or HTTPS page to display.'),
      );
    }
    // Cross-origin frames cannot report their document or CDP attachment state.
    // Leave network and framing errors visible in the browser's own frame.
    return ClipRRect(
      borderRadius: BorderRadius.circular(UiTokens.of(context).controlRadius),
      child: HtmlElementView.fromTagName(
        key: ValueKey(uri),
        tagName: 'iframe',
        onElementCreated: (element) {
          final frame = element as web.HTMLIFrameElement;
          frame
            ..title = 'Embedded page'
            ..src = uri
            ..style.border = '0'
            ..style.width = '100%'
            ..style.height = '100%';
        },
      ),
    );
  }
}
