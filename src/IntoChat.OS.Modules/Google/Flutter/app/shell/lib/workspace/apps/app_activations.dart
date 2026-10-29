import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

import 'apps_request.dart';

/// What the shell shows after an app activation: a toast (empty for none) and, after an install, the app to open.
final class AppActivationResult {
  const AppActivationResult(this.toast, this.tone, [this.open, this.reopen]);
  final String toast;
  final UiTone tone;
  final String? open;

  /// After a consent conflict: the Apps detail to show again (its `open` input), so the panel shows what the release
  /// asks for now.
  final String? reopen;
}

// The Apps app's open inputs, as AppsNames.Parse accepts them.
final _target = RegExp(
  r'^(app:[a-z][a-z0-9_-]{0,63}/[a-z][a-z0-9_-]{0,63}(@\d{1,9}\.\d{1,9}\.\d{1,9})?|installed:[a-z][a-z0-9_-]{0,63})$',
);

const _failed = {
  'install': 'That app could not be installed.',
  'update': 'That app could not be updated.',
  'remove': 'That app could not be removed.',
};

/// Runs a `{kind: install|update|remove}` activation over the person's own session. The install request and the
/// update consent are passed through exactly as the Apps app sent them; nothing is computed here. A 409 asking
/// for consent shows nothing: the Apps app renders the requirements panel from its own signal. A remove runs only
/// once the Apps app has confirmed it. Toasts name what the server answered, never the action's own label.
Future<AppActivationResult> runAppActivation(
  Map<String, dynamic> action,
  AppsRequest request,
) async {
  final kind = action['kind'];
  final failed = AppActivationResult(
    _failed[kind] ?? _failed['install']!,
    UiTone.error,
  );
  try {
    final apps =
        '/brains/${Uri.encodeComponent(action['brainId'] as String)}/apps';
    final name = action['name'] is String ? action['name'] as String : '';
    final app = '$apps/${Uri.encodeComponent(name)}';
    switch (kind) {
      case 'install':
        final answer = await request(
          'POST',
          '$apps/install',
          Map<String, dynamic>.from(action['request'] as Map),
        );
        final installed = answer is Map ? answer['installedName'] : null;
        return installed is String
            ? AppActivationResult(
                'Installed $installed.',
                UiTone.done,
                installed,
              )
            : const AppActivationResult('Installed.', UiTone.done);
      case 'update':
        if (name.isEmpty) return failed;
        final answer = await request('POST', '$app/update', {
          if (action['consent'] case final String consent) 'consent': consent,
        });
        final manifest = answer is Map ? answer['manifest'] : null;
        final title = manifest is Map && manifest['title'] is String
            ? manifest['title'] as String
            : name;
        return AppActivationResult('Updated $title.', UiTone.done);
      case 'remove':
        if (action['confirmed'] != true) {
          return const AppActivationResult('', UiTone.neutral);
        }
        if (name.isEmpty) return failed;
        final answer = await request('DELETE', app);
        final deleted = answer is Map && answer['deleted'] is String
            ? answer['deleted'] as String
            : name;
        return AppActivationResult('Removed $deleted.', UiTone.done);
    }
  } on UiRequestException catch (error) {
    if (error.statusCode == 409 && error.status == 'needsConsent') {
      final target = action['target'];
      return AppActivationResult(
        '',
        UiTone.neutral,
        null,
        target is String && _target.hasMatch(target) ? target : null,
      );
    }
    // Only a refusal's fixed server copy is shown; other failures never surface their text.
    if (error.statusCode == 403 && error.error != null) {
      return AppActivationResult(error.error!, UiTone.error);
    }
  } catch (_) {
    // Fixed copy below.
  }
  return failed;
}

/// An app's configuration view is the Apps app's detail for that installed app.
Map<String, dynamic> configurationActivation(Map<String, dynamic> action) => {
  if (action['brainId'] case final String brain) 'brainId': brain,
  'kind': 'workspace',
  'application': 'apps',
  'operation': 'open',
  'input': 'installed:${action['application']}',
};
