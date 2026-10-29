// This is a generated file - do not edit.
//
// Generated from neurons.proto.

// @dart = 3.3

// ignore_for_file: annotate_overrides, camel_case_types, comment_references
// ignore_for_file: constant_identifier_names
// ignore_for_file: curly_braces_in_flow_control_structures
// ignore_for_file: deprecated_member_use_from_same_package, library_prefixes
// ignore_for_file: non_constant_identifier_names, prefer_relative_imports
// ignore_for_file: unused_import

import 'dart:convert' as $convert;
import 'dart:core' as $core;
import 'dart:typed_data' as $typed_data;

@$core.Deprecated('Use callRequestDescriptor instead')
const CallRequest$json = {
  '1': 'CallRequest',
  '2': [
    {'1': 'brain_id', '3': 1, '4': 1, '5': 9, '10': 'brainId'},
    {'1': 'contract', '3': 2, '4': 1, '5': 9, '10': 'contract'},
    {'1': 'key', '3': 3, '4': 1, '5': 9, '10': 'key'},
    {'1': 'method', '3': 4, '4': 1, '5': 9, '10': 'method'},
    {'1': 'arguments', '3': 5, '4': 1, '5': 12, '10': 'arguments'},
  ],
};

/// Descriptor for `CallRequest`. Decode as a `google.protobuf.DescriptorProto`.
final $typed_data.Uint8List callRequestDescriptor = $convert.base64Decode(
    'CgtDYWxsUmVxdWVzdBIZCghicmFpbl9pZBgBIAEoCVIHYnJhaW5JZBIaCghjb250cmFjdBgCIA'
    'EoCVIIY29udHJhY3QSEAoDa2V5GAMgASgJUgNrZXkSFgoGbWV0aG9kGAQgASgJUgZtZXRob2QS'
    'HAoJYXJndW1lbnRzGAUgASgMUglhcmd1bWVudHM=');

@$core.Deprecated('Use callReplyDescriptor instead')
const CallReply$json = {
  '1': 'CallReply',
  '2': [
    {'1': 'result', '3': 1, '4': 1, '5': 12, '10': 'result'},
  ],
};

/// Descriptor for `CallReply`. Decode as a `google.protobuf.DescriptorProto`.
final $typed_data.Uint8List callReplyDescriptor =
    $convert.base64Decode('CglDYWxsUmVwbHkSFgoGcmVzdWx0GAEgASgMUgZyZXN1bHQ=');

@$core.Deprecated('Use watchSourceDescriptor instead')
const WatchSource$json = {
  '1': 'WatchSource',
  '2': [
    {'1': 'contract', '3': 1, '4': 1, '5': 9, '10': 'contract'},
    {'1': 'key', '3': 2, '4': 1, '5': 9, '10': 'key'},
    {'1': 'signal_types', '3': 3, '4': 3, '5': 9, '10': 'signalTypes'},
  ],
};

/// Descriptor for `WatchSource`. Decode as a `google.protobuf.DescriptorProto`.
final $typed_data.Uint8List watchSourceDescriptor = $convert.base64Decode(
    'CgtXYXRjaFNvdXJjZRIaCghjb250cmFjdBgBIAEoCVIIY29udHJhY3QSEAoDa2V5GAIgASgJUg'
    'NrZXkSIQoMc2lnbmFsX3R5cGVzGAMgAygJUgtzaWduYWxUeXBlcw==');

@$core.Deprecated('Use watchRequestDescriptor instead')
const WatchRequest$json = {
  '1': 'WatchRequest',
  '2': [
    {'1': 'brain_id', '3': 1, '4': 1, '5': 9, '10': 'brainId'},
    {
      '1': 'sources',
      '3': 2,
      '4': 3,
      '5': 11,
      '6': '.intochat.os.WatchSource',
      '10': 'sources'
    },
  ],
};

/// Descriptor for `WatchRequest`. Decode as a `google.protobuf.DescriptorProto`.
final $typed_data.Uint8List watchRequestDescriptor = $convert.base64Decode(
    'CgxXYXRjaFJlcXVlc3QSGQoIYnJhaW5faWQYASABKAlSB2JyYWluSWQSMgoHc291cmNlcxgCIA'
    'MoCzIYLmludG9jaGF0Lm9zLldhdGNoU291cmNlUgdzb3VyY2Vz');

@$core.Deprecated('Use signalEnvelopeDescriptor instead')
const SignalEnvelope$json = {
  '1': 'SignalEnvelope',
  '2': [
    {'1': 'source', '3': 1, '4': 1, '5': 9, '10': 'source'},
    {'1': 'type', '3': 2, '4': 1, '5': 9, '10': 'type'},
    {'1': 'publisher', '3': 3, '4': 1, '5': 9, '10': 'publisher'},
    {'1': 'sequence', '3': 4, '4': 1, '5': 3, '10': 'sequence'},
    {'1': 'payload', '3': 5, '4': 1, '5': 12, '10': 'payload'},
    {'1': 'server_time_ms', '3': 6, '4': 1, '5': 3, '10': 'serverTimeMs'},
  ],
};

/// Descriptor for `SignalEnvelope`. Decode as a `google.protobuf.DescriptorProto`.
final $typed_data.Uint8List signalEnvelopeDescriptor = $convert.base64Decode(
    'Cg5TaWduYWxFbnZlbG9wZRIWCgZzb3VyY2UYASABKAlSBnNvdXJjZRISCgR0eXBlGAIgASgJUg'
    'R0eXBlEhwKCXB1Ymxpc2hlchgDIAEoCVIJcHVibGlzaGVyEhoKCHNlcXVlbmNlGAQgASgDUghz'
    'ZXF1ZW5jZRIYCgdwYXlsb2FkGAUgASgMUgdwYXlsb2FkEiQKDnNlcnZlcl90aW1lX21zGAYgAS'
    'gDUgxzZXJ2ZXJUaW1lTXM=');
