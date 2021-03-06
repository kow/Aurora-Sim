﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nini.Config;
using Aurora.Simulation.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.MessagingService
{
    public class FriendsProcessing : IService
    {
        #region Declares

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

        protected object OnGenericEvent(string FunctionName, object parameters)
        {
            if (FunctionName == "UserStatusChange")
            {
                //A user has logged in or out... we need to update friends lists across the grid

                IAsyncMessagePostService asyncPoster = m_registry.RequestModuleInterface<IAsyncMessagePostService>();
                IFriendsService friendsService = m_registry.RequestModuleInterface<IFriendsService>();
                ICapsService capsService = m_registry.RequestModuleInterface<ICapsService>();
                IGridService gridService = m_registry.RequestModuleInterface<IGridService>();
                if (asyncPoster != null && friendsService != null && capsService != null && gridService != null)
                {
                    //Get all friends
                    UserInfo info = (UserInfo)parameters;
                    FriendInfo[] friends = friendsService.GetFriends(UUID.Parse(info.UserID));
                    List<UUID> OnlineFriends = new List<UUID>();
                    UUID us = UUID.Parse(info.UserID);
                    foreach (FriendInfo friend in friends)
                    {
                        UUID FriendToInform = UUID.Parse(friend.Friend);
                        //Now find their caps service so that we can find where they are root (and if they are logged in)
                        IClientCapsService clientCaps = capsService.GetClientCapsService(FriendToInform);
                        if (clientCaps != null)
                        {
                            OnlineFriends.Add(FriendToInform);
                            //Find the root agent
                            IRegionClientCapsService regionClientCaps = clientCaps.GetRootCapsService();
                            if (regionClientCaps != null)
                            {
                                //Post!
                                asyncPoster.Post(regionClientCaps.RegionHandle, SyncMessageHelper.AgentStatusChange(us, FriendToInform, info.IsOnline));
                            }
                        }
                    }
                    //If they are online, send all friends online statuses to them
                    if (info.IsOnline)
                    {
                        GridRegion ourRegion = gridService.GetRegionByUUID(UUID.Zero, info.CurrentRegionID);
                        if (ourRegion != null)
                        {
                            foreach (UUID onlineFriend in OnlineFriends)
                            {
                                asyncPoster.Post(ourRegion.RegionHandle, SyncMessageHelper.AgentStatusChange(onlineFriend, us, info.IsOnline));
                            }
                        }
                    }
                }
            }
            return null;
        }

        protected OSDMap OnMessageReceived(OSDMap message)
        {
            //We need to check and see if this is an AgentStatusChange
            if (message.ContainsKey("Method") && message["Method"] == "AgentStatusChange")
            {
                OSDMap innerMessage = (OSDMap)message["Message"];
                //We got a message, now pass it on to the clients that need it
                UUID AgentID = innerMessage["AgentID"].AsUUID();
                UUID FriendToInformID = innerMessage["FriendToInformID"].AsUUID();
                bool NewStatus = innerMessage["NewStatus"].AsBoolean();

                //Do this since IFriendsModule is a scene module, not a ISimulationBase module (not interchangable)
                SceneManager manager = m_registry.RequestModuleInterface<SceneManager>();
                if (manager != null && manager.Scenes.Count > 0)
                {
                    IFriendsModule friendsModule = manager.Scenes[0].RequestModuleInterface<IFriendsModule>();
                    if (friendsModule != null)
                    {
                        //Send the message
                        friendsModule.SendFriendsStatusMessage(FriendToInformID, AgentID, NewStatus);
                    }
                }
            }
            return null;
        }

        #endregion
    }
}
