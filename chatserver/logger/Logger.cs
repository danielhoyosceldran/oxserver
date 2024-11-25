using log4net;
using log4net.Config;
using System.IO;

public static class Logger
{
    // Loggers per a cada mòdul
    public static readonly ILog ApiLogger = LogManager.GetLogger("RequestServerLogger");
    public static readonly ILog WebSocketsServerLogger = LogManager.GetLogger("WebSocketsServerLogger");
    public static readonly ILog DataBaseLogger = LogManager.GetLogger("DataBaseLogger");
    public static readonly ILog UsersLogger = LogManager.GetLogger("UsersLogger");

    public static readonly ILog ConsoleLogger = LogManager.GetLogger("ConsoleLogger");

    // Configurar log4net només una vegada
    static Logger()
    {
        XmlConfigurator.Configure(new FileInfo("./config/log4net.config"));
    }
}
