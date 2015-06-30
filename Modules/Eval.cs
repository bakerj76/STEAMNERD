using System;
using System.IO;
using System.Security;
using System.Security.Policy;
using System.Security.Permissions;
using System.Runtime.Remoting;
using System.Reflection;

using SteamKit2;

using Microsoft.CodeAnalysis.Scripting.CSharp;

namespace STEAMNERD.Modules
{
    class Eval : Module
    {
        public Eval(SteamNerd steamNerd) : base(steamNerd)
        {
        }

        public override bool Match(SteamFriends.ChatMsgCallback callback)
        {
            return callback.Message.ToLower().StartsWith("!eval");
        }

        public override void OnChatMsg(SteamFriends.ChatMsgCallback callback)
        {
            var equation = callback.Message.Remove(0, 5);

            var result = Sandboxer.CreateSandbox(equation);

            SteamNerd.SendMessage(result, callback.ChatRoomID, true);
        }
    }

    class Sandboxer : MarshalByRefObject
    {
        public static string CreateSandbox(string equation)
        {
            //var permissionSet = new PermissionSet(PermissionState.None);
            //permissionSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution));

            var evidence = new Evidence();
            evidence.AddHostEvidence(new Zone(SecurityZone.Internet));
            var permissionSet = SecurityManager.GetStandardSandbox(evidence);

            //var trustAssembly = typeof(Sandboxer).Assembly.Evidence.GetHostEvidence<StrongName>();

            var adSetup = new AppDomainSetup();
            adSetup.ApplicationBase = Path.GetFullPath("Eval");

            var domain = AppDomain.CreateDomain("Sandbox", null, adSetup, permissionSet);//, trustAssembly);

            var handle = Activator.CreateInstanceFrom(domain,
                typeof(Sandboxer).Assembly.ManifestModule.FullyQualifiedName,
                typeof(Sandboxer).FullName
            );

            Sandboxer domainInstance = (Sandboxer)handle.Unwrap();
            return domainInstance.Execute(equation);
        }

        public string Execute(string equation)
        {
            string result = "";

            try
            {
                result = CSharpScript.Eval(equation).ToString();
            }
            catch (Exception e)
            { 
                result = e.Message;
            }

            return result;
        }
    }
}
