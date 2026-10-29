import 'package:flutter/material.dart';

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
  Widget build(BuildContext context) =>
      const Center(child: Text('Browsing requires the Windows desktop app.'));
}
