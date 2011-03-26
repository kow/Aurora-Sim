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
    public class SimianOfflineMessagesConnector : IOfflineMessagesConnector
    {
        private List<string> m_ServerURIs = new List<string>();

        public void Initialize(IGenericData unneeded, IConfigSource source, IRegistryCore simBase, string defaultConnectionString)
        {
            if (source.Configs["AuroraConnectors"].GetString("OfflineMessagesConnector", "LocalConnector") == "SimianConnector")
            {
                m_ServerURIs = simBase.RequestModuleInterface<IConfigurationService>().FindValueOf("RemoteServerURI");
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
            List<GridInstantMessage> Messages = new List<GridInstantMessage>();
            Dictionary<string, OSDMap> Maps = new Dictionary<string,OSDMap>();
            foreach (string m_ServerURI in m_ServerURIs)
            {
                if (SimianUtils.GetGenericEntries(PrincipalID, "OfflineMessages", m_ServerURI, out Maps))
                {
                    GridInstantMessage baseMessage = new GridInstantMessage();
                    foreach (OSDMap map in Maps.Values)
                    {
                        baseMessage.FromOSD(map);
                        Messages.Add(baseMessage);
                    }
                }
            }
            return Messages.ToArray();
        }

        public void AddOfflineMessage(GridInstantMessage message)
        {
            foreach (string m_ServerURI in m_ServerURIs)
            {
                SimianUtils.AddGeneric(new UUID(message.toAgentID), "OfflineMessages", UUID.Random().ToString(), message.ToOSD(), m_ServerURI);
            }
        }

        #endregion
    }
}
