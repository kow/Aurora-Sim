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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using Nini.Config;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IScriptModule : INonSharedRegionModule
    {
        /// <summary>
        /// Should we be able to run currently?
        /// </summary>
        bool Disabled { get; set; }

        /// <summary>
        /// The name of our script engine
        /// </summary>
        string ScriptEngineName { get; }

        /// <summary>
        /// Adds an event to one script with the given parameters
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="primID"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        bool PostScriptEvent(UUID itemID, UUID primID, string name, Object[] args);
        /// <summary>
        /// Posts an event to the entire given object
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        bool PostObjectEvent(UUID itemID, string name, Object[] args);

        // Suspend ALL scripts in a given scene object. The item ID
        // is the UUID of a SOG, and the method acts on all contained
        // scripts. This is different from the suspend/resume that
        // can be issued by a client.
        //
        void SuspendScript(UUID itemID);
        void ResumeScript(UUID itemID);

        /// <summary>
        /// Gets all script errors for the given itemID
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        ArrayList GetScriptErrors(UUID itemID);

        /// <summary>
        /// Updates the given script with the options given
        /// </summary>
        /// <param name="partID"></param>
        /// <param name="itemID"></param>
        /// <param name="script"></param>
        /// <param name="startParam"></param>
        /// <param name="postOnRez"></param>
        /// <param name="stateSource"></param>
        void UpdateScript(UUID partID, UUID itemID, string script, int startParam, bool postOnRez, int stateSource);

        /// <summary>
        /// Stops all scripts that the ScriptEngine is running
        /// </summary>
        void StopAllScripts();

        /// <summary>
        /// Attempt to compile a script from the given assetID and return any compile errors
        /// </summary>
        /// <param name="assetID"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        string TestCompileScript(UUID assetID, UUID itemID);

        /// <summary>
        /// Changes script references from an old Item/Prim to a new one
        /// </summary>
        /// <param name="olditemID"></param>
        /// <param name="newItem"></param>
        /// <param name="newPart"></param>
        void UpdateScriptToNewObject(UUID olditemID, TaskInventoryItem newItem, OpenSim.Region.Framework.Scenes.SceneObjectPart newPart);

        /// <summary>
        /// Force a state save for the given script
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="primID"></param>
        void SaveStateSave(UUID itemID, UUID primID);

        /// <summary>
        /// Get a list of all script function names in the Apis
        /// </summary>
        /// <returns></returns>
        List<string> GetAllFunctionNames();

        /// <summary>
        /// Get the number of active (running) scripts in the given entity 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        int GetActiveScripts(IEntity obj);

        /// <summary>
        /// Get the number of scripts in the given entity 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        int GetTotalScripts (IEntity obj);

        /// <summary>
        /// Get the number of active (running) scripts that this engine is controlling
        /// </summary>
        /// <returns></returns>
        int GetActiveScripts();

        /// <summary>
        /// Get the number of events fired per second currently
        /// </summary>
        /// <returns></returns>
        int GetScriptEPS();

        /// <summary>
        /// Get the top scripts in the Script Engine
        /// </summary>
        /// <param name="RegionID"></param>
        /// <returns></returns>
        Dictionary<uint, float> GetTopScripts(UUID RegionID);
    }
}
