import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:test/test.dart';

void main() {
  test('a uiBase query is used and remembered for the next PWA start', () {
    final remembered = <String>[];
    final value = IntoChatOSHostEnv.rememberedUiBase(
      query: 'https://os.example.test/',
      stored: null,
      remember: remembered.add,
    );
    expect(value, 'https://os.example.test/');
    expect(remembered, ['https://os.example.test/']);
  });

  test('a start without a query reconnects to the remembered OS', () {
    final value = IntoChatOSHostEnv.rememberedUiBase(
      query: null,
      stored: 'http://127.0.0.1:5080/',
      remember: (_) => fail('nothing new to remember'),
    );
    expect(value, 'http://127.0.0.1:5080/');
  });

  test('a new query replaces the remembered OS', () {
    final remembered = <String>[];
    final value = IntoChatOSHostEnv.rememberedUiBase(
      query: 'https://hosted.example.test/',
      stored: 'http://127.0.0.1:5080/',
      remember: remembered.add,
    );
    expect(value, 'https://hosted.example.test/');
    expect(remembered, ['https://hosted.example.test/']);
  });

  test('nothing configured leaves the page to the later fallbacks', () {
    expect(
      IntoChatOSHostEnv.rememberedUiBase(
        query: null,
        stored: null,
        remember: (_) => fail('nothing to remember'),
      ),
      isNull,
    );
  });
}
