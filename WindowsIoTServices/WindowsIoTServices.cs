using System.ServiceProcess;
using System.Configuration;


namespace WindowsIoTServices
{
    public partial class WindowsIoTServices : ServiceBase
    {
        public WindowsIoTServices()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            IoTMainClass.RunThread();
        }

        protected override void OnStop()
        { 
            IoTMainClass.KillThread();
            
        }
    }
}
