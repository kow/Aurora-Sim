﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Aurora.Simulation.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;
using Aurora.DataManager;
using Aurora.Framework;
using Aurora.Services.DataService;
using OpenMetaverse.StructuredData;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.CapsService
{
    public class CapsService : ICapsService, IService
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A list of all clients and their Client Caps Handlers
        /// </summary>
        protected Dictionary<UUID, IClientCapsService> m_ClientCapsServices = new Dictionary<UUID, IClientCapsService>();

        /// <summary>
        /// A list of all regions Caps Services
        /// </summary>
        protected Dictionary<ulong, IRegionCapsService> m_RegionCapsServices = new Dictionary<ulong, IRegionCapsService>();

        protected IRegistryCore m_registry;
        public IRegistryCore Registry
        {
            get { return m_registry; }
        }

        protected IHttpServer m_server;
        public IHttpServer Server
        {
            get { return m_server; }
        }

        public string HostUri
        {
            get { return m_server.HostName + ":" + m_server.Port; }
        }

        #endregion

        #region IService members

        public string Name
        {
            get { return GetType().Name; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("CapsHandler", "") != Name)
                return;
            m_registry = registry;
            registry.RegisterModuleInterface<ICapsService>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            ISimulationBase simBase = registry.RequestModuleInterface<ISimulationBase>();
            m_server = simBase.GetHttpServer(0);

            MainConsole.Instance.Commands.AddCommand("CapsService", false, "show presences", "show presences", "Shows all presences in the grid", ShowUsers);
        }

        public void FinishedStartup()
        {
        }

        #endregion

        #region Console Commands

        protected void ShowUsers(string module, string[] cmd)
        {
            //Check for all or full to show child agents
            bool showChildAgents = cmd.Length == 3 ? cmd[2] == "all" ? true : cmd[2] == "full" ? true : false : false;
            foreach (IRegionCapsService regionCaps in m_RegionCapsServices.Values)
            {
                foreach (IRegionClientCapsService clientCaps in regionCaps.GetClients())
                {
                    if ((clientCaps.RootAgent || showChildAgents))
                    {
                        IGridService gridService = m_registry.RequestModuleInterface<IGridService>();
                        uint x, y;
                        Utils.LongToUInts(regionCaps.RegionHandle, out x, out y);
                        GridRegion region = gridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                        UserAccount account = m_registry.RequestModuleInterface<IUserAccountService>().GetUserAccount(UUID.Zero, clientCaps.AgentID);
                        m_log.InfoFormat("Region - {0}, User {1}, {2}, {3}", region.RegionName,account.Name, clientCaps.RootAgent ? "Root Agent" : "Child Agent", clientCaps.Disabled ? "Disabled" : "Not Disabled");
                    }
                }
            }
        }

        #endregion

        #region ICapsService members

        #region Client Caps

        /// <summary>
        /// Remove the all of the user's CAPS from the system
        /// </summary>
        /// <param name="AgentID"></param>
        public void RemoveCAPS(UUID AgentID)
        {
            if(m_ClientCapsServices.ContainsKey(AgentID))
            {
                IClientCapsService perClient = m_ClientCapsServices[AgentID];
                perClient.Close();
                m_ClientCapsServices.Remove(AgentID);
                m_registry.RequestModuleInterface<ISimulationBase>().EventManager.FireGenericEventHandler("UserLogout", AgentID);
            }
        }

        /// <summary>
        /// Create a Caps URL for the given user/region. Called normally by the EventQueueService or the LLLoginService on login
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="SimCAPS"></param>
        /// <param name="CAPS"></param>
        /// <param name="regionHandle"></param>
        /// <param name="IsRootAgent">Will this child be a root agent</param>
        /// <returns></returns>
        public string CreateCAPS(UUID AgentID, string CAPSBase, ulong regionHandle, bool IsRootAgent, AgentCircuitData circuitData)
        {
            //Now make sure we didn't use an old one or something
            IClientCapsService service = GetOrCreateClientCapsService(AgentID);
            IRegionClientCapsService clientService = service.GetOrCreateCapsService(regionHandle, CAPSBase, circuitData);
            
            //Fix the root agent status
            clientService.RootAgent = IsRootAgent;

            m_registry.RequestModuleInterface<ISimulationBase>().EventManager.FireGenericEventHandler("UserLogin", AgentID);
            m_log.Debug("[CapsService]: Adding Caps URL " + clientService.CapsUrl + " for agent " + AgentID);
            return clientService.CapsUrl;
        }

        /// <summary>
        /// Get or create a new Caps Service for the given client
        /// Note: This does not add them to a region if one is created. 
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        public IClientCapsService GetOrCreateClientCapsService(UUID AgentID)
        {
            if (!m_ClientCapsServices.ContainsKey(AgentID))
            {
                PerClientBasedCapsService client = new PerClientBasedCapsService();
                client.Initialise(this, AgentID);
                m_ClientCapsServices.Add(AgentID, client);
            }
            return m_ClientCapsServices[AgentID];
        }

        /// <summary>
        /// Get a Caps Service for the given client
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        public IClientCapsService GetClientCapsService(UUID AgentID)
        {
            if (!m_ClientCapsServices.ContainsKey(AgentID))
                return null;
            return m_ClientCapsServices[AgentID];
        }

        public List<IClientCapsService> GetClientsCapsServices()
        {
            return new List<IClientCapsService>(m_ClientCapsServices.Values);
        }

        #endregion

        #region Region Caps

        /// <summary>
        /// Get a region handler for the given region
        /// </summary>
        /// <param name="RegionHandle"></param>
        public IRegionCapsService GetCapsForRegion(ulong RegionHandle)
        {
            IRegionCapsService service;
            if (m_RegionCapsServices.TryGetValue(RegionHandle, out service))
            {
                return service;
            }
            return null;
        }

        /// <summary>
        /// Create a caps handler for the given region
        /// </summary>
        /// <param name="RegionHandle"></param>
        public void AddCapsForRegion(ulong RegionHandle)
        {
            if (!m_RegionCapsServices.ContainsKey(RegionHandle))
            {
                IRegionCapsService service = new PerRegionCapsService();
                service.Initialise(RegionHandle, Registry);

                m_RegionCapsServices.Add(RegionHandle, service);
            }
        }

        /// <summary>
        /// Remove the handler for the given region
        /// </summary>
        /// <param name="RegionHandle"></param>
        public void RemoveCapsForRegion(ulong RegionHandle)
        {
            if (m_RegionCapsServices.ContainsKey(RegionHandle))
                m_RegionCapsServices.Remove(RegionHandle);
        }

        public List<IRegionCapsService> GetRegionsCapsServices()
        {
            return new List<IRegionCapsService>(m_RegionCapsServices.Values);
        }

        #endregion

        #endregion
    }
}
