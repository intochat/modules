import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../models/ui_part.dart';
import '../../theme/ui_theme.dart';
import '../../theme/ui_tokens.dart';
import 'embedded_browser_stub.dart'
    if (dart.library.io) 'embedded_browser_windows.dart'
    if (dart.library.js_interop) 'embedded_browser_web.dart';

final class UiWebBrowser extends StatefulWidget {
  const UiWebBrowser({
    super.key,
    required this.part,
    this.height = 160,
    this.onConnect,
    this.onDisconnect,
    this.onUnavailable,
    this.connection = '',
    this.error = '',
    this.actualUrl = '',
  });

  final UiWebBrowserPart part;
  final double? height;
  final Future<void> Function(int, String)? onConnect;
  final Future<void> Function(String)? onDisconnect;
  final Future<void> Function(String)? onUnavailable;
  final String connection;
  final String error;
  final String actualUrl;

  @override
  State<UiWebBrowser> createState() => _UiWebBrowserState();
}

class _UiWebBrowserState extends State<UiWebBrowser> {
  int _session = 0;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final tokens = UiTokens.of(context);
    final connection = widget.connection.toLowerCase();
    final hasError = widget.error.isNotEmpty;
    final url = !kIsWeb && widget.actualUrl.isNotEmpty
        ? widget.actualUrl
        : widget.part.uri;
    final placeholder = kIsWeb
        ? null
        : hasError
        ? widget.error
        : connection == 'waiting'
        ? 'Waiting for the page...'
        : null;

    return Semantics(
      label: 'browser ${widget.part.title}',
      child: DecoratedBox(
        key: Key('ui_webbrowser_${widget.part.name}'),
        decoration: BoxDecoration(
          color: scheme.surfaceContainerLow,
          borderRadius: BorderRadius.circular(tokens.surfaceRadius),
          border: Border.all(color: scheme.outlineVariant),
        ),
        child: SizedBox(
          height: widget.height,
          child: Padding(
            padding: EdgeInsets.all(tokens.spaceSmall),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        widget.part.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontFamily: UiType.bodyFamily,
                          fontWeight: FontWeight.w600,
                          color: scheme.onSurface,
                        ),
                      ),
                    ),
                    if (kIsWeb)
                      Tooltip(
                        message: 'Interact with this page here. Browser automation requires the desktop app.',
                        child: Padding(
                          padding: EdgeInsets.all(tokens.spaceSmall),
                          child: Icon(
                            Icons.info_outline,
                            size: 16,
                            color: scheme.onSurfaceVariant,
                          ),
                        ),
                      ),
                    if (widget.onConnect != null || kIsWeb)
                      IconButton(
                        tooltip: 'Reconnect browser',
                        onPressed: () => setState(() => _session++),
                        icon: const Icon(Icons.refresh),
                      ),
                  ],
                ),
                SizedBox(height: tokens.spaceSmall),
                Expanded(
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      EmbeddedBrowser(
                        // Reconnect replaces the native page and its marked connector session.
                        key: ValueKey(_session),
                        uri: url,
                        onConnect: widget.onConnect,
                        onDisconnect: widget.onDisconnect,
                        onUnavailable: widget.onUnavailable,
                      ),
                      if (placeholder != null)
                        ColoredBox(
                          color: scheme.surface,
                          child: Center(
                            child: Padding(
                              padding: EdgeInsets.all(tokens.spaceMedium),
                              child: SelectableText(
                                placeholder,
                                textAlign: TextAlign.center,
                                style: TextStyle(
                                  color: scheme.onSurfaceVariant,
                                ),
                              ),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
