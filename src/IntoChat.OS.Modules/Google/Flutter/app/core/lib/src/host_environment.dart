import 'package:http/http.dart' as http;

import 'process_environment_stub.dart'
    if (dart.library.io) 'process_environment_io.dart'
    as process_env;
import 'runtime_surface_io.dart'
    if (dart.library.html) 'runtime_surface_web.dart'
    as surface;
import 'served_config_io.dart'
    if (dart.library.html) 'served_config_web.dart'
    as served;

abstract final class IntoChatOSHostEnv {
  static const uiBaseVariable = 'INTOCHATOS_UI_BASE';
  static const shellVariable = 'INTOCHATOS_SHELL';
  static const defaultShellName = 'desk';
  static const chatVariable = 'INTOCHATOS_CHAT';
  static const defaultChatName = 'main';
  static const hostProcessVariables = {
    uiBaseVariable,
    shellVariable,
    chatVariable,
  };

  /// The values config.json served next to a web bundle, loaded once at boot by
  /// [loadServedConfig]. Keys are the host variable names above. Sits between
  /// baked defines and the remaining fallbacks in every resolver.
  static Map<String, String> servedConfig = const {};

  static Future<void> loadServedConfig({http.Client? client}) async {
    // A bundle with the define baked (the deployed shell) never fetches.
    if (const String.fromEnvironment(uiBaseVariable).isNotEmpty) {
      return;
    }
    servedConfig = await served.fetchServedConfig(client) ?? const {};
  }

  static String resolveUiBaseRaw({
    String fromDefine = const String.fromEnvironment(uiBaseVariable),
    Map<String, String>? processEnvironment,
  }) {
    if (fromDefine.isNotEmpty) {
      return fromDefine;
    }
    final fromPage = rememberedUiBase(
      query: surface.queryUiBase(),
      stored: surface.storedUiBase(),
      remember: surface.storeUiBase,
    );
    if (fromPage != null) {
      return fromPage;
    }
    final servedValue = servedConfig[uiBaseVariable] ?? '';
    if (servedValue.isNotEmpty) {
      return servedValue;
    }
    final process = processEnvironment ?? process_env.readProcessEnvironment();
    return process[uiBaseVariable] ?? '';
  }

  static Uri requireUiBaseUri({
    String fromDefine = const String.fromEnvironment(uiBaseVariable),
    Map<String, String>? processEnvironment,
  }) {
    final raw = resolveUiBaseRaw(
      fromDefine: fromDefine,
      processEnvironment: processEnvironment,
    );
    if (raw.isNotEmpty) {
      return browserKernelUri(Uri.parse(raw), surface.sameOriginUiBase());
    }

    // Deployed shell served from intochat-os-ui (same origin) has no process env.
    final sameOrigin = surface.sameOriginUiBase();
    if (sameOrigin != null) {
      return sameOrigin;
    }

    throw StateError(
      '$uiBaseVariable is required (AppHost WithFlutterHost injects it).',
    );
  }

  /// The kernel URL a browser page was pointed at. A `uiBase` query wins and is
  /// remembered, because an installed PWA starts from its manifest start_url,
  /// which carries no query: the next start reconnects to the same OS, local or
  /// hosted. Without a query the remembered value applies.
  static String? rememberedUiBase({
    required String? query,
    required String? stored,
    required void Function(String value) remember,
  }) {
    if (query != null && query.isNotEmpty) {
      if (query != stored) {
        remember(query);
      }
      return query;
    }
    return stored == null || stored.isEmpty ? null : stored;
  }

  static String resolveShell({
    String fromDefine = const String.fromEnvironment(shellVariable),
    Map<String, String>? processEnvironment,
  }) {
    if (fromDefine.isNotEmpty) {
      return fromDefine;
    }
    final servedValue = servedConfig[shellVariable] ?? '';
    if (servedValue.isNotEmpty) {
      return servedValue;
    }
    final process = processEnvironment ?? process_env.readProcessEnvironment();
    final raw = process[shellVariable] ?? '';
    if (raw.isEmpty) {
      return defaultShellName;
    }
    return raw;
  }

  static String resolveChat({
    String fromDefine = const String.fromEnvironment(chatVariable),
    Map<String, String>? processEnvironment,
  }) {
    if (fromDefine.isNotEmpty) {
      return fromDefine;
    }
    final servedValue = servedConfig[chatVariable] ?? '';
    if (servedValue.isNotEmpty) {
      return servedValue;
    }
    final process = processEnvironment ?? process_env.readProcessEnvironment();
    final raw = process[chatVariable] ?? '';
    if (raw.isEmpty) {
      return defaultChatName;
    }
    return raw;
  }
}

/// Keeps local shell and kernel cookies same-site without rewriting remote hosts.
Uri browserKernelUri(Uri kernel, Uri? page) {
  const loopback = {'localhost', '127.0.0.1', '::1', '[::1]'};
  if (page != null &&
      loopback.contains(kernel.host) &&
      loopback.contains(page.host)) {
    return kernel.replace(host: page.host);
  }
  return kernel;
}
