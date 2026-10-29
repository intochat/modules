import 'dart:convert';

import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

export 'package:intochat_os_ui/intochat_os_ui.dart' show shellIcon;

typedef IconLoader = Future<Uint8List?> Function(String path);

// An app's icon file, or its initial when it ships none or the file cannot be read.
class AppIcon extends StatefulWidget {
  const AppIcon({
    super.key,
    required this.title,
    this.path,
    this.load,
    this.size = 48,
  });

  final String title;
  final String? path;
  final IconLoader? load;
  final double size;

  @override
  State<AppIcon> createState() => _AppIconState();
}

class _AppIconState extends State<AppIcon> {
  Future<Uint8List?>? _bytes;

  @override
  void initState() {
    super.initState();
    _bytes = _fetch();
  }

  @override
  void didUpdateWidget(AppIcon oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.path != widget.path) _bytes = _fetch();
  }

  Future<Uint8List?>? _fetch() {
    final path = widget.path, load = widget.load;
    if (path == null || load == null) return null;
    return load(path).then<Uint8List?>((b) => b, onError: (Object _) => null);
  }

  static bool _isSvg(Uint8List bytes) => utf8
      .decode(bytes.take(256).toList(), allowMalformed: true)
      .trimLeft()
      .startsWith('<');

  Widget _fallback(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final initial = widget.title.trim().isEmpty
        ? '?'
        : widget.title.trim()[0].toUpperCase();
    return Container(
      key: const ValueKey('app-icon-fallback'),
      width: widget.size,
      height: widget.size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: scheme.primaryContainer,
        borderRadius: BorderRadius.circular(widget.size / 4),
      ),
      child: Text(
        initial,
        style: TextStyle(
          fontSize: widget.size / 2.2,
          fontWeight: FontWeight.w600,
          color: scheme.onPrimaryContainer,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final bytes = _bytes;
    if (bytes == null) return _fallback(context);
    return FutureBuilder<Uint8List?>(
      future: bytes,
      builder: (context, snapshot) {
        final data = snapshot.data;
        if (data == null || data.isEmpty) return _fallback(context);
        final size = widget.size;
        return ClipRRect(
          borderRadius: BorderRadius.circular(size / 4),
          child: _isSvg(data)
              ? SvgPicture.memory(data, width: size, height: size)
              : Image.memory(
                  data,
                  width: size,
                  height: size,
                  fit: BoxFit.cover,
                  errorBuilder: (context, _, _) => _fallback(context),
                ),
        );
      },
    );
  }
}
