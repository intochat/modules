import 'package:grpc/grpc.dart';
import 'package:grpc/grpc_connection_interface.dart' show ClientChannelBase;

/// A native gRPC connection to the kernel; HTTP/2 end to end on desktop and mobile.
ClientChannelBase createNeuronChannel(Uri baseUri) => ClientChannel(
  baseUri.host,
  port: baseUri.hasPort ? baseUri.port : (baseUri.scheme == 'https' ? 443 : 80),
  options: ChannelOptions(
    credentials: baseUri.scheme == 'https'
        ? const ChannelCredentials.secure()
        : const ChannelCredentials.insecure(),
  ),
);
