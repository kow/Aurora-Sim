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
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IEntityTransferModule
    {
        void Teleport(IScenePresence agent, ulong regionHandle, Vector3 position,
                                                      Vector3 lookAt, uint teleportFlags);

        void TeleportHome(UUID id, IClientAPI client);

        void Cross(IScenePresence agent, bool isFlying, GridRegion neighborRegion);

        bool CrossGroupToNewRegion(SceneObjectGroup sog, Vector3 position, GridRegion destination);

        void CancelTeleport(UUID AgentID, ulong RegionHandle);

        void RequestTeleportLocation(IClientAPI iClientAPI, ulong regionHandle, Vector3 position, Vector3 lookAt, uint p);

        void RequestTeleportLocation(IClientAPI iClientAPI, GridRegion reg, Vector3 position, Vector3 lookAt, uint p);

        void RequestTeleportLocation(IClientAPI iClientAPI, string RegionName, Vector3 pos, Vector3 lookat, uint p);

        bool IncomingCreateObject(UUID regionID, UUID userID, UUID itemID);

        bool IncomingCreateObject(UUID regionID, ISceneObject sog);

        bool NewUserConnection (IScene scene, AgentCircuitData agent, uint teleportFlags, out string reason);

        bool IncomingChildAgentDataUpdate (IScene scene, AgentData cAgentData);

        bool IncomingChildAgentDataUpdate (IScene scene, AgentPosition cAgentData);

        bool IncomingRetrieveRootAgent (IScene scene, UUID id, out IAgentData agent);

        bool IncomingCloseAgent (IScene scene, UUID agentID);
    }
}
