import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  tearDown(() => IntoChatOSHostEnv.servedConfig = const {});

  test('served config supplies the kernel url when no define is baked', () {
    IntoChatOSHostEnv.servedConfig = {
      IntoChatOSHostEnv.uiBaseVariable: 'http://localhost:5000',
      IntoChatOSHostEnv.shellVariable: 'desk-2',
      IntoChatOSHostEnv.chatVariable: 'side',
    };

    expect(
      IntoChatOSHostEnv.resolveUiBaseRaw(
        fromDefine: '',
        processEnvironment: const {},
      ),
      'http://localhost:5000',
    );
    expect(
      IntoChatOSHostEnv.resolveShell(
        fromDefine: '',
        processEnvironment: const {},
      ),
      'desk-2',
    );
    expect(
      IntoChatOSHostEnv.resolveChat(
        fromDefine: '',
        processEnvironment: const {},
      ),
      'side',
    );
  });

  test('a baked define beats the served config', () {
    IntoChatOSHostEnv.servedConfig = {
      IntoChatOSHostEnv.uiBaseVariable: 'http://served:1',
    };

    expect(
      IntoChatOSHostEnv.resolveUiBaseRaw(
        fromDefine: 'http://baked:2',
        processEnvironment: const {},
      ),
      'http://baked:2',
    );
  });

  test('absent served config falls back to the process environment', () {
    expect(
      IntoChatOSHostEnv.resolveUiBaseRaw(
        fromDefine: '',
        processEnvironment: {
          IntoChatOSHostEnv.uiBaseVariable: 'http://process:3',
        },
      ),
      'http://process:3',
    );
  });
}
