// This is a generated file - do not edit.
//
// Generated from neurons.proto.

// @dart = 3.3

// ignore_for_file: annotate_overrides, camel_case_types, comment_references
// ignore_for_file: constant_identifier_names
// ignore_for_file: curly_braces_in_flow_control_structures
// ignore_for_file: deprecated_member_use_from_same_package, library_prefixes
// ignore_for_file: non_constant_identifier_names, prefer_relative_imports

import 'dart:core' as $core;

import 'package:fixnum/fixnum.dart' as $fixnum;
import 'package:protobuf/protobuf.dart' as $pb;

export 'package:protobuf/protobuf.dart' show GeneratedMessageGenericExtensions;

class CallRequest extends $pb.GeneratedMessage {
  factory CallRequest({
    $core.String? brainId,
    $core.String? contract,
    $core.String? key,
    $core.String? method,
    $core.List<$core.int>? arguments,
  }) {
    final result = CallRequest._();
    if (brainId != null) result.brainId = brainId;
    if (contract != null) result.contract = contract;
    if (key != null) result.key = key;
    if (method != null) result.method = method;
    if (arguments != null) result.arguments = arguments;
    return result;
  }

  CallRequest._();

  factory CallRequest.fromBuffer($core.List<$core.int> data,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      CallRequest()..mergeFromBuffer(data, registry);
  factory CallRequest.fromJson($core.String json,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      CallRequest()..mergeFromJson(json, registry);

  static final $pb.BuilderInfo _i = $pb.BuilderInfo(
      _omitMessageNames ? '' : 'CallRequest',
      package: const $pb.PackageName(_omitMessageNames ? '' : 'intochat.os'),
      createEmptyInstance: CallRequest.$_createMessage)
    ..aOS(1, _omitFieldNames ? '' : 'brainId')
    ..aOS(2, _omitFieldNames ? '' : 'contract')
    ..aOS(3, _omitFieldNames ? '' : 'key')
    ..aOS(4, _omitFieldNames ? '' : 'method')
    ..a<$core.List<$core.int>>(
        5, _omitFieldNames ? '' : 'arguments', $pb.PbFieldType.OY)
    ..hasRequiredFields = false;

  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  CallRequest clone() => deepCopy();
  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  CallRequest copyWith(void Function(CallRequest) updates) =>
      super.copyWith((message) => updates(message as CallRequest))
          as CallRequest;

  @$core.override
  $pb.BuilderInfo get info_ => _i;

  @$core.pragma('dart2js:noInline')
  @$core.Deprecated('Use CallRequest() / CallRequest.new instead')
  static CallRequest create() => CallRequest._();
  static $pb.GeneratedMessage $_createMessage() => CallRequest._();
  @$core.override
  CallRequest createEmptyInstance() => CallRequest._();
  @$core.pragma('dart2js:noInline')
  static CallRequest getDefault() =>
      _defaultInstance ??= $pb.GeneratedMessage.$_defaultFor<CallRequest>(
          CallRequest.$_createMessage);
  static CallRequest? _defaultInstance;

  @$pb.TagNumber(1)
  $core.String get brainId => $_getSZ(0);
  @$pb.TagNumber(1)
  set brainId($core.String value) => $_setString(0, value);
  @$pb.TagNumber(1)
  $core.bool hasBrainId() => $_has(0);
  @$pb.TagNumber(1)
  void clearBrainId() => $_clearField(1);

  @$pb.TagNumber(2)
  $core.String get contract => $_getSZ(1);
  @$pb.TagNumber(2)
  set contract($core.String value) => $_setString(1, value);
  @$pb.TagNumber(2)
  $core.bool hasContract() => $_has(1);
  @$pb.TagNumber(2)
  void clearContract() => $_clearField(2);

  @$pb.TagNumber(3)
  $core.String get key => $_getSZ(2);
  @$pb.TagNumber(3)
  set key($core.String value) => $_setString(2, value);
  @$pb.TagNumber(3)
  $core.bool hasKey() => $_has(2);
  @$pb.TagNumber(3)
  void clearKey() => $_clearField(3);

  @$pb.TagNumber(4)
  $core.String get method => $_getSZ(3);
  @$pb.TagNumber(4)
  set method($core.String value) => $_setString(3, value);
  @$pb.TagNumber(4)
  $core.bool hasMethod() => $_has(3);
  @$pb.TagNumber(4)
  void clearMethod() => $_clearField(4);

  @$pb.TagNumber(5)
  $core.List<$core.int> get arguments => $_getN(4);
  @$pb.TagNumber(5)
  set arguments($core.List<$core.int> value) => $_setBytes(4, value);
  @$pb.TagNumber(5)
  $core.bool hasArguments() => $_has(4);
  @$pb.TagNumber(5)
  void clearArguments() => $_clearField(5);
}

class CallReply extends $pb.GeneratedMessage {
  factory CallReply({
    $core.List<$core.int>? result,
  }) {
    final result$ = CallReply._();
    if (result != null) result$.result = result;
    return result$;
  }

  CallReply._();

  factory CallReply.fromBuffer($core.List<$core.int> data,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      CallReply()..mergeFromBuffer(data, registry);
  factory CallReply.fromJson($core.String json,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      CallReply()..mergeFromJson(json, registry);

  static final $pb.BuilderInfo _i = $pb.BuilderInfo(
      _omitMessageNames ? '' : 'CallReply',
      package: const $pb.PackageName(_omitMessageNames ? '' : 'intochat.os'),
      createEmptyInstance: CallReply.$_createMessage)
    ..a<$core.List<$core.int>>(
        1, _omitFieldNames ? '' : 'result', $pb.PbFieldType.OY)
    ..hasRequiredFields = false;

  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  CallReply clone() => deepCopy();
  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  CallReply copyWith(void Function(CallReply) updates) =>
      super.copyWith((message) => updates(message as CallReply)) as CallReply;

  @$core.override
  $pb.BuilderInfo get info_ => _i;

  @$core.pragma('dart2js:noInline')
  @$core.Deprecated('Use CallReply() / CallReply.new instead')
  static CallReply create() => CallReply._();
  static $pb.GeneratedMessage $_createMessage() => CallReply._();
  @$core.override
  CallReply createEmptyInstance() => CallReply._();
  @$core.pragma('dart2js:noInline')
  static CallReply getDefault() => _defaultInstance ??=
      $pb.GeneratedMessage.$_defaultFor<CallReply>(CallReply.$_createMessage);
  static CallReply? _defaultInstance;

  @$pb.TagNumber(1)
  $core.List<$core.int> get result => $_getN(0);
  @$pb.TagNumber(1)
  set result($core.List<$core.int> value) => $_setBytes(0, value);
  @$pb.TagNumber(1)
  $core.bool hasResult() => $_has(0);
  @$pb.TagNumber(1)
  void clearResult() => $_clearField(1);
}

class WatchSource extends $pb.GeneratedMessage {
  factory WatchSource({
    $core.String? contract,
    $core.String? key,
    $core.Iterable<$core.String>? signalTypes,
  }) {
    final result = WatchSource._();
    if (contract != null) result.contract = contract;
    if (key != null) result.key = key;
    if (signalTypes != null) result.signalTypes.addAll(signalTypes);
    return result;
  }

  WatchSource._();

  factory WatchSource.fromBuffer($core.List<$core.int> data,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      WatchSource()..mergeFromBuffer(data, registry);
  factory WatchSource.fromJson($core.String json,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      WatchSource()..mergeFromJson(json, registry);

  static final $pb.BuilderInfo _i = $pb.BuilderInfo(
      _omitMessageNames ? '' : 'WatchSource',
      package: const $pb.PackageName(_omitMessageNames ? '' : 'intochat.os'),
      createEmptyInstance: WatchSource.$_createMessage)
    ..aOS(1, _omitFieldNames ? '' : 'contract')
    ..aOS(2, _omitFieldNames ? '' : 'key')
    ..pPS(3, _omitFieldNames ? '' : 'signalTypes')
    ..hasRequiredFields = false;

  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  WatchSource clone() => deepCopy();
  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  WatchSource copyWith(void Function(WatchSource) updates) =>
      super.copyWith((message) => updates(message as WatchSource))
          as WatchSource;

  @$core.override
  $pb.BuilderInfo get info_ => _i;

  @$core.pragma('dart2js:noInline')
  @$core.Deprecated('Use WatchSource() / WatchSource.new instead')
  static WatchSource create() => WatchSource._();
  static $pb.GeneratedMessage $_createMessage() => WatchSource._();
  @$core.override
  WatchSource createEmptyInstance() => WatchSource._();
  @$core.pragma('dart2js:noInline')
  static WatchSource getDefault() =>
      _defaultInstance ??= $pb.GeneratedMessage.$_defaultFor<WatchSource>(
          WatchSource.$_createMessage);
  static WatchSource? _defaultInstance;

  @$pb.TagNumber(1)
  $core.String get contract => $_getSZ(0);
  @$pb.TagNumber(1)
  set contract($core.String value) => $_setString(0, value);
  @$pb.TagNumber(1)
  $core.bool hasContract() => $_has(0);
  @$pb.TagNumber(1)
  void clearContract() => $_clearField(1);

  @$pb.TagNumber(2)
  $core.String get key => $_getSZ(1);
  @$pb.TagNumber(2)
  set key($core.String value) => $_setString(1, value);
  @$pb.TagNumber(2)
  $core.bool hasKey() => $_has(1);
  @$pb.TagNumber(2)
  void clearKey() => $_clearField(2);

  @$pb.TagNumber(3)
  $pb.PbList<$core.String> get signalTypes => $_getList(2);
}

class WatchRequest extends $pb.GeneratedMessage {
  factory WatchRequest({
    $core.String? brainId,
    $core.Iterable<WatchSource>? sources,
  }) {
    final result = WatchRequest._();
    if (brainId != null) result.brainId = brainId;
    if (sources != null) result.sources.addAll(sources);
    return result;
  }

  WatchRequest._();

  factory WatchRequest.fromBuffer($core.List<$core.int> data,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      WatchRequest()..mergeFromBuffer(data, registry);
  factory WatchRequest.fromJson($core.String json,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      WatchRequest()..mergeFromJson(json, registry);

  static final $pb.BuilderInfo _i = $pb.BuilderInfo(
      _omitMessageNames ? '' : 'WatchRequest',
      package: const $pb.PackageName(_omitMessageNames ? '' : 'intochat.os'),
      createEmptyInstance: WatchRequest.$_createMessage)
    ..aOS(1, _omitFieldNames ? '' : 'brainId')
    ..pPM<WatchSource>(2, _omitFieldNames ? '' : 'sources',
        subBuilder: WatchSource.$_createMessage)
    ..hasRequiredFields = false;

  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  WatchRequest clone() => deepCopy();
  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  WatchRequest copyWith(void Function(WatchRequest) updates) =>
      super.copyWith((message) => updates(message as WatchRequest))
          as WatchRequest;

  @$core.override
  $pb.BuilderInfo get info_ => _i;

  @$core.pragma('dart2js:noInline')
  @$core.Deprecated('Use WatchRequest() / WatchRequest.new instead')
  static WatchRequest create() => WatchRequest._();
  static $pb.GeneratedMessage $_createMessage() => WatchRequest._();
  @$core.override
  WatchRequest createEmptyInstance() => WatchRequest._();
  @$core.pragma('dart2js:noInline')
  static WatchRequest getDefault() =>
      _defaultInstance ??= $pb.GeneratedMessage.$_defaultFor<WatchRequest>(
          WatchRequest.$_createMessage);
  static WatchRequest? _defaultInstance;

  @$pb.TagNumber(1)
  $core.String get brainId => $_getSZ(0);
  @$pb.TagNumber(1)
  set brainId($core.String value) => $_setString(0, value);
  @$pb.TagNumber(1)
  $core.bool hasBrainId() => $_has(0);
  @$pb.TagNumber(1)
  void clearBrainId() => $_clearField(1);

  @$pb.TagNumber(2)
  $pb.PbList<WatchSource> get sources => $_getList(1);
}

class SignalEnvelope extends $pb.GeneratedMessage {
  factory SignalEnvelope({
    $core.String? source,
    $core.String? type,
    $core.String? publisher,
    $fixnum.Int64? sequence,
    $core.List<$core.int>? payload,
    $fixnum.Int64? serverTimeMs,
  }) {
    final result = SignalEnvelope._();
    if (source != null) result.source = source;
    if (type != null) result.type = type;
    if (publisher != null) result.publisher = publisher;
    if (sequence != null) result.sequence = sequence;
    if (payload != null) result.payload = payload;
    if (serverTimeMs != null) result.serverTimeMs = serverTimeMs;
    return result;
  }

  SignalEnvelope._();

  factory SignalEnvelope.fromBuffer($core.List<$core.int> data,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      SignalEnvelope()..mergeFromBuffer(data, registry);
  factory SignalEnvelope.fromJson($core.String json,
          [$pb.ExtensionRegistry registry = $pb.ExtensionRegistry.EMPTY]) =>
      SignalEnvelope()..mergeFromJson(json, registry);

  static final $pb.BuilderInfo _i = $pb.BuilderInfo(
      _omitMessageNames ? '' : 'SignalEnvelope',
      package: const $pb.PackageName(_omitMessageNames ? '' : 'intochat.os'),
      createEmptyInstance: SignalEnvelope.$_createMessage)
    ..aOS(1, _omitFieldNames ? '' : 'source')
    ..aOS(2, _omitFieldNames ? '' : 'type')
    ..aOS(3, _omitFieldNames ? '' : 'publisher')
    ..aInt64(4, _omitFieldNames ? '' : 'sequence')
    ..a<$core.List<$core.int>>(
        5, _omitFieldNames ? '' : 'payload', $pb.PbFieldType.OY)
    ..aInt64(6, _omitFieldNames ? '' : 'serverTimeMs')
    ..hasRequiredFields = false;

  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  SignalEnvelope clone() => deepCopy();
  @$core.Deprecated('See https://github.com/google/protobuf.dart/issues/998.')
  SignalEnvelope copyWith(void Function(SignalEnvelope) updates) =>
      super.copyWith((message) => updates(message as SignalEnvelope))
          as SignalEnvelope;

  @$core.override
  $pb.BuilderInfo get info_ => _i;

  @$core.pragma('dart2js:noInline')
  @$core.Deprecated('Use SignalEnvelope() / SignalEnvelope.new instead')
  static SignalEnvelope create() => SignalEnvelope._();
  static $pb.GeneratedMessage $_createMessage() => SignalEnvelope._();
  @$core.override
  SignalEnvelope createEmptyInstance() => SignalEnvelope._();
  @$core.pragma('dart2js:noInline')
  static SignalEnvelope getDefault() =>
      _defaultInstance ??= $pb.GeneratedMessage.$_defaultFor<SignalEnvelope>(
          SignalEnvelope.$_createMessage);
  static SignalEnvelope? _defaultInstance;

  @$pb.TagNumber(1)
  $core.String get source => $_getSZ(0);
  @$pb.TagNumber(1)
  set source($core.String value) => $_setString(0, value);
  @$pb.TagNumber(1)
  $core.bool hasSource() => $_has(0);
  @$pb.TagNumber(1)
  void clearSource() => $_clearField(1);

  @$pb.TagNumber(2)
  $core.String get type => $_getSZ(1);
  @$pb.TagNumber(2)
  set type($core.String value) => $_setString(1, value);
  @$pb.TagNumber(2)
  $core.bool hasType() => $_has(1);
  @$pb.TagNumber(2)
  void clearType() => $_clearField(2);

  @$pb.TagNumber(3)
  $core.String get publisher => $_getSZ(2);
  @$pb.TagNumber(3)
  set publisher($core.String value) => $_setString(2, value);
  @$pb.TagNumber(3)
  $core.bool hasPublisher() => $_has(2);
  @$pb.TagNumber(3)
  void clearPublisher() => $_clearField(3);

  @$pb.TagNumber(4)
  $fixnum.Int64 get sequence => $_getI64(3);
  @$pb.TagNumber(4)
  set sequence($fixnum.Int64 value) => $_setInt64(3, value);
  @$pb.TagNumber(4)
  $core.bool hasSequence() => $_has(3);
  @$pb.TagNumber(4)
  void clearSequence() => $_clearField(4);

  @$pb.TagNumber(5)
  $core.List<$core.int> get payload => $_getN(4);
  @$pb.TagNumber(5)
  set payload($core.List<$core.int> value) => $_setBytes(4, value);
  @$pb.TagNumber(5)
  $core.bool hasPayload() => $_has(4);
  @$pb.TagNumber(5)
  void clearPayload() => $_clearField(5);

  @$pb.TagNumber(6)
  $fixnum.Int64 get serverTimeMs => $_getI64(5);
  @$pb.TagNumber(6)
  set serverTimeMs($fixnum.Int64 value) => $_setInt64(5, value);
  @$pb.TagNumber(6)
  $core.bool hasServerTimeMs() => $_has(5);
  @$pb.TagNumber(6)
  void clearServerTimeMs() => $_clearField(6);
}

const $core.bool _omitFieldNames =
    $core.bool.fromEnvironment('protobuf.omit_field_names');
const $core.bool _omitMessageNames =
    $core.bool.fromEnvironment('protobuf.omit_message_names');
