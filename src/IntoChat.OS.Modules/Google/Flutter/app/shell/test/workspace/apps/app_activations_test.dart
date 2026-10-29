import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:intochat_os_flutter_shell/workspace/apps/app_activations.dart';
import 'package:intochat_os_flutter_shell/workspace/apps/apps_request.dart';
import 'package:intochat_os_ui/intochat_os_ui.dart';

typedef _Call = ({String method, String path, Object? body});

void main() {
  late List<_Call> calls;
  setUp(() => calls = []);

  AppsRequest answering(Object? Function(String method, String path) answer) =>
      (method, path, [body]) async {
        calls.add((method: method, path: path, body: body));
        return answer(method, path);
      };

  AppsRequest failing(int status, Map<String, Object?> body) =>
      (method, path, [request]) async {
        calls.add((method: method, path: path, body: request));
        throw UiRequestException(method, path, status, jsonEncode(body));
      };

  test('install_posts_the_request_and_opens_the_app', () async {
    final request = {
      'ref': {'publisher': 'os', 'name': 'notes', 'version': '1.0.0'},
      'consent': 'fixture-value-0001',
    };
    final result = await runAppActivation({
      'kind': 'install',
      'brainId': 'b 1',
      'title': 'Misleading',
      'request': request,
    }, answering((_, _) => {'status': 'ok', 'installedName': 'notes'}));

    expect(calls.single.method, 'POST');
    expect(calls.single.path, '/brains/b%201/apps/install');
    expect(calls.single.body, request);
    expect(result.toast, 'Installed notes.');
    expect(result.tone, UiTone.done);
    expect(result.open, 'notes');
  });

  test('conflict_shows_nothing', () async {
    final result = await runAppActivation({
      'kind': 'install',
      'brainId': 'b',
      'request': {'ref': {}},
    }, failing(409, {'status': 'needsConsent', 'requires': []}));
    expect(result.toast, isEmpty);
    expect(result.open, isNull);
  });

  test('consent_conflict_reopens_the_named_detail', () async {
    Future<AppActivationResult> run(Object? target) => runAppActivation({
      'kind': 'install',
      'brainId': 'b',
      'target': target,
      'request': {'ref': {}},
    }, failing(409, {'status': 'needsConsent', 'requires': []}));
    expect((await run('app:os/calculator')).reopen, 'app:os/calculator');
    expect((await run('installed:calc2')).reopen, 'installed:calc2');
    expect((await run('settings')).reopen, isNull);
    expect((await run(7)).reopen, isNull);
    final refused = await runAppActivation({
      'kind': 'install',
      'brainId': 'b',
      'target': 'app:os/calculator',
      'request': {'ref': {}},
    }, failing(409, {'status': 'refused'}));
    expect(refused.reopen, isNull);
  });

  test('forbidden_shows_server_copy', () async {
    final result = await runAppActivation(
      {
        'kind': 'install',
        'brainId': 'b',
        'request': {'ref': {}},
      },
      failing(403, {
        'status': 'refused',
        'error': 'Only you can install apps.',
      }),
    );
    expect(result.toast, 'Only you can install apps.');
    expect(result.tone, UiTone.error);

    final other = await runAppActivation({
      'kind': 'install',
      'brainId': 'b',
      'request': {'ref': {}},
    }, failing(500, {'error': 'System.Exception: boom at line 1'}));
    expect(other.toast, 'That app could not be installed.');
    expect(other.open, isNull);
  });

  test('update_and_remove_call_their_routes', () async {
    final updated = await runAppActivation(
      {
        'kind': 'update',
        'brainId': 'b',
        'name': 'notes',
        'title': 'Misleading',
        'consent': 'fixture-value-0002',
      },
      answering(
        (_, _) => {
          'name': 'notes',
          'manifest': {'title': 'Notes'},
        },
      ),
    );
    expect(calls.last.method, 'POST');
    expect(calls.last.path, '/brains/b/apps/notes/update');
    expect(calls.last.body, {'consent': 'fixture-value-0002'});
    expect(updated.toast, 'Updated Notes.');
    expect(updated.open, isNull);

    final unconfirmed = await runAppActivation({
      'kind': 'remove',
      'brainId': 'b',
      'name': 'notes',
    }, answering((_, _) => {'deleted': 'notes'}));
    expect(calls, hasLength(1));
    expect(unconfirmed.toast, isEmpty);

    final removed = await runAppActivation({
      'kind': 'remove',
      'brainId': 'b',
      'name': 'notes',
      'title': 'Misleading',
      'confirmed': true,
    }, answering((_, _) => {'deleted': 'notes'}));
    expect(calls.last.method, 'DELETE');
    expect(calls.last.path, '/brains/b/apps/notes');
    expect(removed.toast, 'Removed notes.');
  });

  test('install_without_a_name_says_installed', () async {
    final result = await runAppActivation({
      'kind': 'install',
      'brainId': 'b',
      'request': {'ref': {}},
    }, answering((_, _) => {'status': 'ok'}));
    expect(result.toast, 'Installed.');
    expect(result.tone, UiTone.done);
    expect(result.open, isNull);
  });

  test('update_and_remove_failures_have_their_own_copy', () async {
    final updated = await runAppActivation({
      'kind': 'update',
      'brainId': 'b',
      'name': 'notes',
    }, failing(500, {'error': 'boom'}));
    expect(updated.toast, 'That app could not be updated.');
    final removed = await runAppActivation({
      'kind': 'remove',
      'brainId': 'b',
      'name': 'notes',
      'confirmed': true,
    }, failing(500, {'error': 'boom'}));
    expect(removed.toast, 'That app could not be removed.');
    expect(removed.tone, UiTone.error);
    final malformed = await runAppActivation({
      'kind': 'install',
      'brainId': 7,
    }, answering((_, _) => null));
    expect(malformed.toast, 'That app could not be installed.');
  });

  test('configuration_view_opens_the_apps_detail', () {
    expect(
      configurationActivation({
        'brainId': 'b',
        'application': 'notes',
        'view': 'configuration',
      }),
      {
        'brainId': 'b',
        'kind': 'workspace',
        'application': 'apps',
        'operation': 'open',
        'input': 'installed:notes',
      },
    );
  });
}
