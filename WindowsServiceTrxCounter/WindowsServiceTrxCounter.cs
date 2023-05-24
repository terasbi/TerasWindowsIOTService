using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServiceTrxCounter
{
    public partial class WindowsServiceTrxCounter : ServiceBase
    {
        public WindowsServiceTrxCounter()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            TrxMainClass.RunThread();
        }

        protected override void OnStop()
        {
            TrxMainClass.KillThread();
        }
    }
}
