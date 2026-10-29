// This is a generated file - do not edit.
//
// Generated from neurons.proto.

// @dart = 3.3

// ignore_for_file: annotate_overrides, camel_case_types, comment_references
// ignore_for_file: constant_identifier_names
// ignore_for_file: curly_braces_in_flow_control_structures
// ignore_for_file: deprecated_member_use_from_same_package, library_prefixes
// ignore_for_file: non_constant_identifier_names, prefer_relative_imports

import 'dart:async' as $async;
import 'dart:core' as $core;

import 'package:grpc/service_api.dart' as $grpc;
import 'package:protobuf/protobuf.dart' as $pb;

import 'neurons.pb.dart' as $0;

export 'neurons.pb.dart';

/// The two operations the OS has: call a neuron contract, watch a source's signals.
/// Payloads are JSON with the HTTP surface's web defaults; the C# contracts are the schema.
@$pb.GrpcServiceName('intochat.os.Neurons')
class NeuronsClient extends $grpc.Client {
  /// The hostname for this service.
  static const $core.String defaultHost = '';

  /// OAuth scopes needed for the client.
  static const $core.List<$core.String> oauthScopes = [
    '',
  ];

  NeuronsClient(super.channel, {super.options, super.interceptors});

  $grpc.ResponseFuture<$0.CallReply> call(
    $0.CallRequest request, {
    $grpc.CallOptions? options,
  }) {
    return $createUnaryCall(_$call, request, options: options);
  }

  $grpc.ResponseStream<$0.SignalEnvelope> watch(
    $0.WatchRequest request, {
    $grpc.CallOptions? options,
  }) {
    return $createStreamingCall(_$watch, $async.Stream.fromIterable([request]),
        options: options);
  }

  // method descriptors

  static final _$call = $grpc.ClientMethod<$0.CallRequest, $0.CallReply>(
      '/intochat.os.Neurons/Call',
      ($0.CallRequest value) => value.writeToBuffer(),
      $0.CallReply.fromBuffer);
  static final _$watch = $grpc.ClientMethod<$0.WatchRequest, $0.SignalEnvelope>(
      '/intochat.os.Neurons/Watch',
      ($0.WatchRequest value) => value.writeToBuffer(),
      $0.SignalEnvelope.fromBuffer);
}

@$pb.GrpcServiceName('intochat.os.Neurons')
abstract class NeuronsServiceBase extends $grpc.Service {
  $core.String get $name => 'intochat.os.Neurons';

  NeuronsServiceBase() {
    $addMethod($grpc.ServiceMethod<$0.CallRequest, $0.CallReply>(
        'Call',
        call_Pre,
        false,
        false,
        ($core.List<$core.int> value) => $0.CallRequest.fromBuffer(value),
        ($0.CallReply value) => value.writeToBuffer()));
    $addMethod($grpc.ServiceMethod<$0.WatchRequest, $0.SignalEnvelope>(
        'Watch',
        watch_Pre,
        false,
        true,
        ($core.List<$core.int> value) => $0.WatchRequest.fromBuffer(value),
        ($0.SignalEnvelope value) => value.writeToBuffer()));
  }

  $async.Future<$0.CallReply> call_Pre(
      $grpc.ServiceCall $call, $async.Future<$0.CallRequest> $request) async {
    return call($call, await $request);
  }

  $async.Future<$0.CallReply> call(
      $grpc.ServiceCall call, $0.CallRequest request);

  $async.Stream<$0.SignalEnvelope> watch_Pre(
      $grpc.ServiceCall $call, $async.Future<$0.WatchRequest> $request) async* {
    yield* watch($call, await $request);
  }

  $async.Stream<$0.SignalEnvelope> watch(
      $grpc.ServiceCall call, $0.WatchRequest request);
}
