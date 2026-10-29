import 'package:intochat_os_flutter/intochat_os_flutter.dart';
import 'package:test/test.dart';

void main() {
  test('confirmation inbox watches both requests and resolutions', () async {
    final link = RecordingLink();
    final client = IntoChatOSUiClient(baseUri: Uri.parse('http://localhost'))
      ..link = link;
    final watch = client
        .watchNode('alice', 'confirmation', 'alice/confirmations')
        .listen((_) {});
    await Future<void>.delayed(const Duration(milliseconds: 80));
    await watch.cancel();
    expect(link.brain, 'alice');
    expect(link.source!.key, 'alice/confirmations');
    expect(
      link.source!.contract,
      'IntoChat.OS.Confirmation.IConfirmationInbox',
    );
    expect(link.source!.types, [
      'ConfirmationRequested',
      'ConfirmationResolved',
    ]);
  });

  test(
    'friend slots watch the viewers space rather than foreign neurons',
    () async {
      final link = RecordingLink();
      final client = IntoChatOSUiClient(baseUri: Uri.parse('http://localhost'))
        ..link = link;
      for (final slot in [
        ('person', 'bob/profile'),
        ('card', 'bob/posts/p1'),
        ('collection', 'bob/posts'),
      ]) {
        final watch = client
            .watchNode('alice', slot.$1, slot.$2)
            .listen((_) {});
        await Future<void>.delayed(const Duration(milliseconds: 80));
        await watch.cancel();
        expect(link.brain, 'alice');
        expect(link.source!.key, 'alice/space');
        expect(link.source!.contract, 'IntoChat.OS.Modules.Flame.Scene.IScene');
        expect(link.source!.types, ['SceneChanged']);
      }
    },
  );
}

class RecordingLink implements NeuronLink {
  String? brain;
  WatchSourceSpec? source;
  @override
  Stream<NeuronSignal> watch(String brainId, List<WatchSourceSpec> sources) {
    brain = brainId;
    source = sources.single;
    return Stream<NeuronSignal>.multi((_) {});
  }

  @override
  Future<Map<String, dynamic>?> call(
    String brainId,
    String contract,
    String key,
    String method,
    List<Object?> arguments,
  ) async => null;
  @override
  Future<void> shutdown() async {}
}
