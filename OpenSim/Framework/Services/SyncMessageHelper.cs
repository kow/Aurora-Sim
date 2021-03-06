/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Framework
{
    public class SyncMessageHelper
    {
        public static OSDMap ArrivedAtDestination(UUID AgentID, int DrawDistance, AgentCircuitData circuit, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("AgentID", AgentID);

            llsdBody.Add("DrawDistance", DrawDistance);
            llsdBody.Add("Circuit", circuit.PackAgentCircuitData());

            return buildEvent("ArrivedAtDestination", llsdBody, AgentID, requestingRegion);
        }

        /// <summary>
        /// Tells the region to tell the given agent that the other agent is online
        /// </summary>
        /// <param name="AgentID">Agent that is either logging in or logging out</param>
        /// <param name="FriendToInformID">Friend that will be told of the incoming/outgoing user</param>
        /// <param name="newStatus">Whether they are logged in or out</param>
        /// <returns></returns>
        public static OSDMap AgentStatusChange(UUID AgentID, UUID FriendToInformID, bool newStatus)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("AgentID", AgentID);

            llsdBody.Add("FriendToInformID", FriendToInformID);
            llsdBody.Add("NewStatus", newStatus);

            return buildEvent("AgentStatusChange", llsdBody, AgentID, 0);
        }

        public static OSDMap UpdateEstateInfo(uint EstateID, UUID RegionID)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("EstateID", EstateID);
            llsdBody.Add("RegionID", RegionID);

            return buildEvent("UpdateEstateInfo", llsdBody, UUID.Zero, 0);
        }

        public static OSDMap CrossAgent(GridRegion crossingRegion, Vector3 pos,
            Vector3 velocity, AgentCircuitData circuit, AgentData cAgent, ulong RequestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Pos", pos);
            llsdBody.Add("Vel", velocity);
            llsdBody.Add("Region", crossingRegion.ToOSD());
            llsdBody.Add("Circuit", circuit.PackAgentCircuitData());
            llsdBody.Add("AgentData", cAgent.Pack());
            return buildEvent("CrossAgent", llsdBody, circuit.AgentID, RequestingRegion);
        }

        public static OSDMap TeleportAgent(int DrawDistance, AgentCircuitData circuit,
            AgentData data, uint TeleportFlags,
            GridRegion destination, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("DrawDistance", DrawDistance);
            llsdBody.Add("Circuit", circuit.PackAgentCircuitData());
            llsdBody.Add("TeleportFlags", TeleportFlags);
            llsdBody.Add("AgentData", data.Pack());
            llsdBody.Add("Region", destination.ToOSD());
            return buildEvent("TeleportAgent", llsdBody, circuit.AgentID, requestingRegion);
        }

        public static OSDMap SendChildAgentUpdate(AgentPosition agentpos, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("AgentPos", agentpos.Pack());
            return buildEvent("SendChildAgentUpdate", llsdBody, agentpos.AgentID, requestingRegion);
        }

        public static OSDMap CancelTeleport(UUID AgentID, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("AgentID", AgentID);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("CancelTeleport", llsdBody, AgentID, requestingRegion);
        }

        public static OSDMap AgentLoggedOut(UUID AgentID, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("AgentID", AgentID);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("AgentLoggedOut", llsdBody, AgentID, requestingRegion);
        }

        public static OSDMap FriendGrantRights(UUID requester, UUID target, int myFlags, int rights, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Requester", requester);
            llsdBody.Add("Target", target);
            llsdBody.Add("MyFlags", myFlags);
            llsdBody.Add("Rights", rights);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("FriendGrantRights", llsdBody, requester, requestingRegion);
        }

        public static OSDMap FriendTerminated(UUID requester, UUID exfriend, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Requester", requester);
            llsdBody.Add("ExFriend", exfriend);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("FriendTerminated", llsdBody, requester, requestingRegion);
        }

        public static OSDMap FriendshipOffered(UUID requester, UUID friend, GridInstantMessage im, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Requester", requester);
            llsdBody.Add("Friend", friend);
            llsdBody.Add("IM", im.ToOSD());
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("FriendshipOffered", llsdBody, requester, requestingRegion);
        }

        public static OSDMap FriendshipDenied(UUID requester, string clientName, UUID friendID, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Requester", requester);
            llsdBody.Add("ClientName", clientName);
            llsdBody.Add("FriendID", friendID);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("FriendshipDenied", llsdBody, requester, requestingRegion);
        }

        public static OSDMap FriendshipApproved(UUID requester, string clientName, UUID friendID, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();

            llsdBody.Add("Requester", requester);
            llsdBody.Add("ClientName", clientName);
            llsdBody.Add("FriendID", friendID);
            llsdBody.Add("RequestingRegion", requestingRegion);
            return buildEvent("FriendshipApproved", llsdBody, requester, requestingRegion);
        }

        public static OSDMap LogoutRegionAgents(ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();
            return buildEvent("LogoutRegionAgents", llsdBody, UUID.Zero, requestingRegion);
        }

        public static OSDMap RegionIsOnline(ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();
            return buildEvent("RegionIsOnline", llsdBody, UUID.Zero, requestingRegion);
        }

        public static OSDMap DisableSimulator(UUID AgentID, ulong requestingRegion)
        {
            OSDMap llsdBody = new OSDMap();
            return buildEvent("DisableSimulator", llsdBody, AgentID, requestingRegion);
        }

        public static OSDMap buildEvent(string eventName, OSD eventBody, UUID AgentID, ulong requestingRegion)
        {
            OSDMap llsdEvent = new OSDMap(2);
            llsdEvent.Add("Message", eventBody);
            llsdEvent.Add("Method", new OSDString(eventName));
            llsdEvent.Add("AgentID", AgentID);
            llsdEvent.Add("RequestingRegion", requestingRegion);

            return llsdEvent;
        }
    }
}
