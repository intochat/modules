using DigitalBrain.Contracts;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Contracts;

namespace IntoChat.OS.Modules.Flutter.WebBrowser;

[NeuronAlias("webbrowser.connector"), DefaultNeuronType(UIVocabulary.WebBrowserType)]
public interface IWebBrowserConnector
{
    Task Connect(int port, string sessionId);
    Task Disconnect(string sessionId);
    Task Unavailable(string reason);
}
