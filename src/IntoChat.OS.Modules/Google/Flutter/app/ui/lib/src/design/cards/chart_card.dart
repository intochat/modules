import 'dart:math' as math;

import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../keys.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum ChartState { loading, ready, empty, error }

/// Columns compare amounts; a line shows a trend over time.
enum ChartKind { columns, line }

class ChartPoint {
  const ChartPoint(this.label, this.value);

  /// Short x label, e.g. "Oct".
  final String label;
  final double value;
}

class ChartData {
  const ChartData({
    required this.title,
    required this.points,
    required this.from,
    this.prefix = '',
    this.emptyText = 'Nothing to chart for this period.',
    this.errorText = 'Something stopped the lookup. Nothing changed.',
    this.compare,
    this.seriesName = '',
    this.compareName = '',
    this.sourceNeuronUri,
  });

  /// Names the series. With one series there is no legend box.
  final String title;
  final List<ChartPoint> points;

  /// The same measure for an earlier period (last year), one value per point.
  /// It draws in warm grey behind the accent series, with a legend.
  final List<double>? compare;

  /// Legend names when [compare] is set, e.g. "2026" and "2025".
  final String seriesName;
  final String compareName;

  /// Optional neuron:// URI backing this chart.
  final String? sourceNeuronUri;

  bool get compares => compare != null;

  /// "+12%" against the comparison, or null when there is nothing to compare.
  String? change(int i) {
    final b = compare?[i];
    if (b == null || b == 0) return null;
    final pct = ((points[i].value - b) / b * 100).round();
    return pct == 0 ? 'same' : '${pct > 0 ? '+' : '−'}${pct.abs()}%';
  }

  /// e.g. "Salesforce, closed won".
  final String from;

  /// Currency sign, e.g. "€".
  final String prefix;
  final String emptyText;
  final String errorText;

  /// Full value, e.g. "€186,000".
  String full(double v) {
    final s = v.round().toString().replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ',');
    return '$prefix$s';
  }

  /// Compact value for axis ticks and labels, e.g. "€420K", "€1.2M".
  String compact(double v) {
    if (v >= 1e6) return '$prefix${(v / 1e6).toStringAsFixed(v % 1e6 == 0 ? 0 : 1)}M';
    if (v >= 1e3) return '$prefix${(v / 1e3).round()}K';
    return '$prefix${v.round()}';
  }
}

/// Chart: one series in the accent colour; the title names it, so there is no
/// legend. Comparing with an earlier period adds that series in warm grey
/// (dashed as a line), a legend, and both values plus the change in the
/// tooltip and the table. Only the highest and the latest value carry a label;
/// hover any mark for its value, or switch to the table. Never two y-axes.
class ChartCard extends StatefulWidget {
  const ChartCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.kind = ChartKind.columns,
    this.onOpen,
    this.onRetry,
    this.onSwitchChartKind,
    this.onCopyNeuronUri,
  });

  final CardSource source;
  final ChartData data;
  final ChartState state;
  final ChartKind kind;
  final VoidCallback? onOpen;
  final VoidCallback? onRetry;
  final ValueChanged<ChartKind>? onSwitchChartKind;
  final ValueChanged<String>? onCopyNeuronUri;

  @override
  State<ChartCard> createState() => _ChartCardState();
}

class _ChartCardState extends State<ChartCard> {
  bool _table = false;
  int? _hover;
  late ChartKind _kind = widget.kind;

  static const _plotHeight = 180.0;
  static const _axisWidth = 56.0;

  @override
  void didUpdateWidget(covariant ChartCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.kind != widget.kind) {
      _kind = widget.kind;
    }
  }

  void _toggleKind() {
    final next = _kind == ChartKind.columns ? ChartKind.line : ChartKind.columns;
    setState(() => _kind = next);
    widget.onSwitchChartKind?.call(next);
  }

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = widget.data;
    final Widget body = switch (widget.state) {
      ChartState.loading => SizedBox(
        height: _plotHeight + 22,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            for (final h in const [0.35, 0.55, 0.45, 0.7, 0.5, 0.8])
              Expanded(
                child: Container(
                  height: _plotHeight * h,
                  margin: const EdgeInsets.symmetric(horizontal: 10),
                  decoration: BoxDecoration(
                    color: c.surfaceSunk,
                    borderRadius: const BorderRadius.vertical(top: Radius.circular(4)),
                  ),
                ),
              ),
          ],
        ),
      ),
      ChartState.empty => Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Row(
          children: [
            Icon(LucideIcons.chartNoAxesColumn, size: 16, color: c.textMuted),
            const SizedBox(width: 10),
            Expanded(
              child: Text(d.emptyText, style: IcType.text(14, color: c.textBody)),
            ),
          ],
        ),
      ),
      ChartState.error => Padding(
        padding: const EdgeInsets.symmetric(vertical: 14),
        child: Row(
          children: [
            Icon(LucideIcons.triangleAlert, size: 16, color: c.error),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                d.errorText,
                style: IcType.text(14, weight: FontWeight.w500, color: c.textStrong),
              ),
            ),
            Btn(label: 'Try again', small: true, onTap: widget.onRetry),
          ],
        ),
      ),
      // Keyboard: select the chart, then ← → move through its points.
      ChartState.ready =>
        _table
            ? _tableView(c, d)
            : Stop(
                onActivate: () => setState(() => _hover ??= d.points.length - 1),
                onArrow: (by) => setState(
                  () => _hover = ((_hover ?? (by > 0 ? -1 : d.points.length)) + by).clamp(0, d.points.length - 1),
                ),
                builder: (context, v) => SelectionRing(on: v.selected, radius: IcRadius.xs, child: _plot(c, d)),
              ),
    };

    return CardShell(
      source: widget.source,
      status: widget.state == ChartState.loading
          ? CardChip(label: 'Looking up', fg: c.working, bg: c.workingBg, icon: const WorkingSpinner(size: 12))
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            d.title,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
          ),
          const SizedBox(height: 2),
          Text(d.from, style: IcType.text(12.5, color: c.textMuted)),
          if (d.compares && widget.state == ChartState.ready) ...[
            const SizedBox(height: 10),
            Row(
              children: [
                _Swatch(color: c.accent, line: _kind == ChartKind.line),
                const SizedBox(width: 6),
                Text(
                  d.seriesName,
                  style: IcType.text(12.5, weight: FontWeight.w600, color: c.textStrong, tnum: true),
                ),
                const SizedBox(width: 16),
                _Swatch(color: c.chartContext, line: _kind == ChartKind.line, dashed: true),
                const SizedBox(width: 6),
                Text(d.compareName, style: IcType.text(12.5, color: c.textBody, tnum: true)),
              ],
            ),
          ],
          const SizedBox(height: 14),
          body,
          if (widget.state == ChartState.ready) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Stop(
                  onActivate: () => setState(() => _table = !_table),
                  builder: (context, v) => SelectionRing(
                    on: v.selected,
                    radius: IcRadius.xs,
                    child: MouseRegion(
                      cursor: SystemMouseCursors.click,
                      child: GestureDetector(
                        onTap: () => setState(() => _table = !_table),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                          child: Row(
                            children: [
                              Icon(_table ? LucideIcons.chartColumn : LucideIcons.table, size: 14, color: c.accent),
                              const SizedBox(width: 6),
                              Text(
                                _table ? 'Show as chart' : 'Show as table',
                                style: IcType.text(13, weight: FontWeight.w600, color: c.accent),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
                if (!_table && widget.onSwitchChartKind != null) ...[
                  const SizedBox(width: 10),
                  Btn(
                    label: _kind == ChartKind.columns ? 'Line' : 'Columns',
                    icon: _kind == ChartKind.columns ? LucideIcons.chartLine : LucideIcons.chartColumn,
                    small: true,
                    onTap: _toggleKind,
                  ),
                ],
                const Spacer(),
                if (d.sourceNeuronUri != null) ...[
                  Btn(
                    label: 'Copy URI',
                    icon: LucideIcons.copy,
                    small: true,
                    onTap: () {
                      final uri = d.sourceNeuronUri!;
                      Clipboard.setData(ClipboardData(text: uri));
                      widget.onCopyNeuronUri?.call(uri);
                    },
                  ),
                  const SizedBox(width: 8),
                ],
                Btn(label: 'Open in Sheets', icon: LucideIcons.grid3x3, small: true, onTap: widget.onOpen),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _tableView(IcColors c, ChartData d) => Container(
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(IcRadius.button),
      border: Border.all(color: c.borderHairline),
    ),
    child: Column(
      children: [
        if (d.compares)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            color: c.surfaceSunk,
            child: Row(
              children: [
                const Spacer(),
                for (final (w, t) in [(96.0, d.compareName), (12.0, ''), (96.0, d.seriesName), (64.0, 'Change')])
                  SizedBox(
                    width: w,
                    child: Text(
                      t,
                      textAlign: TextAlign.right,
                      style: IcType.text(11.5, weight: FontWeight.w600, color: c.textMuted, tnum: true),
                    ),
                  ),
              ],
            ),
          ),
        for (final (i, p) in d.points.indexed)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            decoration: BoxDecoration(
              border: i == 0 && !d.compares ? null : Border(top: BorderSide(color: c.borderHairline)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(p.label, style: IcType.text(13, color: c.textBody)),
                ),
                if (d.compares) ...[
                  SizedBox(
                    width: 96,
                    child: Text(
                      d.full(d.compare![i]),
                      textAlign: TextAlign.right,
                      style: IcType.text(13, color: c.textBody, tnum: true),
                    ),
                  ),
                  const SizedBox(width: 12),
                ],
                SizedBox(
                  width: 96,
                  child: Text(
                    d.full(p.value),
                    textAlign: TextAlign.right,
                    style: IcType.text(13, weight: FontWeight.w600, color: c.textStrong, tnum: true),
                  ),
                ),
                if (d.compares)
                  SizedBox(
                    width: 64,
                    child: Text(
                      d.change(i) ?? '',
                      textAlign: TextAlign.right,
                      style: IcType.text(13, color: c.textMuted, tnum: true),
                    ),
                  ),
              ],
            ),
          ),
      ],
    ),
  );

  Widget _plot(IcColors c, ChartData d) {
    final pts = d.points;
    final cmp = d.compare;
    final maxV = [...pts.map((p) => p.value), ...?cmp].reduce(math.max);
    final ticks = _ticks(maxV);
    final top = ticks.last;
    final axisStyle = IcType.text(11.5, color: c.textMuted, tnum: true);
    final labelStyle = IcType.text(12, weight: FontWeight.w600, color: c.textStrong, tnum: true);
    final mainMax = pts.map((p) => p.value).reduce(math.max);
    final maxI = pts.indexWhere((p) => p.value == mainMax);
    final lastI = pts.length - 1;

    return LayoutBuilder(
      builder: (context, box) {
        final plotW = box.maxWidth - _axisWidth;
        final slot = plotW / pts.length;
        double xOf(int i) => _axisWidth + slot * (i + 0.5);
        double yOf(double v) => 10 + (_plotHeight - 10) * (1 - v / top);

        int? indexAt(double x) {
          final i = ((x - _axisWidth) / slot).floor();
          return i >= 0 && i < pts.length ? i : null;
        }

        final hi = _hover;
        return SizedBox(
          height: _plotHeight + 22,
          child: MouseRegion(
            onHover: (e) => setState(() => _hover = indexAt(e.localPosition.dx)),
            onExit: (_) => setState(() => _hover = null),
            child: Stack(
              clipBehavior: Clip.none,
              children: [
                Positioned.fill(
                  child: CustomPaint(
                    painter: _ChartPainter(
                      kind: _kind,
                      values: [for (final p in pts) p.value],
                      compare: cmp,
                      context: c.chartContext,
                      top: top,
                      ticks: ticks,
                      axisWidth: _axisWidth,
                      plotHeight: _plotHeight,
                      accent: c.accent,
                      accentHover: c.accentHover,
                      grid: c.borderHairline,
                      surface: c.surfaceCard,
                      hover: hi,
                    ),
                  ),
                ),
                // Y ticks.
                for (final t in ticks)
                  Positioned(
                    left: 0,
                    width: _axisWidth - 8,
                    top: yOf(t) - 8,
                    child: Text(
                      d.compact(t),
                      textAlign: TextAlign.right,
                      maxLines: 1,
                      softWrap: false,
                      style: axisStyle,
                    ),
                  ),
                // X labels.
                for (final (i, p) in pts.indexed)
                  Positioned(
                    left: xOf(i) - slot / 2,
                    width: slot,
                    top: _plotHeight + 6,
                    child: Text(p.label, textAlign: TextAlign.center, style: axisStyle),
                  ),
                for (final i in {maxI, lastI})
                  if (hi != i)
                    if (cmp != null && _kind == ChartKind.line && i == lastI)
                      Positioned(
                        left: xOf(i) - 132,
                        width: 140,
                        top: yOf(pts[i].value) + (pts[i].value >= cmp[i] ? -22 : 8),
                        child: Text(
                          '${d.seriesName}  ${d.compact(pts[i].value)}',
                          textAlign: TextAlign.right,
                          style: labelStyle,
                        ),
                      )
                    else
                      Positioned(
                        left: xOf(i) - 40,
                        width: 80,
                        top: yOf(pts[i].value) - 22,
                        child: Text(d.compact(pts[i].value), textAlign: TextAlign.center, style: labelStyle),
                      ),
                if (cmp != null && _kind == ChartKind.line && hi != lastI)
                  Positioned(
                    left: xOf(lastI) - 132,
                    width: 140,
                    top: yOf(cmp[lastI]) + (cmp[lastI] > pts[lastI].value ? -22 : 8),
                    child: Text(
                      d.compareName,
                      textAlign: TextAlign.right,
                      style: IcType.text(11.5, weight: FontWeight.w500, color: c.textMuted, tnum: true),
                    ),
                  ),
                // Tooltip.
                if (hi != null)
                  Positioned(
                    left: (xOf(hi) - (cmp == null ? 70 : 110)).clamp(0.0, box.maxWidth - (cmp == null ? 140 : 220)),
                    width: cmp == null ? 140 : 220,
                    top: math.max(0, yOf(math.max(pts[hi].value, cmp?[hi] ?? 0)) - (cmp == null ? 54 : 92)),
                    child: IgnorePointer(
                      child: Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
                        decoration: BoxDecoration(
                          color: c.surfaceCard,
                          borderRadius: BorderRadius.circular(IcRadius.sm),
                          border: Border.all(color: c.borderInput),
                          boxShadow: IcShadow.sm(c),
                        ),
                        child: cmp == null
                            ? Column(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Text(pts[hi].label, style: IcType.text(11.5, color: c.textMuted)),
                                  Text(d.full(pts[hi].value), style: labelStyle),
                                ],
                              )
                            : Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Text(pts[hi].label, style: IcType.text(11.5, color: c.textMuted)),
                                  const SizedBox(height: 3),
                                  for (final (v, name, color) in [
                                    (pts[hi].value, d.seriesName, c.accent),
                                    (cmp[hi], d.compareName, c.chartContext),
                                  ])
                                    Row(
                                      children: [
                                        _Swatch(color: color),
                                        const SizedBox(width: 6),
                                        Expanded(
                                          child: Text(
                                            name,
                                            maxLines: 1,
                                            overflow: TextOverflow.ellipsis,
                                            style: IcType.text(12, color: c.textBody, tnum: true),
                                          ),
                                        ),
                                        const SizedBox(width: 10),
                                        Text(d.full(v), style: labelStyle),
                                      ],
                                    ),
                                  if (d.change(hi) case final ch?) ...[
                                    const SizedBox(height: 3),
                                    Text(
                                      ch == 'same' ? 'Same as ${d.compareName}' : '$ch on ${d.compareName}',
                                      style: IcType.text(11.5, color: c.textMuted, tnum: true),
                                    ),
                                  ],
                                ],
                              ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }

  /// Clean ticks from 0, 3 to 5 of them, the last at or above [maxV].
  static List<double> _ticks(double maxV) {
    if (maxV <= 0) return [0, 1];
    final raw = maxV / 3;
    final mag = math.pow(10, (math.log(raw) / math.ln10).floor()).toDouble();
    final step = [1.0, 2.0, 2.5, 5.0, 10.0].map((m) => m * mag).firstWhere((s) => s >= raw);
    final n = (maxV / step).ceil();
    return [for (var i = 0; i <= n; i++) step * i];
  }
}

class _ChartPainter extends CustomPainter {
  _ChartPainter({
    required this.kind,
    required this.values,
    this.compare,
    required this.context,
    required this.top,
    required this.ticks,
    required this.axisWidth,
    required this.plotHeight,
    required this.accent,
    required this.accentHover,
    required this.grid,
    required this.surface,
    required this.hover,
  });

  final ChartKind kind;
  final List<double> values;
  final List<double>? compare;
  final Color context;
  final double top;
  final List<double> ticks;
  final double axisWidth;
  final double plotHeight;
  final Color accent;
  final Color accentHover;
  final Color grid;
  final Color surface;
  final int? hover;

  @override
  void paint(Canvas canvas, Size size) {
    final plotW = size.width - axisWidth;
    final slot = plotW / values.length;
    double xOf(int i) => axisWidth + slot * (i + 0.5);
    double yOf(double v) => 10 + (plotHeight - 10) * (1 - v / top);

    // Hairline grid at the ticks; the baseline is the 0 tick.
    final gridPaint = Paint()
      ..color = grid
      ..strokeWidth = 1;
    for (final t in ticks) {
      final y = yOf(t).roundToDouble() + 0.5;
      canvas.drawLine(Offset(axisWidth, y), Offset(size.width, y), gridPaint);
    }

    final cmp = compare;
    void column(double left, double w, double v, Color color) {
      final rect = Rect.fromLTRB(left, yOf(v), left + w, yOf(0));
      canvas.drawRRect(
        RRect.fromRectAndCorners(rect, topLeft: const Radius.circular(4), topRight: const Radius.circular(4)),
        Paint()..color = color,
      );
    }

    if (kind == ChartKind.columns) {
      if (cmp != null) {
        // Pairs: last year left in grey, this year right in the accent, 2px apart.
        final w = math.min(16.0, (slot * 0.6 - 2) / 2);
        for (var i = 0; i < values.length; i++) {
          final x = xOf(i);
          column(x - w - 1, w, cmp[i], context);
          column(x + 1, w, values[i], hover == i ? accentHover : accent);
        }
        return;
      }
      // Thin columns, 24px at most, 4px rounded top, square on the baseline.
      final w = math.min(24.0, slot - 2 - slot * 0.4);
      for (var i = 0; i < values.length; i++) {
        final x = xOf(i);
        column(x - w / 2, w, values[i], hover == i ? accentHover : accent);
      }
      return;
    }

    // The comparison line first, dashed, so the accent line stays on top.
    if (cmp != null) {
      final dash = Paint()
        ..color = context
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..strokeCap = StrokeCap.round;
      for (var i = 0; i + 1 < cmp.length; i++) {
        final a = Offset(xOf(i), yOf(cmp[i]));
        final b = Offset(xOf(i + 1), yOf(cmp[i + 1]));
        final len = (b - a).distance;
        for (double t = 0; t < len; t += 9) {
          canvas.drawLine(a + (b - a) * (t / len), a + (b - a) * (math.min(t + 4, len) / len), dash);
        }
      }
    }

    // Line: 2px, round joins, a 10% wash under it, end dot with a surface ring.
    final path = Path();
    final area = Path()..moveTo(xOf(0), yOf(0));
    for (var i = 0; i < values.length; i++) {
      final p = Offset(xOf(i), yOf(values[i]));
      i == 0 ? path.moveTo(p.dx, p.dy) : path.lineTo(p.dx, p.dy);
      area.lineTo(p.dx, p.dy);
    }
    area
      ..lineTo(xOf(values.length - 1), yOf(0))
      ..close();
    if (cmp == null) canvas.drawPath(area, Paint()..color = accent.withValues(alpha: 0.1));
    canvas.drawPath(
      path,
      Paint()
        ..color = accent
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..strokeJoin = StrokeJoin.round
        ..strokeCap = StrokeCap.round,
    );
    void dot(int i) {
      final p = Offset(xOf(i), yOf(values[i]));
      canvas.drawCircle(p, 6, Paint()..color = surface);
      canvas.drawCircle(p, 4, Paint()..color = accent);
    }

    void contextDot(int i) {
      final p = Offset(xOf(i), yOf(cmp![i]));
      canvas.drawCircle(p, 6, Paint()..color = surface);
      canvas.drawCircle(p, 4, Paint()..color = context);
    }

    if (hover != null) {
      final x = xOf(hover!).roundToDouble() + 0.5;
      canvas.drawLine(Offset(x, yOf(top)), Offset(x, yOf(0)), gridPaint..color = accent.withValues(alpha: 0.35));
      if (cmp != null) contextDot(hover!);
      dot(hover!);
    }
    if (cmp != null) contextDot(values.length - 1);
    dot(values.length - 1);
  }

  @override
  bool shouldRepaint(_ChartPainter old) =>
      old.hover != hover ||
      old.values != values ||
      old.compare != compare ||
      old.accent != accent ||
      old.context != context ||
      old.kind != kind;
}

/// Legend and tooltip key: a small square, or a short line (dashed for the
/// comparison) when the chart is a line.
class _Swatch extends StatelessWidget {
  const _Swatch({required this.color, this.line = false, this.dashed = false});

  final Color color;
  final bool line;
  final bool dashed;

  @override
  Widget build(BuildContext context) {
    if (!line) {
      return Container(
        width: 10,
        height: 10,
        decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(2)),
      );
    }
    return SizedBox(
      width: 16,
      height: 10,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          for (var i = 0; i < (dashed ? 2 : 1); i++)
            Container(
              width: dashed ? 6 : 16,
              height: 2,
              decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(1)),
            ),
        ],
      ),
    );
  }
}
