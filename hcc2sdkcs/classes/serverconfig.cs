namespace Sensia.HCC2.SDK.Config
{
    public class ServerConfig
    {
        public static string Host = "localhost";
        public static int Port = 502;
        public static byte SlaveID = 1;
        public static int TimeBetweenPolls = 1000;
        public static int TimeBetweenControls = 1000;
        public static int Timeout = 2000;
        public static int Retries = 0;
    }
}