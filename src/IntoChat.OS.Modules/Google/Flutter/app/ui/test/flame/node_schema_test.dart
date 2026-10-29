import 'dart:convert';
import 'dart:io';

import 'package:intochat_os_ui/intochat_os_ui.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'the Dart node schema matches docs/superpowers/contracts/flame-schema.json',
    () {
      final json = jsonDecode(
        File(
          '${_repositoryRoot()}/docs/superpowers/contracts/flame-schema.json',
        ).readAsStringSync(),
      ) as Map<String, dynamic>;

      expect(FlameSchema.gestures, (json['gestures'] as List).cast<String>());
      expect(
        FlameSchema.tweenProperties,
        (json['tweenProperties'] as List).cast<String>(),
      );
      expect(
        FlameSchema.tweenCurves,
        (json['tweenCurves'] as List).cast<String>(),
      );

      expect(FlameSchema.treeKinds, (json['treeKinds'] as List).cast<String>());
      expect(
        FlameSchema.reservedBlackboardKeys,
        (json['reservedBlackboardKeys'] as List).cast<String>(),
      );
      expect(FlameSchema.maxTreeNodes, json['maxTreeNodes']);
      expect(FlameSchema.maxTreeDepth, json['maxTreeDepth']);
      final tasks = (json['treeTasks'] as List).cast<Map<String, dynamic>>();
      expect(
        FlameSchema.treeTasks.map((task) => task.kind).toList(),
        tasks.map((task) => task['kind']).toList(),
      );
      for (var index = 0; index < tasks.length; index++) {
        final task = FlameSchema.treeTasks[index];
        expect(task.minSeconds, tasks[index]['minSeconds']);
        expect(task.maxSeconds, tasks[index]['maxSeconds']);
        expect(task.minValue, tasks[index]['minValue']);
        expect(task.maxValue, tasks[index]['maxValue']);
      }

      final nodes = (json['nodes'] as List).cast<Map<String, dynamic>>();
      expect(
        FlameSchema.nodes.map((node) => node.type).toList(),
        nodes.map((node) => node['type']).toList(),
      );
      for (final node in FlameSchema.nodes) {
        final spec = nodes.firstWhere(
          (candidate) => candidate['type'] == node.type,
        );
        final parameters = (spec['parameters'] as List)
            .cast<Map<String, dynamic>>();
        expect(
          node.parameters.map((param) => param.key).toList(),
          parameters.map((param) => param['key']).toList(),
        );
        for (var index = 0; index < node.parameters.length; index++) {
          final param = node.parameters[index];
          expect(param.kind.name, parameters[index]['kind']);
          expect(param.required, parameters[index]['required']);
          expect(
            param.choices,
            (parameters[index]['choices'] as List).cast<String>(),
          );
        }
      }
    },
  );

  test('the retired Home prefabs are absent from the vocabulary', () {
    expect(FlameSchema.forType('ino_sphere'), isNull);
    expect(FlameSchema.forType('brain_graph'), isNull);
    expect(FlameSchema.forType('sphere'), isNotNull);
  });
}

String _repositoryRoot() {
  var directory = Directory.current;
  while (!File('${directory.path}/Intochat.OS.slnx').existsSync()) {
    final parent = directory.parent;
    if (parent.path == directory.path) {
      throw StateError('Repository root not found.');
    }
    directory = parent;
  }
  return directory.path;
}
