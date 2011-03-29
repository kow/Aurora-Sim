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
    public class RemoteOfflineMessagesConnector : IOfflineMessagesConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IRegistryCore m_registry;

        public void Initialize(IGenericData unneeded, IConfigSource source, IRegistryCore simBase, string defaultConnectionString)
        {
            m_registry = simBase;
            if (source.Configs["AuroraConnectors"].GetString ("OfflineMessagesConnector", "LocalConnector") == "RemoteConnector")
            {
                DataManager.DataManager.RegisterPlugin(Name, this);
            }
        }

        public string Name
        {
            get { return "IOfflineMessagesConnector"; }
        }

        public void Dispose()
        {
        }

        #region IOfflineMessagesConnector Members

        public GridInstantMessage[] GetOfflineMessages(UUID PrincipalID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["PRINCIPALID"] = PrincipalID;
            sendData["METHOD"] = "getofflinemessages";

            string reqString = WebUtils.BuildQueryString(sendData);
            List<GridInstantMessage> Messages = new List<GridInstantMessage>();
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
                                GridInstantMessage message = new GridInstantMessage();
                                message.FromKVP(valuevalue);
                                Messages.Add(message);
                            }
                        }
                    }
                }
                return Messages.ToArray();
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteOfflineMessagesConnector]: Exception when contacting server: {0}", e.ToString());
            }
            return Messages.ToArray();
        }

        public void AddOfflineMessage(GridInstantMessage message)
        {
            Dictionary<string, object> sendData = message.ToKeyValuePairs();

            sendData["METHOD"] = "addofflinemessage";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf(message.toAgentID.ToString(), "RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    AsynchronousRestObjectRequester.MakeRequest("POST",
                           m_ServerURI + "/auroradata",
                           reqString);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteOfflineMessagesConnector]: Exception when contacting server: {0}", e.ToString());
            }
        }

        #endregion
    }
}
