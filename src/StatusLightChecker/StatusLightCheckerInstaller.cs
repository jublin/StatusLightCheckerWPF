using System.ServiceProcess;

namespace StatusLightChecker
{
    // TODO: Service installer needs to be updated for .NET 10 compatibility
    // [System.Configuration.Install.RunInstaller(true)]
    // public class StatusLightCheckerInstaller : System.Configuration.Install.Installer
    // {
    //     public StatusLightCheckerInstaller()
    //     {
    //         var processInstaller = new System.ServiceProcess.ServiceProcessInstaller();
    //         var serviceInstaller = new System.ServiceProcess.ServiceInstaller();

    //         processInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
    //         serviceInstaller.ServiceName = "StatusLightCheckerService";
    //         serviceInstaller.DisplayName = "Status Light Checker Service";
    //         serviceInstaller.Description = "Monitors application status and updates lights accordingly";
    //         serviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;

    //         Installers.Add(processInstaller);
    //         Installers.Add(serviceInstaller);
    //     }
    // }
}