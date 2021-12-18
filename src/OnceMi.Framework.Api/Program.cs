using NLog.Web;

namespace OnceMi.Framework.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();                 //�Ƴ��Ѿ�ע���������־�������
                    logging.SetMinimumLevel(LogLevel.Trace);  //������С����־����
                    //logging.AddConsole();
                })
                .UseNLog()
                .ConfigureAppConfiguration((hostingContext, configuration) =>
                {
                    LoadAppsettings(hostingContext, configuration);
                })
                .ConfigureWebHostDefaults(host =>
                {
                    host.UseStartup<Startup>();
                });

        private static void LoadAppsettings(HostBuilderContext hostingContext, IConfigurationBuilder configuration)
        {
            string baseConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Base.json");
            if (!File.Exists(baseConfigPath))
            {
                throw new Exception($"Base app config not exist. Please check file '{baseConfigPath}'");
            }
            configuration.AddJsonFile(baseConfigPath, optional: false, reloadOnChange: true);

            string normalConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(normalConfigPath))
            {
                configuration.AddJsonFile(normalConfigPath, optional: false, reloadOnChange: true);
            }

            string eventName = hostingContext.HostingEnvironment.EnvironmentName;
            if (!string.IsNullOrEmpty(eventName))
            {
                string eventAppConfigPath = Path.Combine(AppContext.BaseDirectory, $"appsettings.{eventName}.json");
                if (File.Exists(eventAppConfigPath))
                {
                    configuration.AddJsonFile(eventAppConfigPath, optional: false, reloadOnChange: true);
                }
            }
        }
    }
}
