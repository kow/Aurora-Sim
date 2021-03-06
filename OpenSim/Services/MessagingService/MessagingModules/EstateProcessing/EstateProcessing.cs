﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Nini.Config;
using log4net;
using Aurora.Simulation.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Aurora.Framework;
using Aurora.DataManager;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.MessagingService
{
    public class EstateProcessing : IService
    {
        #region Declares

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IRegistryCore m_registry;

        #endregion

        #region IService Members

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            m_registry = registry;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            registry.RequestModuleInterface<ISimulationBase>().EventManager.OnGenericEvent += OnGenericEvent;

            //Also look for incoming messages to display
            registry.RequestModuleInterface<IAsyncMessageRecievedService>().OnMessageReceived += OnMessageReceived;
        }

        public void FinishedStartup()
        {
        }

        /// <summary>
        /// Server side
        /// </summary>
        /// <param name="FunctionName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        protected object OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "EstateUpdated")
            {
                EstateSettings es = (EstateSettings)parameters;
                IEstateConnector estateConnector = Aurora.DataManager.DataManager.RequestPlugin<IEstateConnector>();
                if (estateConnector != null)
                {
                    List<UUID> regions = estateConnector.GetRegions(es.EstateID);
                    if (regions != null)
                    {
                        foreach (UUID region in regions)
                        {
                            //Send the message to update all regions that are in this estate, as a setting changed
                            IAsyncMessagePostService asyncPoster = m_registry.RequestModuleInterface<IAsyncMessagePostService>();
                            IGridService gridService = m_registry.RequestModuleInterface<IGridService>();
                            if (gridService != null && asyncPoster != null)
                            {
                                GridRegion r = gridService.GetRegionByUUID(UUID.Zero, region);
                                if (r != null)
                                    asyncPoster.Post(r.RegionHandle, SyncMessageHelper.UpdateEstateInfo(es.EstateID, region));
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Region side
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected OSDMap OnMessageReceived(OSDMap message)
        {
            //We need to check and see if this is an AgentStatusChange
            if (message.ContainsKey("Method") && message["Method"] == "EstateUpdated")
            {
                OSDMap innerMessage = (OSDMap)message["Message"];
                //We got a message, deal with it
                uint estateID = innerMessage["EstateID"].AsUInteger();
                UUID regionID = innerMessage["RegionID"].AsUUID();
                SceneManager manager = m_registry.RequestModuleInterface<SceneManager>();
                if (manager != null)
                {
                    Scene s = null;
                    if (manager.TryGetScene(regionID, out s))
                    {
                        if (s.RegionInfo.EstateSettings.EstateID == estateID)
                        {
                            IEstateConnector estateConnector = Aurora.DataManager.DataManager.RequestPlugin<IEstateConnector>();
                            if (estateConnector != null)
                            {
                                EstateSettings es = null;
                                if (estateConnector.LoadEstateSettings(regionID, out es))
                                {
                                    s.RegionInfo.EstateSettings = es;
                                    m_log.Debug("[EstateProcessor]: Updated estate information.");
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
