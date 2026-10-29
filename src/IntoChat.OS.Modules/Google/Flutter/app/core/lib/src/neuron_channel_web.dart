import 'package:grpc/grpc_connection_interface.dart' show ClientChannelBase;
import 'package:grpc/grpc_web.dart';

/// gRPC-Web over XHR: unary and server-streaming calls through an HTTP/1.1 front.
ClientChannelBase createNeuronChannel(Uri baseUri) =>
    GrpcWebClientChannel.xhr(baseUri);
