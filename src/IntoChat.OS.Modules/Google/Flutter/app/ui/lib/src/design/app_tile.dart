import 'package:flutter/widgets.dart';
import 'package:flutter_svg/flutter_svg.dart';

import 'tokens.dart';

/// How an app icon is drawn.
enum AppTileStyle {
  /// The IntoChat IC mark (Chat).
  icMark,

  /// House style: ink tile, cream glyph, one blue detail.
  ink,

  /// House style: cream tile, ink glyph, one blue detail.
  cream,

  /// Third-party service: real brand mark on a white tile.
  brand,
}

/// Rounded-square app icon (radius 22% of size).
class AppTile extends StatelessWidget {
  const AppTile({
    super.key,
    required this.style,
    this.glyph,
    this.brandColor,
    this.size = 18,
  });

  final AppTileStyle style;
  final IconData? glyph;
  final Color? brandColor;
  final double size;

  static const _ink = Color(0xFF17140F);
  static const _cream = Color(0xFFF7F4EE);
  static const _blue = Color(0xFF1F5EFF);

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(size * 0.22);
    final darkEdge = IcTheme.of(context).isDark
        ? Border.all(color: const Color(0x40FBFAF7), width: 0.75)
        : null;
    if (style == AppTileStyle.icMark) {
      return Container(
        foregroundDecoration: BoxDecoration(
          borderRadius: radius,
          border: darkEdge,
        ),
        child: ClipRRect(
          borderRadius: radius,
          child: SvgPicture.asset(
            'assets/brand/ic-mark.svg',
            package: 'intochat_os_ui',
            width: size,
            height: size,
          ),
        ),
      );
    }
    final bg = switch (style) {
      AppTileStyle.ink => _ink,
      AppTileStyle.cream => _cream,
      _ => const Color(0xFFFFFFFF),
    };
    final fg = switch (style) {
      AppTileStyle.ink => _cream,
      AppTileStyle.cream => _ink,
      _ => brandColor ?? _blue,
    };
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: bg,
        borderRadius: radius,
        border: style == AppTileStyle.ink
            ? darkEdge
            : Border.all(color: const Color(0x1F17140F), width: 0.5),
      ),
      child: Stack(
        alignment: Alignment.center,
        children: [
          Icon(glyph, size: size * 0.58, color: fg),
          if (style != AppTileStyle.brand)
            Positioned(
              right: size * 0.14,
              bottom: size * 0.14,
              child: Container(
                width: size * 0.14,
                height: size * 0.14,
                decoration: const BoxDecoration(
                  color: _blue,
                  shape: BoxShape.circle,
                ),
              ),
            ),
        ],
      ),
    );
  }
}
