import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import 'tokens.dart';

enum AppTileStyle { glass, brand, outline }

/// Metadata for an IntoChat OS app icon badge (squircle gradient + vector glyph).
@immutable
class IcAppIconSpec {
  const IcAppIconSpec({
    required this.appId,
    required this.title,
    required this.category,
    required this.gradientTop,
    required this.gradientBottom,
    required this.icon,
    this.glyphColor = const Color(0xFFFFFFFF),
    this.useInoStar = false,
  });

  final String appId;
  final String title;
  final String category;
  final Color gradientTop;
  final Color gradientBottom;
  final IconData icon;
  final Color glyphColor;
  final bool useInoStar;

  String get accentHex =>
      '#${gradientTop.toARGB32().toRadixString(16).padLeft(8, '0').substring(2).toUpperCase()}';
}

/// First-class squircle app icon badge (`IcAppIcon`) with a two-stop gradient
/// squircle (corner radius 22% of size), 1px specular border, and custom/Lucide
/// vector glyph.
///
/// Ported from `E:\intochat\design\apps\os\lib\apps\app_info.dart` & `registry.dart`.
class IcAppIcon extends StatelessWidget {
  const IcAppIcon({
    super.key,
    required this.appId,
    this.icon,
    this.accentHex,
    this.size = 32,
    this.gradientTop,
    this.gradientBottom,
    this.glyphColor,
  });

  final String appId;
  final IconData? icon;
  final String? accentHex;
  final double size;
  final Color? gradientTop;
  final Color? gradientBottom;
  final Color? glyphColor;

  static const Map<String, IcAppIconSpec> builtInSpecs = {
    'ino': IcAppIconSpec(
      appId: 'ino',
      title: 'Ino',
      category: 'Intelligence',
      gradientTop: Color(0xFF3D7EFF),
      gradientBottom: Color(0xFF1948C8),
      icon: LucideIcons.sparkles,
      useInoStar: true,
    ),
    'files': IcAppIconSpec(
      appId: 'files',
      title: 'Files',
      category: 'Workspace',
      gradientTop: Color(0xFFF6B73C),
      gradientBottom: Color(0xFFC97B10),
      icon: LucideIcons.folder,
    ),
    'creator': IcAppIconSpec(
      appId: 'creator',
      title: 'Creator',
      category: 'Studio',
      gradientTop: Color(0xFFFF5D8F),
      gradientBottom: Color(0xFFB91C68),
      icon: LucideIcons.wandSparkles,
    ),
    'gallery': IcAppIconSpec(
      appId: 'gallery',
      title: 'UI Gallery',
      category: 'Studio',
      gradientTop: Color(0xFF22C55E),
      gradientBottom: Color(0xFF0D8A3E),
      icon: LucideIcons.palette,
    ),
    'post': IcAppIconSpec(
      appId: 'post',
      title: 'Post',
      category: 'Reach',
      gradientTop: Color(0xFFFB7185),
      gradientBottom: Color(0xFFBE123C),
      icon: LucideIcons.megaphone,
    ),
    'settings': IcAppIconSpec(
      appId: 'settings',
      title: 'Settings',
      category: 'System',
      gradientTop: Color(0xFF64748B),
      gradientBottom: Color(0xFF334155),
      icon: LucideIcons.slidersHorizontal,
    ),
    'circles': IcAppIconSpec(
      appId: 'circles',
      title: 'Circles',
      category: 'Social',
      gradientTop: Color(0xFFA855F7),
      gradientBottom: Color(0xFF6D28D9),
      icon: LucideIcons.circleDot,
    ),
    'calculator': IcAppIconSpec(
      appId: 'calculator',
      title: 'Calculator',
      category: 'Utilities',
      gradientTop: Color(0xFF14B8A6),
      gradientBottom: Color(0xFF0F766E),
      icon: LucideIcons.calculator,
    ),
    'datavisualizer': IcAppIconSpec(
      appId: 'datavisualizer',
      title: 'Data Visualizer',
      category: 'Analytics',
      gradientTop: Color(0xFF38BDF8),
      gradientBottom: Color(0xFF0284C7),
      icon: LucideIcons.chartSpline,
    ),
  };

  static String normalizeAppId(String raw) {
    final lower = raw.trim().toLowerCase();
    if (lower.contains('ino') || lower == 'assistant') return 'ino';
    if (lower.contains('file')) return 'files';
    if (lower.contains('creat')) return 'creator';
    if (lower.contains('galler')) return 'gallery';
    if (lower.contains('post') || lower.contains('publish')) return 'post';
    if (lower.contains('setting')) return 'settings';
    if (lower.contains('circle') ||
        lower.contains('space') ||
        lower.contains('hub') ||
        lower.contains('market') ||
        lower.contains('workspace')) {
      return 'circles';
    }
    if (lower.contains('calc')) return 'calculator';
    if (lower.contains('visual') || lower.contains('chart')) {
      return 'datavisualizer';
    }
    return lower;
  }

  static Color? tryParseHex(String? hex) {
    if (hex == null || hex.trim().isEmpty) return null;
    final clean = hex.trim().replaceAll('#', '');
    if (clean.length == 6) {
      final v = int.tryParse('FF$clean', radix: 16);
      if (v != null) return Color(v);
    } else if (clean.length == 8) {
      final v = int.tryParse(clean, radix: 16);
      if (v != null) return Color(v);
    }
    return null;
  }

  static IcAppIconSpec resolveSpec(
    String rawAppId, {
    IconData? iconOverride,
    String? accentHex,
    Color? topOverride,
    Color? bottomOverride,
  }) {
    final norm = normalizeAppId(rawAppId);
    final base = builtInSpecs[norm];
    final parsedAccent = tryParseHex(accentHex);

    if (base != null &&
        parsedAccent == null &&
        topOverride == null &&
        iconOverride == null) {
      return base;
    }

    final top =
        topOverride ??
        parsedAccent ??
        base?.gradientTop ??
        const Color(0xFF5B8CFF);
    final bottom =
        bottomOverride ??
        (parsedAccent != null
            ? Color.lerp(parsedAccent, const Color(0xFF000000), 0.36)!
            : (base?.gradientBottom ?? const Color(0xFF1D4ED8)));

    return IcAppIconSpec(
      appId: norm,
      title: base?.title ?? rawAppId,
      category: base?.category ?? 'App',
      gradientTop: top,
      gradientBottom: bottom,
      icon: iconOverride ?? base?.icon ?? LucideIcons.layoutGrid,
      useInoStar: base?.useInoStar == true && iconOverride == null,
    );
  }

  @override
  Widget build(BuildContext context) {
    final spec = resolveSpec(
      appId,
      iconOverride: icon,
      accentHex: accentHex,
      topOverride: gradientTop,
      bottomOverride: gradientBottom,
    );
    final r = size * 0.22;
    final fg = glyphColor ?? spec.glyphColor;

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(r),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [spec.gradientTop, spec.gradientBottom],
        ),
        border: Border.all(
          color: const Color(0x38FFFFFF), // 22% white specular border
          width: 1,
        ),
        boxShadow: [
          BoxShadow(
            color: const Color(0x38000000),
            blurRadius: size * 0.28,
            offset: Offset(0, size * 0.08),
          ),
        ],
      ),
      alignment: Alignment.center,
      child: spec.useInoStar
          ? CustomPaint(
              size: Size.square(size * 0.56),
              painter: _InoStarPainter(color: fg),
            )
          : Icon(spec.icon, size: size * 0.52, color: fg),
    );
  }
}

class _InoStarPainter extends CustomPainter {
  const _InoStarPainter({required this.color});
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final cx = size.width / 2;
    final cy = size.height / 2;
    final r = size.width / 2;
    final inner = r * 0.26;
    final path = Path();
    for (int i = 0; i < 8; i++) {
      final a = i * math.pi / 4 - math.pi / 2;
      final rad = i.isEven ? r : inner;
      final x = cx + math.cos(a) * rad;
      final y = cy + math.sin(a) * rad;
      if (i == 0) {
        path.moveTo(x, y);
      } else {
        path.lineTo(x, y);
      }
    }
    path.close();
    canvas.drawPath(path, Paint()..color = color);
    canvas.drawCircle(
      Offset(cx + r * 0.58, cy - r * 0.58),
      r * 0.13,
      Paint()..color = color.withValues(alpha: 0.85),
    );
  }

  @override
  bool shouldRepaint(covariant _InoStarPainter old) => old.color != color;
}

/// Shared app icon tile used in the desktop launcher, dock, and gallery.
class AppTile extends StatelessWidget {
  const AppTile({
    super.key,
    this.icon,
    this.accent,
    this.size = 44,
    this.appId,
    this.accentHex,
    this.style = AppTileStyle.brand,
    this.brandColor,
    this.glyph,
  });

  final IconData? icon;
  final Color? accent;
  final double size;
  final String? appId;
  final String? accentHex;
  final AppTileStyle style;
  final Color? brandColor;
  final Widget? glyph;

  @override
  Widget build(BuildContext context) {
    if (appId != null) {
      return IcAppIcon(
        appId: appId!,
        icon: icon,
        accentHex: accentHex,
        size: size,
        gradientTop: brandColor ?? accent,
      );
    }
    final c = IcTheme.of(context);
    final effectiveAccent = brandColor ?? accent ?? c.accent;
    final r = size * 0.22;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(r),
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: style == AppTileStyle.brand
              ? [
                  effectiveAccent,
                  Color.lerp(effectiveAccent, const Color(0xFF000000), 0.36)!,
                ]
              : [
                  effectiveAccent.withValues(alpha: c.isDark ? 0.28 : 0.18),
                  effectiveAccent.withValues(alpha: c.isDark ? 0.10 : 0.06),
                ],
        ),
        border: Border.all(
          color: style == AppTileStyle.brand
              ? const Color(0x38FFFFFF)
              : effectiveAccent.withValues(alpha: 0.45),
        ),
        boxShadow: [
          BoxShadow(
            color: effectiveAccent.withValues(alpha: c.isDark ? 0.22 : 0.12),
            blurRadius: size * 0.28,
            offset: Offset(0, size * 0.08),
          ),
        ],
      ),
      alignment: Alignment.center,
      child:
          glyph ??
          Icon(
            icon ?? LucideIcons.layoutGrid,
            size: size * 0.50,
            color: style == AppTileStyle.brand
                ? const Color(0xFFFFFFFF)
                : effectiveAccent,
          ),
    );
  }
}
