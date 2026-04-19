using System.Configuration;
using System.Data;
using System.Windows;

namespace CK3_WPF_Reader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Upgrade settings from previous version if needed
            if (CK3_Reader.Properties.Settings.Default.UpgradeRequired)
            {
                CK3_Reader.Properties.Settings.Default.Upgrade();
                CK3_Reader.Properties.Settings.Default.UpgradeRequired = false;
                CK3_Reader.Properties.Settings.Default.Save();
            }
        }
    }

}
