import 'package:flutter/widgets.dart';
import 'package:lucide_icons_flutter/lucide_icons.dart';

import '../controls.dart';
import '../tokens.dart';
import 'card_shell.dart';

enum JobState { running, done, cancelled, error }

enum StepStatus { waiting, running, done, failed }

class JobStep {
  JobStep(this.label, {this.detail, this.status = StepStatus.waiting});

  final String label;
  String? detail;
  StepStatus status;
}

class JobData {
  JobData({required this.title, required this.steps, this.progress = 0, this.resultText, this.errorText, this.elapsed});

  final String title;
  final List<JobStep> steps;
  double progress;

  /// Collapsed line when done, e.g. "Checked 16 opportunities, 14 slipped".
  String? resultText;
  String? errorText;
  String? elapsed;
}

/// Job progress: a task running in the background, step by step.
class JobCard extends StatelessWidget {
  const JobCard({
    super.key,
    required this.source,
    required this.data,
    required this.state,
    this.onCancel,
    this.onRetry,
  });

  final CardSource source;
  final JobData data;
  final JobState state;
  final VoidCallback? onCancel;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final d = data;
    switch (state) {
      case JobState.done:
        return CardResult(
          source: source,
          tone: ResultTone.done,
          text: d.resultText ?? d.title,
          detail: d.elapsed,
          expanded: _body(c),
        );
      case JobState.cancelled:
        return CardResult(source: source, tone: ResultTone.rejected, text: 'Stopped', detail: d.title);
      default:
        return _body(c);
    }
  }

  Widget _body(IcColors c) {
    final d = data;
    final running = state == JobState.running;
    return CardShell(
      source: source,
      status: running
          ? CardChip(
              label: 'Working',
              fg: c.textBody,
              bg: c.surfaceSunk,
              icon: WorkingSpinner(size: 12, color: c.textBody),
            )
          : null,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            d.title,
            style: IcType.text(15, weight: FontWeight.w600, color: c.textStrong),
          ),
          const SizedBox(height: 12),
          for (final s in d.steps) _StepRow(step: s),
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: BorderRadius.circular(IcRadius.pill),
            child: Container(
              height: 6,
              color: c.surfaceSunk,
              alignment: Alignment.centerLeft,
              child: AnimatedFractionallySizedBox(
                duration: IcMotion.slow,
                curve: IcMotion.ease,
                widthFactor: d.progress.clamp(0.03, 1.0),
                child: Container(color: state == JobState.error ? c.error : (running ? c.accent : c.done)),
              ),
            ),
          ),
          if (state == JobState.error && d.errorText != null) ...[
            const SizedBox(height: 12),
            CardError(text: d.errorText!, onRetry: onRetry),
          ],
          if (running && onCancel != null) ...[
            const SizedBox(height: 12),
            Align(
              alignment: Alignment.centerLeft,
              child: Btn(label: 'Stop', small: true, onTap: onCancel),
            ),
          ],
        ],
      ),
    );
  }
}

class _StepRow extends StatelessWidget {
  const _StepRow({required this.step});

  final JobStep step;

  @override
  Widget build(BuildContext context) {
    final c = IcTheme.of(context);
    final s = step;
    final icon = switch (s.status) {
      StepStatus.done => Container(
        width: 18,
        height: 18,
        decoration: BoxDecoration(color: c.doneBg, shape: BoxShape.circle),
        child: Icon(LucideIcons.check, size: 11, color: c.done),
      ),
      StepStatus.running => SizedBox(
        width: 18,
        height: 18,
        child: Center(child: WorkingSpinner(size: 15, color: c.textStrong)),
      ),
      StepStatus.failed => Container(
        width: 18,
        height: 18,
        decoration: BoxDecoration(color: c.errorBg, shape: BoxShape.circle),
        child: Icon(LucideIcons.x, size: 11, color: c.error),
      ),
      StepStatus.waiting => Container(
        width: 18,
        height: 18,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: c.borderInput, width: 1.5),
        ),
      ),
    };
    final color = switch (s.status) {
      StepStatus.waiting => c.textSubtle,
      StepStatus.failed => c.error,
      StepStatus.running => c.textStrong,
      StepStatus.done => c.textBody,
    };
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          icon,
          const SizedBox(width: 10),
          Expanded(
            child: Text.rich(
              TextSpan(
                children: [
                  TextSpan(
                    text: s.label,
                    style: IcType.text(
                      13,
                      weight: s.status == StepStatus.running ? FontWeight.w600 : FontWeight.w500,
                      color: color,
                    ),
                  ),
                  if (s.detail != null)
                    TextSpan(
                      text: '  ${s.detail}',
                      style: IcType.text(13, color: c.textMuted, tnum: true),
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
