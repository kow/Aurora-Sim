using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using log4net;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Simulation.Base;

namespace Aurora.Modules
{
    /// <summary>
    /// This module loads/saves the avatar's profile from/into a "AvatarProfile Archive"
    /// </summary>
    public class AuroraAvatarProfileArchiver : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        private IUserAccountService UserAccountService
        {
            get { return m_scene.UserAccountService; }
        }

        public void Initialise(Nini.Config.IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_scene == null)
                m_scene = scene;

            MainConsole.Instance.Commands.AddCommand("region", false, "save avatar profile",
                                          "save avatar profile <First> <Last> <Filename>",
                                          "Saves profile and avatar data to an archive", HandleSaveAvatarProfile);
            MainConsole.Instance.Commands.AddCommand("region", false, "load avatar profile",
                                          "load avatar profile <First> <Last> <Filename>",
                                          "Loads profile and avatar data from an archive", HandleLoadAvatarProfile);
        }

        public void RemoveRegion(Scene scene)
        {

        }

        public void RegionLoaded(Scene scene)
        {

        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void PostInitialise() { }

        public void Close() { }

        public string Name { get { return "AvatarProfileArchiver"; } }

        public bool IsSharedModule
        {
            get { return true; }
        }

        protected void HandleLoadAvatarProfile(string module, string[] cmdparams)
        {
            if (cmdparams.Length != 6)
            {
                m_log.Info("[AvatarProfileArchiver] Not enough parameters!");
                return;
            }
            StreamReader reader = new StreamReader(cmdparams[5]);
            string document = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();

            string[] lines = document.Split('\n');
            List<string> file = new List<string>(lines);
            Dictionary<string, object> replyData = WebUtils.ParseXmlResponse(file[1]);

            Dictionary<string, object> results = replyData["result"] as Dictionary<string, object>;
            UserAccount UDA = new UserAccount();
            UDA.Name = cmdparams[3] + cmdparams[4];
            UDA.PrincipalID = UUID.Random();
            UDA.ScopeID = UUID.Zero;
            UDA.UserFlags = int.Parse(results["UserFlags"].ToString());
            UDA.UserLevel = 0; //For security... Don't want everyone loading full god mode.
            UDA.UserTitle = results["UserTitle"].ToString();
            UDA.Email = results["Email"].ToString();
            UDA.Created = int.Parse(results["Created"].ToString());
            if (results.ContainsKey("ServiceURLs") && results["ServiceURLs"] != null)
            {
                UDA.ServiceURLs = new Dictionary<string, object>();
                string str = results["ServiceURLs"].ToString();
                if (str != string.Empty)
                {
                    string[] parts = str.Split(new char[] { ';' });
                    foreach (string s in parts)
                    {
                        string[] parts2 = s.Split(new char[] { '*' });
                        if (parts2.Length == 2)
                            UDA.ServiceURLs[parts2[0]] = parts2[1];
                    }
                }
            }
            UserAccountService.StoreUserAccount(UDA);

            replyData = WebUtils.ParseXmlResponse(file[2]);
            IUserProfileInfo UPI = new IUserProfileInfo();
            UPI.FromKVP(replyData["result"] as Dictionary<string, object>);
            //Update the principle ID to the new user.
            UPI.PrincipalID = UDA.PrincipalID;

            IProfileConnector profileData = Aurora.DataManager.DataManager.RequestPlugin<IProfileConnector>();
            if (profileData.GetUserProfile(UPI.PrincipalID) == null)
                profileData.CreateNewProfile(UPI.PrincipalID);

            profileData.UpdateUserProfile(UPI);

            m_log.Info("[AvatarProfileArchiver] Loaded Avatar Profile from " + cmdparams[5]);
        }
        protected void HandleSaveAvatarProfile(string module, string[] cmdparams)
        {
            if (cmdparams.Length != 6)
            {
                m_log.Info("[AvatarProfileArchiver] Not enough parameters!");
                return;
            }
            UserAccount account = UserAccountService.GetUserAccount(UUID.Zero, cmdparams[3] + " " + cmdparams[4]);
            if (account == null)
            {
                m_log.Info("Account could not be found, stopping now.");
                return;
            }
            IProfileConnector data = Aurora.DataManager.DataManager.RequestPlugin<IProfileConnector>();
            string UPIxmlString = "";
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (data != null)
            {
                IUserProfileInfo profile = data.GetUserProfile(account.PrincipalID);
                if (profile != null)
                {
                    result["result"] = profile.ToKeyValuePairs();
                    UPIxmlString = WebUtils.BuildXmlResponse(result);
                }
            }

            result["result"] = account.ToKeyValuePairs();
            string UDAxmlString = WebUtils.BuildXmlResponse(result);

            StreamWriter writer = new StreamWriter(cmdparams[5]);
            writer.Write("<profile>\n");
            writer.Write(UDAxmlString + "\n");
            writer.Write(UPIxmlString + "\n");
            writer.Write("</profile>\n");
            writer.Close();
            writer.Dispose();
            m_log.Info("[AvatarProfileArchiver] Saved Avatar Profile to " + cmdparams[5]);
        }
    }
}
