import 'dart:async';
import 'dart:convert';
import 'dart:developer' as developer;
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import 'basic_credentials.dart';
import 'cookie_http_client.dart';
import 'session_http_client_io.dart'
    if (dart.library.html) 'session_http_client_web.dart'
    as transport;
import 'host_environment.dart';
import 'models/app_manifest.dart';
import 'models/installed_app.dart';
import 'models/cluster.dart';
import 'models/brain_models.dart';
import 'models/compute_usage.dart';
import 'models/model_catalog.dart';
import 'models/grant_summary.dart';
import 'neuron_channel.dart';
import 'neuron_watch_mux.dart';
import 'node_contracts.dart';
import 'scene_client.dart';
import 'models/session_capabilities.dart';
import 'models/table_models.dart';
import 'models/workspace_models.dart';

final class IntoChatOSUiClient {
  /// Gated on the kernel; 404 when the kernel runs ungated.
  static const authCheckPath = '/auth/check';

  IntoChatOSUiClient({
    required this.baseUri,
    http.Client? httpClient,
    BasicCredentials? credentials,
  }) : workspaceIdentity = credentials?.username ?? 'local-owner',
       _http = httpClient is CookieHttpClient
           ? httpClient
           : CookieHttpClient(
               httpClient ?? transport.createSessionHttpClient(),
               credentials: credentials,
             ),
       _ownsClient = httpClient == null,
       _credentials = credentials;

  factory IntoChatOSUiClient.fromEnvironment({
    http.Client? httpClient,
    Map<String, String>? processEnvironment,
    BasicCredentials? credentials,
  }) {
    return IntoChatOSUiClient(
      baseUri: IntoChatOSHostEnv.requireUiBaseUri(
        processEnvironment: processEnvironment,
      ),
      httpClient: httpClient,
      credentials: credentials,
    );
  }

  /// Probes the kernel's gated `/auth/check`.
  ///
  /// Returns true when the kernel accepts the attached credentials — including
  /// the ungated case, where the endpoint is absent and any request already
  /// succeeds. Returns false only on an explicit 401, so a wrong password is
  /// distinguishable from an unreachable kernel, which throws.
  Future<bool> checkAuth() async {
    final uri = baseUri.replace(path: authCheckPath);
    final streamed = await _http.send(http.Request('GET', uri));
    final response = await http.Response.fromStream(streamed);
    if (response.statusCode == 401) {
      return false;
    }
    if (response.statusCode == 204 || response.statusCode == 404) {
      return true;
    }
    throw StateError(
      'auth check failed: ${response.statusCode} ${response.body}',
    );
  }

  final Uri baseUri;

  /// Non-secret scope for local workspace preferences; never includes credentials.
  String workspaceIdentity;
  String? principalId;
  String? accountId;
  String? defaultBrainId;

  void _useSession(Map<String, dynamic> session) {
    principalId = session['principalId'] as String;
    accountId = session['accountId'] as String;
    defaultBrainId = session['brainId'] as String;
    workspaceIdentity = '$principalId|$accountId';
  }

  Future<void> readSession() async {
    final response = await _request(
      'GET',
      '/identity/session',
      timeout: const Duration(seconds: 10),
    );
    if (response.statusCode == 200 && response.body.isNotEmpty) {
      _useSession(jsonDecode(response.body) as Map<String, dynamic>);
    }
  }

  /// True while the client is on a lobby session: an anonymous visitor of a deployment that serves one
  /// (`GET /lobby/session`). The lobby brain is the default brain and its app renders on its own root, so the
  /// shell shows no chrome around it. The flag is generic: any session may carry it.
  bool chromeless = false;

  /// Set by the shell while a lobby session is open; [endLobby] calls it.
  Future<void> Function()? onLobbyEnded;

  /// Opens the lobby session when the deployment serves one: `GET /lobby/session` answers the lobby brain id
  /// (marked `chromeless: true`). Returns false for any other answer, so a host without a lobby
  /// keeps its sign-in behaviour. The visitor's lobby cookie is held by the HTTP client.
  Future<bool> readLobbySession() async {
    try {
      final response = await _http
          .get(
            baseUri.replace(
              path: '/lobby/session',
              query: null,
              fragment: null,
            ),
          )
          .timeout(const Duration(seconds: 5));
      if (response.statusCode != 200 || response.body.isEmpty) return false;
      final body = jsonDecode(response.body);
      final brain = body is Map<String, dynamic> ? body['brainId'] : null;
      // Only an explicit chrome-less descriptor is a lobby: a host that answers this path with a member session
      // (or anything else) is not offering one.
      if (brain is! String ||
          brain.isEmpty ||
          (body as Map<String, dynamic>)['chromeless'] != true) {
        return false;
      }
      defaultBrainId = brain;
      chromeless = true;
      return true;
    } on Object {
      return false;
    }
  }

  /// The lobby neuron raised `LobbyEnded` (the visitor signed in and the host wrote a member session): the
  /// shell drops the lobby connection and reads `/identity/session` again. Nothing of the lobby session is reused.
  Future<void> endLobby() async {
    final ended = onLobbyEnded;
    if (!chromeless || ended == null) return;
    await ended();
  }

  Future<void> signIn(
    String username,
    String password, {
    bool register = false,
  }) async {
    final response = await _request(
      'POST',
      register ? '/identity/register' : '/identity/login',
      body: {'principalId': username, 'password': password},
      timeout: const Duration(seconds: 15),
    );
    _useSession(jsonDecode(response.body) as Map<String, dynamic>);
  }

  /// Creates an owned workspace; clients never choose or claim an existing id.
  Future<String> createWorkspace() async {
    final response = await _request(
      'POST',
      '/identity/brains',
      body: const {},
      timeout: const Duration(seconds: 15),
    );
    return (jsonDecode(response.body) as Map<String, dynamic>)['brainId']
        as String;
  }

  Future<void> signOut() async {
    await _request(
      'POST',
      '/identity/logout',
      timeout: const Duration(seconds: 10),
    );
  }

  Future<void> signInLocalAccount(String username, String password) async {
    if (!['localhost', '127.0.0.1', '::1', '[::1]'].contains(baseUri.host) ||
        !['http', 'https'].contains(baseUri.scheme)) {
      throw StateError('Local accounts require a loopback server.');
    }
    http.Response response;
    try {
      response = await _request(
        'POST',
        '/identity/login',
        body: {'principalId': username, 'password': password},
        timeout: const Duration(seconds: 15),
      );
    } on UiRequestException catch (error) {
      if (error.statusCode != 401) rethrow;
      response = await _request(
        'POST',
        '/identity/register',
        body: {'principalId': username, 'password': password},
        timeout: const Duration(seconds: 15),
      );
    }
    final Object? session;
    try {
      session = jsonDecode(response.body);
    } on FormatException {
      throw StateError('Local sign-in did not return a valid session.');
    }
    if (session is! Map<String, dynamic> ||
        session['principalId'] != username ||
        session['accountId'] is! String ||
        (session['accountId'] as String).isEmpty ||
        session['brainId'] is! String ||
        (session['brainId'] as String).isEmpty) {
      throw StateError('Local sign-in did not return the requested account.');
    }
    _useSession(session);
  }

  final CookieHttpClient _http;
  final bool _ownsClient;
  final BasicCredentials? _credentials;

  /// Live updates ride the neuron channel when the client owns its transport (a real kernel),
  /// unless the build is told to keep HTTP polling (`--dart-define=INTOCHATOS_UI_TRANSPORT=http`)
  /// so the comparison can run both. A test that injects its HTTP client keeps the HTTP path,
  /// or supplies a [link] of its own.
  static const liveTransportEnv = 'INTOCHATOS_UI_TRANSPORT';
  static const _liveTransport = String.fromEnvironment(
    liveTransportEnv,
    defaultValue: 'grpc',
  );
  bool get grpcLive {
    final live = _link != null || (_ownsClient && _liveTransport != 'http');
    if (!live && !_pollingAnnounced) {
      _pollingAnnounced = true;
      developer.log(
        'live updates stay on HTTP polling (transport=$_liveTransport, ownsClient=$_ownsClient)',
        name: 'intochatos.channel',
      );
    }
    return live;
  }

  bool _pollingAnnounced = false;

  NeuronLink? _link;
  set link(NeuronLink value) => _link = value;
  final _watches = <String, NeuronWatchMux>{};
  NeuronLink get channel => _link ??= NeuronChannel(
    baseUri,
    authorization: _credentials?.authorizationHeader,
    sessionHeaders: () => {
      if (_http.cookies.isNotEmpty)
        'cookie': _http.cookies.entries
            .map((entry) => '${entry.key}=${entry.value}')
            .join('; '),
    },
  );

  /// The brain's shared watch: every node watch of a screen rides one `Watch` stream.
  NeuronWatchMux watches(String brainId) =>
      _watches[brainId] ??= NeuronWatchMux(() => channel, brainId);

  /// The node's own change signal; the view reads the node once per event.
  Stream<NeuronSignal> watchNode(String brainId, String kind, String name) {
    final friendSlot =
        (kind == 'person' && name.endsWith('/profile')) ||
        (kind == 'collection' && name.endsWith('/posts')) ||
        (kind == 'card' && RegExp(r'^.+/posts/[A-Za-z0-9_-]+$').hasMatch(name));
    final contract = NodeContracts.of(friendSlot ? 'scene' : kind);
    final source = friendSlot ? '$brainId/space' : name;
    final signals = kind == 'confirmation'
        ? ['ConfirmationRequested', 'ConfirmationResolved']
        : [contract.changed];
    return watches(
      brainId,
    ).watch(WatchSourceSpec(contract.contract, source, signals)).map((signal) {
      // Delivery latency as the comparison measures it: envelope stamp to arrival.
      developer.log(
        'neuron.latency ms=${DateTime.now().millisecondsSinceEpoch - signal.serverTimeMs} type=${signal.type} source=${signal.source}',
        name: 'intochatos.channel',
      );
      return signal;
    });
  }

  /// Server-owned capabilities; the shell hides developer-only surfaces when this is off.
  Future<SessionCapabilities> readCapabilities() async {
    final response = await _request(
      'GET',
      '/session/capabilities',
      timeout: const Duration(seconds: 10),
    );
    return SessionCapabilities.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<List<TableSummary>> listTables(String workspace) async {
    final body = await _tableRequest(
      'GET',
      '/brains/${Uri.encodeComponent(workspace)}/ui/tables',
    );
    return (body as List)
        .map(
          (item) =>
              TableSummary.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList();
  }

  /// This brain's apps, with where each installed copy came from and whether an update is offered.
  Future<List<InstalledAppSummary>> listInstalledApps(String workspace) async {
    final body = await _tableRequest(
      'GET',
      '/brains/${Uri.encodeComponent(workspace)}/apps',
    );
    return (body as List)
        .map(
          (item) => InstalledAppSummary.fromJson(
            Map<String, dynamic>.from(item as Map),
          ),
        )
        .toList();
  }

  static String appRequestPath(String brainId, String appId) =>
      '/brains/${Uri.encodeComponent(brainId)}/apps/${Uri.encodeComponent(appId)}/request';

  /// The manifest an app declares, read from the same view its detail page uses.
  Future<AppManifest> readAppManifest(String brainId, String appId) async {
    final body = await jsonRequest(
      'GET',
      '/brains/${Uri.encodeComponent(brainId)}/apps/${Uri.encodeComponent(appId)}',
    );
    return AppManifest.fromJson(
      Map<String, dynamic>.from((body as Map)['manifest'] as Map),
    );
  }

  /// The route that publishes an open request; the app answers through neurons, never here.
  static String appOpenPath(String brainId, String appId) =>
      appRequestPath(brainId, appId);

  /// The brain-scoped key of one installed app; its neurons live under it.
  static String appKey(String brainId, String appId) => '$brainId/apps/$appId';

  /// Publishes a request to an app; the app answers through neurons, never through this call.
  Future<String> requestApp(
    String brainId,
    String appId,
    String operation,
    String input,
  ) async {
    final body = await jsonRequest('POST', appRequestPath(brainId, appId), {
      'operation': operation,
      'input': input,
    });
    return (body as Map)['requestId'] as String;
  }

  /// Raw bytes of a served file such as an app icon; null when the server has none.
  Future<Uint8List?> readBytes(String path) async {
    try {
      final response = await _request(
        'GET',
        path,
        timeout: const Duration(seconds: 30),
      );
      return response.bodyBytes;
    } on UiRequestException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<TableSnapshot> readTable(
    String workspace,
    String id, {
    int offset = 0,
    int limit = 50,
  }) async => TableSnapshot.fromJson(
    Map<String, dynamic>.from(
      await _tableRequest(
        'GET',
        '/brains/${Uri.encodeComponent(workspace)}/ui/tables/${Uri.encodeComponent(id)}?offset=$offset&limit=$limit',
      ) as Map,
    ),
  );

  Future<TableSnapshot> updateTableView(
    String workspace,
    String id,
    TableViewUpdate update,
  ) async => TableSnapshot.fromJson(
    Map<String, dynamic>.from(
      await _tableRequest(
        'POST',
        '/brains/${Uri.encodeComponent(workspace)}/ui/tables/${Uri.encodeComponent(id)}/view',
        body: update.toJson(),
      ) as Map,
    ),
  );

  Future<WorkspaceSnapshot> readWorkspace(
    String workspaceId, {
    Future<void>? cancelled,
  }) async => WorkspaceSnapshot.fromJson(
    Map<String, dynamic>.from(
      await _tableRequest(
        'GET',
        '/brains/${Uri.encodeComponent(workspaceId)}',
        cancelled: cancelled,
      ) as Map,
    ),
  );

  Future<WorkspaceSnapshot> showNeuron(
    String workspace,
    String key,
    String operationId,
    int expectedRevision,
  ) => _navigateNeuron(workspace, 'show', operationId, expectedRevision, key);
  Future<WorkspaceSnapshot> backNeuron(
    String workspace,
    String operationId,
    int expectedRevision,
  ) => _navigateNeuron(workspace, 'back', operationId, expectedRevision);
  Future<WorkspaceSnapshot> homeNeuron(
    String workspace,
    String operationId,
    int expectedRevision,
  ) => _navigateNeuron(workspace, 'home', operationId, expectedRevision);
  Future<WorkspaceSnapshot> closeNeuron(
    String workspace,
    String key,
    String operationId,
    int expectedRevision,
  ) => _navigateNeuron(workspace, 'close', operationId, expectedRevision, key);

  Future<WorkspaceSnapshot> _navigateNeuron(
    String workspace,
    String command,
    String operationId,
    int expectedRevision, [
    String? key,
  ]) async => WorkspaceSnapshot.fromJson(
    Map<String, dynamic>.from(
      await _tableRequest(
        'POST',
        '/brains/${Uri.encodeComponent(workspace)}/$command',
        body: {
          'app': ?key,
          'operationId': operationId,
          'expectedRevision': expectedRevision,
        },
      ) as Map,
    ),
  );

  Future<void> reportProblem({
    required String workspaceId,
    required String intentId,
    required String message,
  }) async {
    await _tableRequest(
      'POST',
      '/brains/${Uri.encodeComponent(workspaceId)}/reports',
      body: {'intentId': intentId, 'message': message},
    );
  }

  /// The live signals of `types` (simple type names, e.g. DeploymentChanged) on the brain's
  /// `/live` feed. Each source's retained value arrives first, so a late listener starts current.
  /// A reader that falls behind gets `resync`: the feed reconnects and replays, so listeners read again.
  /// The pause before reconnecting after a resync, doubling up to [maxResyncPause].
  Duration firstResyncPause = const Duration(seconds: 1);
  Duration maxResyncPause = const Duration(seconds: 30);

  Stream<LiveEnvelope> watchLive(String brainId, List<String> types) async* {
    final path =
        '/brains/${Uri.encodeComponent(brainId)}/live?${types.map((type) => 'type=${Uri.encodeQueryComponent(type)}').join('&')}';
    var resyncPause = firstResyncPause;
    while (true) {
      var resync = false;
      await for (final envelope in _serverEvents(
        path,
        'Live signals are unavailable.',
      ).map(LiveEnvelope.fromJson)) {
        if (envelope.isResync) {
          resync = true;
          break;
        }
        yield envelope;
      }
      // The server ended a stream this reader fell behind on: reconnecting replays current state.
      if (!resync) return;
      // A server that resyncs on every connect must not spin: wait 1 s, doubling to 30 s.
      await Future<void>.delayed(resyncPause);
      resyncPause = resyncPause * 2 > maxResyncPause
          ? maxResyncPause
          : resyncPause * 2;
    }
  }

  Stream<DeploymentChanged> watchDeployment(String brainId) =>
      watchLive(brainId, ['DeploymentChanged'])
          .where((envelope) => !envelope.isReady)
          .map((envelope) => DeploymentChanged.fromJson(envelope.payload));

  Stream<AppActivity> watchActivity(String brainId) =>
      watchLive(brainId, ['AppActivity'])
          .where((envelope) => !envelope.isReady)
          .map((envelope) => AppActivity.fromJson(envelope.payload));

  /// The brains the signed-in person is in, with their kind.
  Future<List<BrainEntry>> listBrains() async {
    final body = await _tableRequest('GET', '/identity/brains');
    return (body as List)
        .map(
          (item) => BrainEntry.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList();
  }

  /// R126: every circle brain the person is in, each watched on its own `/live` feed, merged. The circles are
  /// listed once per subscription; with none the stream ends, and a listener that resubscribes lists again.
  Stream<CircleActivity> watchCircleActivity() {
    late StreamController<CircleActivity> controller;
    final subscriptions = <StreamSubscription<CircleActivity>>[];
    controller = StreamController<CircleActivity>(
      onListen: () async {
        try {
          final circles = (await listBrains())
              .where((brain) => brain.isCircle)
              .map((brain) => brain.id)
              .toList();
          if (circles.isEmpty) {
            await controller.close();
            return;
          }
          var open = circles.length;
          for (final circle in circles) {
            subscriptions.add(
              watchLive(circle, ['CircleSpoke', 'CircleChanged'])
                  .map(
                    (envelope) => CircleActivity.fromEnvelope(circle, envelope),
                  )
                  .listen(
                    controller.add,
                    onError: controller.addError,
                    onDone: () {
                      if (--open == 0) controller.close();
                    },
                  ),
            );
          }
        } catch (error, stack) {
          controller.addError(error, stack);
          await controller.close();
        }
      },
      onCancel: () async {
        for (final subscription in subscriptions) {
          await subscription.cancel();
        }
      },
    );
    return controller.stream;
  }

  Stream<Map<String, dynamic>> _serverEvents(String path, String unavailable) {
    final abort = Completer<void>();
    StreamSubscription<String>? incoming;
    late StreamController<Map<String, dynamic>> controller;
    controller = StreamController<Map<String, dynamic>>(
      onListen: () async {
        try {
          final request = http.AbortableRequest(
            'GET',
            baseUri.resolve(path),
            abortTrigger: abort.future,
          )..headers['accept'] = 'text/event-stream';
          final response = await _http.send(request);
          if (abort.isCompleted || response.statusCode != 200) {
            await response.stream.listen(null).cancel();
            if (!abort.isCompleted) {
              throw TableRequestException(response.statusCode, unavailable);
            }
            return;
          }
          incoming = response.stream
              .transform(utf8.decoder)
              .transform(const LineSplitter())
              .listen(
                (line) {
                  if (!line.startsWith('data:')) return;
                  try {
                    controller.add(
                      jsonDecode(line.substring(5).trimLeft())
                          as Map<String, dynamic>,
                    );
                  } catch (error, stack) {
                    controller.addError(error, stack);
                  }
                },
                onError: (Object error, StackTrace stack) {
                  if (!abort.isCompleted) controller.addError(error, stack);
                },
                onDone: controller.close,
              );
        } catch (error, stack) {
          if (!abort.isCompleted) {
            controller.addError(error, stack);
            unawaited(controller.close());
          }
        }
      },
      onCancel: () async {
        if (!abort.isCompleted) abort.complete();
        await incoming?.cancel();
      },
    );
    return controller.stream;
  }

  Future<Object?> _tableRequest(
    String method,
    String path, {
    Map<String, Object?>? body,
    Duration timeout = const Duration(seconds: 40),
    Future<void>? cancelled,
  }) async {
    final abort = Completer<void>();
    final request = http.AbortableRequest(
      method,
      baseUri.resolve(path),
      abortTrigger: cancelled == null
          ? abort.future
          : Future.any([abort.future, cancelled]),
    );
    if (body != null) {
      request.headers['content-type'] = 'application/json';
      request.body = jsonEncode(body);
    }
    final response = await _http
        .send(request)
        .then(http.Response.fromStream)
        .timeout(
          timeout,
          onTimeout: () {
            abort.complete();
            throw TimeoutException('$method $path timed out.');
          },
        );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      var message = response.body;
      try {
        final error = jsonDecode(message);
        if (error is Map) {
          final detail = error['error'] ?? error['detail'];
          if (detail is String) message = detail;
        }
      } on FormatException {
        // Non-JSON proxy/server failures still carry their original text.
      }
      throw TableRequestException(response.statusCode, message);
    }
    return response.body.isEmpty ? null : jsonDecode(response.body);
  }

  /// The brain's neurons as snapshots: the whole brain with [brainId], or one chat's view with [chatName].
  Stream<BrainSnapshot> watchBrain({String? chatName, String? brainId}) {
    if ((chatName == null) == (brainId == null)) {
      throw ArgumentError('Name a chat or a brain, not both.');
    }
    final path = brainId != null
        ? '/brains/${Uri.encodeComponent(brainId)}/brain/events'
        : '/chats/${Uri.encodeComponent(chatName!)}/brain/events';
    final abort = Completer<void>();
    late StreamController<BrainSnapshot> controller;
    controller = StreamController<BrainSnapshot>(
      onListen: () async {
        try {
          final request = http.AbortableRequest(
            'GET',
            baseUri.replace(path: path),
            abortTrigger: abort.future,
          )..headers['accept'] = 'text/event-stream';
          final response = await _http.send(request);
          if (response.statusCode != 200) {
            throw StateError(
              'Brain stream unavailable (${response.statusCode}).',
            );
          }
          String? event;
          final data = <String>[];
          await for (final line
              in response.stream
                  .transform(utf8.decoder)
                  .transform(const LineSplitter())) {
            if (abort.isCompleted) break;
            if (line.startsWith('event:')) event = line.substring(6).trim();
            if (line.startsWith('data:')) {
              data.add(line.substring(5).trimLeft());
            }
            if (line.isEmpty) {
              if (event == 'brain-snapshot' && data.isNotEmpty) {
                controller.add(
                  BrainSnapshot.fromJson(
                    jsonDecode(data.join('\n')) as Map<String, dynamic>,
                  ),
                );
              }
              event = null;
              data.clear();
            }
          }
        } catch (error, stack) {
          if (!abort.isCompleted) controller.addError(error, stack);
        } finally {
          if (!controller.isClosed) await controller.close();
        }
      },
      onCancel: () {
        if (!abort.isCompleted) abort.complete();
      },
    );
    return controller.stream;
  }

  /// Spend and balance for one brain, grouped by [by]; rows are most expensive first.
  Future<ComputeSummary> readCompute(
    String brainId, {
    String by = 'app',
    DateTime? since,
  }) async {
    final query = Uri(
      queryParameters: {'by': by, 'since': ?since?.toUtc().toIso8601String()},
    ).query;
    return ComputeSummary.fromJson(
      Map<String, dynamic>.from(
        await _tableRequest(
          'GET',
          '/brains/${Uri.encodeComponent(brainId)}/compute?$query',
        ) as Map,
      ),
    );
  }

  /// The most recent individual costs for one brain, newest [take] first.
  Future<List<ComputeCost>> readComputeRecent(
    String brainId, {
    int take = 50,
  }) async {
    final body = await _tableRequest(
      'GET',
      '/brains/${Uri.encodeComponent(brainId)}/compute/recent?take=$take',
    );
    return [
      for (final item in body as List)
        ComputeCost.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  }

  Future<ComputeTracePage> readComputeTrace(
    String brainId,
    String traceId, {
    int take = 100,
    String? continuationToken,
  }) async {
    if (take < 1 || take > 500) {
      throw ArgumentError.value(take, 'take', 'Must be between 1 and 500.');
    }
    final query = Uri(
      queryParameters: {
        'take': '$take',
        'continuationToken': ?continuationToken,
      },
    ).query;
    return ComputeTracePage.fromJson(
      Map<String, dynamic>.from(
        await _tableRequest(
          'GET',
          '/brains/${Uri.encodeComponent(brainId)}/compute/traces/${Uri.encodeComponent(traceId)}?$query',
        ) as Map,
      ),
    );
  }

  Future<void> postUi(String path, {Map<String, Object?>? body}) async {
    await _request(
      'POST',
      path,
      body: body,
      timeout: const Duration(seconds: 10),
    );
  }

  Future<BrainSnapshot> readBrain({required String chatName}) async {
    final response = await _request(
      'GET',
      '/chats/${Uri.encodeComponent(chatName)}/brain',
      timeout: const Duration(seconds: 10),
    );
    return BrainSnapshot.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  /// The brain's scenes over this client's authenticated session.
  SceneClient scenes(String workspace) =>
      SceneClient(_http, baseUri, workspace);

  Future<http.Response> _request(
    String method,
    String path, {
    Map<String, Object?>? body,
    Duration? timeout,
  }) async {
    final abort = timeout == null ? null : Completer<void>();
    final request = abort == null
        ? http.Request(method, baseUri.resolve(path))
        : http.AbortableRequest(
            method,
            baseUri.resolve(path),
            abortTrigger: abort.future,
          );
    if (body != null) {
      request.headers['content-type'] = 'application/json';
      request.body = jsonEncode(body);
    }
    final operation = _http.send(request).then(http.Response.fromStream);
    final response = timeout == null
        ? await operation
        : await operation.timeout(
            timeout,
            onTimeout: () {
              // Stop the actual request, so timed-out graph polls cannot accumulate
              // network work while the store schedules a retry.
              abort!.complete();
              throw TimeoutException('$method $path timed out', timeout);
            },
          );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw UiRequestException(
        method,
        path,
        response.statusCode,
        response.body,
      );
    }
    return response;
  }

  Future<Map<String, dynamic>> appRequest(
    String workspace,
    String path, {
    Map<String, Object?>? body,
  }) async {
    final response = await _request(
      body == null ? 'GET' : 'POST',
      '/brains/${Uri.encodeComponent(workspace)}/apps/$path',
      body: body,
      timeout: const Duration(seconds: 30),
    );
    return Map<String, dynamic>.from(jsonDecode(response.body) as Map);
  }

  /// Calls the developer-mode C# file API; [path] is relative to `/csharp` (empty for the list).
  Future<Map<String, dynamic>> csharpRequest(
    String workspace,
    String method,
    String path, {
    Map<String, Object?>? body,
  }) async {
    try {
      final result = await _tableRequest(
        method,
        '/brains/${Uri.encodeComponent(workspace)}/csharp${path.isEmpty ? '' : '/$path'}',
        body: body,
        // Starting builds the app inside a .NET SDK container, which can take minutes.
        timeout: const Duration(minutes: 3),
      );
      return result is Map ? Map<String, dynamic>.from(result) : {};
    } on TableRequestException catch (error) {
      throw CSharpFileRequestException(error.statusCode, error.message);
    }
  }

  Future<Map<String, dynamic>> storeSecret(
    String name,
    String label,
    String value,
  ) async {
    final owner = principalId;
    if (owner == null) {
      throw StateError('Sign in before storing a secret.');
    }
    final response = await _request(
      'POST',
      '/secrets/${Uri.encodeComponent(owner)}',
      body: {'name': name, 'label': label, 'value': value},
      timeout: const Duration(seconds: 30),
    );
    return response.body.isEmpty
        ? <String, dynamic>{}
        : Map<String, dynamic>.from(jsonDecode(response.body) as Map);
  }

  Future<ChatModelCatalog> readModelCatalog() async =>
      ChatModelCatalog.fromJson(
        Map<String, dynamic>.from(
          await jsonRequest('GET', '/ai/models') as Map,
        ),
      );

  Future<Object?> connectorRequest(
    String workspaceId,
    String path, {
    Map<String, Object?>? body,
  }) {
    if (path.isNotEmpty) {
      throw ArgumentError.value(
        path,
        'path',
        'The connector route is the catalog only.',
      );
    }
    return jsonRequest('GET', '/connectors');
  }

  Future<Object?> workspaceConnectionsRequest(
    String workspaceId,
    String path, {
    Map<String, Object?>? body,
  }) => jsonRequest(
    body == null ? 'GET' : 'POST',
    '/brains/${Uri.encodeComponent(workspaceId)}/connections${path.isEmpty ? '' : '/$path'}',
    body,
  );

  Future<Object?> workspaceWindowsRequest(
    String workspaceId,
    String path, {
    Map<String, Object?>? body,
  }) => jsonRequest(
    body == null ? 'GET' : 'POST',
    '/brains/${Uri.encodeComponent(workspaceId)}/windows/$path',
    body,
  );

  /// The live grants an owner has given to apps in this workspace.
  Future<List<GrantSummary>> listGrants(String workspace) async {
    final response = await _request(
      'GET',
      '/brains/${Uri.encodeComponent(workspace)}/grants',
      timeout: const Duration(seconds: 30),
    );
    return [
      for (final item in jsonDecode(response.body) as List)
        GrantSummary.fromJson(Map<String, dynamic>.from(item as Map)),
    ];
  }

  /// Revokes one (app, semantic type, mode) grant so its value looks missing again.
  Future<void> revokeGrant(
    String workspace, {
    required String appId,
    required String semanticTypeId,
    required String mode,
  }) async {
    await _request(
      'POST',
      '/brains/${Uri.encodeComponent(workspace)}/grants/revoke',
      body: {
        'appId': appId,
        'semanticTypeId': semanticTypeId,
        'mode': _grantModeValue(mode),
      },
      timeout: const Duration(seconds: 30),
    );
  }

  static Object _grantModeValue(String mode) => switch (mode) {
    'Once' => 0,
    'ThisChat' => 1,
    _ => 2,
  };

  void close() {
    if (_ownsClient) {
      _http.close();
    }
    for (final mux in _watches.values) {
      unawaited(mux.close());
    }
    _watches.clear();
    unawaited(_link?.shutdown() ?? Future.value());
    _link = null;
  }

  Future<dynamic> jsonRequest(
    String method,
    String path, [
    Object? body,
  ]) async {
    // Creating or authoring an app is one model inference call; everything else on the
    // apps surface (implement) answers immediately with 202.
    final authoring =
        method == 'POST' &&
        (path.endsWith('/apps') || path.endsWith('/author'));
    final response = await _request(
      method,
      path,
      body: body == null ? null : Map<String, Object?>.from(body as Map),
      timeout: authoring
          ? const Duration(minutes: 3)
          : const Duration(seconds: 30),
    );
    return response.body.isEmpty ? null : jsonDecode(response.body);
  }
}

/// Includes status without requiring callers to parse server error text.
final class UiRequestException extends StateError {
  UiRequestException(String method, String path, this.statusCode, String body)
    : detail = _problemDetail(body),
      error = _field(body, 'error'),
      status = _field(body, 'status'),
      super('$method $path failed: $statusCode $body');
  final int statusCode;
  final String? detail;

  /// The answer's own `error` and `status` strings, when it is a JSON object that carries them.
  final String? error, status;

  static String? _field(String body, String name) {
    try {
      final value = jsonDecode(body);
      return value is Map && value[name] is String
          ? value[name] as String
          : null;
    } on FormatException {
      return null;
    }
  }

  static String? _problemDetail(String body) {
    try {
      final problem = jsonDecode(body);
      if (problem is Map && problem['detail'] is String) {
        return problem['detail'] as String;
      }
    } on FormatException {
      // Non-JSON failures keep their existing diagnostic text.
    }
    return null;
  }

  @override
  String toString() => detail ?? super.toString();
}

final class CSharpFileRequestException implements Exception {
  const CSharpFileRequestException(this.statusCode, this.message);
  final int statusCode;
  final String message;
  @override
  String toString() => 'C# file request failed ($statusCode): $message';
}
