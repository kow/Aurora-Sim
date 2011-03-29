using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Aurora.Framework;
using Aurora.DataManager;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using log4net;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Aurora.Simulation.Base;

namespace Aurora.Services.DataService
{
    public class RemoteMuteListConnector : IMuteListConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IRegistryCore m_registry;

        public void Initialize(IGenericData unneeded, IConfigSource source, IRegistryCore simBase, string defaultConnectionString)
        {
            m_registry = simBase;
            if (source.Configs["AuroraConnectors"].GetString("MuteListConnector", "LocalConnector") == "RemoteConnector")
            {
                DataManager.DataManager.RegisterPlugin(Name, this);
            }
        }

        public string Name
        {
            get { return "IMuteListConnector"; }
        }

        public void Dispose()
        {
        }

        #region IMuteListConnector Members

        public MuteList[] GetMuteList(UUID PrincipalID)
        {
            Dictionary<string, object> sendData = new Dictionary<string,object>();

            sendData["PRINCIPALID"] = PrincipalID.ToString();
            sendData["METHOD"] = "getmutelist";

            string reqString = WebUtils.BuildQueryString(sendData);
            List<MuteList> Mutes = new List<MuteList>();
            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf(PrincipalID.ToString(), "RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                           m_ServerURI + "/auroradata",
                           reqString);
                    if (reply != string.Empty)
                    {
                        Dictionary<string, object> replyData = WebUtils.ParseXmlResponse(reply);

                        foreach (object f in replyData)
                        {
                            KeyValuePair<string, object> value = (KeyValuePair<string, object>)f;
                            if (value.Value is Dictionary<string, object>)
                            {
                                Dictionary<string, object> valuevalue = value.Value as Dictionary<string, object>;
                                MuteList mute = new MuteList();
                                mute.FromKVP(valuevalue);
                                Mutes.Add(mute);
                            }
                        }
                    }
                }
                return Mutes.ToArray();
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteMuteListConnector]: Exception when contacting server: {0}", e.ToString());
            }
            return Mutes.ToArray();
        }

        public void UpdateMute(MuteList mute, UUID PrincipalID)
        {
            Dictionary<string, object> sendData = mute.ToKeyValuePairs();

            sendData["PRINCIPALID"] = PrincipalID.ToString();
            sendData["METHOD"] = "updatemute";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf(PrincipalID.ToString(), "RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    AsynchronousRestObjectRequester.MakeRequest("POST",
                        m_ServerURI + "/auroradata",
                        reqString);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteMuteListConnector]: Exception when contacting server: {0}", e.ToString());
            }
        }

        public void DeleteMute(UUID muteID, UUID PrincipalID)
        {
            Dictionary<string, object> sendData = new Dictionary<string,object>();

            sendData["PRINCIPALID"] = PrincipalID.ToString();
            sendData["MUTEID"] = muteID.ToString();
            sendData["METHOD"] = "deletemute";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf(PrincipalID.ToString(), "RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    AsynchronousRestObjectRequester.MakeRequest("POST",
                           m_ServerURI + "/auroradata",
                           reqString);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteMuteListConnector]: Exception when contacting server: {0}", e.ToString());
            }
        }

        public bool IsMuted(UUID PrincipalID, UUID PossibleMuteID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["PRINCIPALID"] = PrincipalID.ToString();
            sendData["MUTEID"] = PossibleMuteID.ToString();
            sendData["METHOD"] = "ismuted";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf(PrincipalID.ToString(), "RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                           m_ServerURI + "/auroradata",
                           reqString);
                    if (reply != string.Empty)
                    {
                        Dictionary<string, object> replyData = WebUtils.ParseXmlResponse(reply);
                        return bool.Parse(replyData["Muted"].ToString());
                    }
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteMuteListConnector]: Exception when contacting server: {0}", e.ToString());
            }
            return false;
        }

        #endregion
    }
}
