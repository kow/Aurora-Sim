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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using OpenMetaverse;
using log4net;
using log4net.Core;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Physics.Manager;
using Nini.Config;
using OpenSim;
using Aurora.Simulation.Base;
using Aurora.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate void NewScene(Scene scene);
    /// <summary>
    /// Manager for adding, closing, reseting, and restarting scenes.
    /// </summary>
    public class SceneManager : IApplicationPlugin
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event NewScene OnAddedScene;
        public event NewScene OnCloseScene;

        private List<Scene> m_localScenes;
        private Scene m_currentScene = null;
        private ISimulationBase m_OpenSimBase;
        private IConfigSource m_config = null;
        public IConfigSource ConfigSource
        {
            get { return m_config; }
        }
        private int RegionsFinishedStarting = 0;
        public int AllRegions = 0;
        protected ISimulationDataStore m_simulationDataService;
        protected List<ISharedRegionStartupModule> m_startupPlugins = new List<ISharedRegionStartupModule>();
        protected List<IClientNetworkServer> m_clientServers = new List<IClientNetworkServer>();
        
        public ISimulationDataStore SimulationDataService
        {
            get { return m_simulationDataService; }
        }

        public List<IClientNetworkServer> ClientServers
        {
            get { return m_clientServers; }
        }

        public List<Scene> Scenes
        {
            get { return m_localScenes; }
        }

        public Scene CurrentScene
        {
            get { return m_currentScene; }
        }

        public Scene CurrentOrFirstScene
        {
            get
            {
                if (m_currentScene == null)
                {
                    if (m_localScenes.Count > 0)
                    {
                        return m_localScenes[0];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return m_currentScene;
                }
            }
        }

        #endregion

        #region IApplicationPlugin members

        public void Initialize(ISimulationBase openSim)
        {
            m_OpenSimBase = openSim;

            IConfig handlerConfig = openSim.ConfigSource.Configs["ApplicationPlugins"];
            if (handlerConfig.GetString("SceneManager", "") != Name)
                return;

            m_localScenes = new List<Scene>();

            m_config = openSim.ConfigSource;

            string StorageDLL = "";

            string dllName = String.Empty;
            string connString = String.Empty;

            // Try reading the [DatabaseService] section, if it exists
            IConfig dbConfig = openSim.ConfigSource.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                dllName = dbConfig.GetString("StorageProvider", String.Empty);
                connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            // Try reading the [SimulationDataStore] section
            IConfig simConfig = openSim.ConfigSource.Configs["SimulationDataStore"];
            if (simConfig != null)
            {
                dllName = simConfig.GetString("StorageProvider", dllName);
                connString = simConfig.GetString("ConnectionString", connString);
            }

            // We tried, but this doesn't exist. We can't proceed
            if (dllName == String.Empty)
                dllName = "OpenSim.Data.Null.dll";

            m_simulationDataService = AuroraModuleLoader.LoadPlugin<ISimulationDataStore>(dllName);
            
            if (m_simulationDataService == null)
            {
                m_log.ErrorFormat("[SceneManager]: FAILED TO LOAD THE SIMULATION SERVICE AT '{0}', QUITING...", StorageDLL);
                System.Threading.Thread.Sleep(10000);
                Environment.Exit(0);
            }
            m_simulationDataService.Initialise(connString);

            AddConsoleCommands();

            //Load the startup modules for the region
            m_startupPlugins = AuroraModuleLoader.PickupModules<ISharedRegionStartupModule>();

            //Register us!
            m_OpenSimBase.ApplicationRegistry.RegisterModuleInterface<SceneManager>(this);
        }

        public void ReloadConfiguration(IConfigSource config)
        {
            //Update this
            m_config = config;
            if (m_localScenes != null)
                return;
            foreach (Scene scene in m_localScenes)
            {
                scene.Config = config;
                scene.PhysicsScene.PostInitialise(config);
            }
        }

        public void PostInitialise()
        {
        }

        public void Start()
        {
        }

        public void PostStart()
        {
        }

        public string Name
        {
            get { return "SceneManager"; }
        }

        public void Dispose()
        {
        }

        public void Close()
        {
            Scene[] scenes = new Scene[m_localScenes.Count];
            m_localScenes.CopyTo(scenes, 0);
            // collect known shared modules in sharedModules
            for (int i = 0; i < scenes.Length; i++)
            {
                // close scene/region
                CloseRegion(scenes[i]);
            }
        }

        #endregion

        #region Startup complete

        public void HandleStartupComplete(IScene scene, List<string> data)
        {
            RegionsFinishedStarting++;
            if (RegionsFinishedStarting == AllRegions)
            {
                FinishStartUp();
            }
        }

        private void FinishStartUp()
        {
            //Tell modules about it 
            StartupCompleteModules();

            m_OpenSimBase.RunStartupCommands();

            // For now, start at the 'root' level by default
            if (Scenes.Count == 1)
            {
                // If there is only one region, select it
                ChangeSelectedRegion(Scenes[0].RegionInfo.RegionName);
            }
            else
            {
                ChangeSelectedRegion("root");
            }

            TimeSpan timeTaken = DateTime.Now - m_OpenSimBase.StartupTime;

            m_log.InfoFormat("[SceneManager]: Startup is complete and took {0}m {1}s", timeTaken.Minutes, timeTaken.Seconds);
        }

        #endregion

        #region ForEach functions

        public void ForEachCurrentScene(Action<Scene> func)
        {
            if (m_currentScene == null)
            {
                m_localScenes.ForEach(func);
            }
            else
            {
                func(m_currentScene);
            }
        }

        public void ForEachScene(Action<Scene> action)
        {
            m_localScenes.ForEach(action);
        }

        #endregion

        #region TrySetScene functions

        public bool TrySetCurrentScene(string regionName)
        {
            if ((String.Compare(regionName, "root") == 0)
                || (String.Compare(regionName, "..") == 0)
                || (String.Compare(regionName, "/") == 0))
            {
                m_currentScene = null;
                return true;
            }
            else
            {
                foreach (Scene scene in m_localScenes)
                {
                    if (String.Compare(scene.RegionInfo.RegionName, regionName, true) == 0)
                    {
                        m_currentScene = scene;
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TrySetCurrentScene(UUID regionID)
        {
            m_log.Debug("Searching for Region: '" + regionID + "'");

            foreach (Scene scene in m_localScenes)
            {
                if (scene.RegionInfo.RegionID == regionID)
                {
                    m_currentScene = scene;
                    return true;
                }
            }

            return false;
        }

        public void ChangeSelectedRegion(string newRegionName)
        {
            if (!TrySetCurrentScene(newRegionName))
            {
                m_log.Info (String.Format ("Couldn't select region {0}", newRegionName));
                return;
            }

            string regionName = (CurrentScene == null ? "root" : CurrentScene.RegionInfo.RegionName);
            MainConsole.Instance.DefaultPrompt = String.Format("Region ({0}) ", regionName);
            MainConsole.Instance.ConsoleScene = CurrentScene;
        }

        #endregion

        #region TryGet functions

        public bool TryGetScene(string regionName, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (String.Compare(mscene.RegionInfo.RegionName, regionName, true) == 0)
                {
                    scene = mscene;
                    return true;
                }
            }
            scene = null;
            return false;
        }

        public bool TryGetScene(UUID regionID, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (mscene.RegionInfo.RegionID == regionID)
                {
                    scene = mscene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        /// <summary>
        /// Try to find a current scene at the given location
        /// </summary>
        /// <param name="locX">In meters</param>
        /// <param name="locY">In meters</param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool TryGetScene(int locX, int locY, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (mscene.RegionInfo.RegionLocX == locX &&
                    mscene.RegionInfo.RegionLocY == locY)
                {
                    scene = mscene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        /// <summary>
        /// Try to find a current scene at the given location
        /// </summary>
        /// <param name="locX">In meters</param>
        /// <param name="locY">In meters</param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool TryGetScene(ulong RegionHandle, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if (mscene.RegionInfo.RegionHandle == RegionHandle)
                {
                    scene = mscene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        public bool TryGetScene(IPEndPoint ipEndPoint, out Scene scene)
        {
            foreach (Scene mscene in m_localScenes)
            {
                if ((mscene.RegionInfo.InternalEndPoint.Equals(ipEndPoint.Address)) &&
                    (mscene.RegionInfo.InternalEndPoint.Port == ipEndPoint.Port))
                {
                    scene = mscene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        public bool TryGetScenePresence (UUID avatarId, out IScenePresence avatar)
        {
            foreach (Scene scene in m_localScenes)
            {
                if (scene.TryGetScenePresence(avatarId, out avatar))
                {
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        public bool TryGetAvatarsScene(UUID avatarId, out Scene scene)
        {
            IScenePresence avatar = null;
            foreach (Scene mScene in m_localScenes)
            {
                if (mScene.TryGetScenePresence(avatarId, out avatar))
                {
                    scene = mScene;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        public bool TryGetAvatarByName (string avatarName, out IScenePresence avatar)
        {
            foreach (Scene scene in m_localScenes)
            {
                if (scene.TryGetAvatarByName(avatarName, out avatar))
                {
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        #endregion

        #region Get functions

        public List<IScenePresence> GetCurrentSceneAvatars ()
        {
            List<IScenePresence> avatars = new List<IScenePresence> ();

            ForEachCurrentScene(
                delegate(Scene scene)
                {
                    scene.ForEachScenePresence (delegate (IScenePresence scenePresence)
                    {
                        if (!scenePresence.IsChildAgent)
                            avatars.Add(scenePresence);
                    });
                }
            );

            return avatars;
        }

        public List<IScenePresence> GetCurrentScenePresences ()
        {
            List<IScenePresence> presences = new List<IScenePresence> ();

            ForEachCurrentScene(delegate(Scene scene)
            {
                scene.ForEachScenePresence (delegate (IScenePresence sp)
                {
                    presences.Add(sp);
                });
            });

            return presences;
        }

        #endregion

        #region Add a region

        /// <summary>
        /// Execute the region creation process.  This includes setting up scene infrastructure.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="portadd_flag"></param>
        /// <param name="do_post_init"></param>
        /// <returns></returns>
        public void CreateRegion(RegionInfo regionInfo, out IScene m_scene)
        {
            // set the initial ports
            regionInfo.HttpPort = MainServer.Instance.Port;

            Scene scene = SetupScene(regionInfo, m_config);

            m_log.Info("[Modules]: Loading region modules");
            IRegionModulesController controller;
            if (m_OpenSimBase.ApplicationRegistry.TryRequestModuleInterface(out controller))
            {
                controller.AddRegionToModules(scene);
            }
            else
                m_log.Error("[Modules]: The new RegionModulesController is missing...");

            //Post init the modules now
            PostInitModules(scene);

            //Start the heartbeats
            scene.StartHeartbeat();
            //Tell the scene that the startup is complete 
            // Note: this event is added in the scene constructor
            scene.FinishedStartup("Startup", new List<string>());

            m_scene = scene;
        }

        /// <summary>
        /// Create a scene and its initial base structures.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="proxyOffset"></param>
        /// <param name="configSource"></param>
        /// <param name="clientServer"> </param>
        /// <returns></returns>
        protected Scene SetupScene(RegionInfo regionInfo, IConfigSource configSource)
        {
            AgentCircuitManager circuitManager = new AgentCircuitManager();
            IPAddress listenIP = regionInfo.InternalEndPoint.Address;
            if (!IPAddress.TryParse(regionInfo.InternalEndPoint.Address.ToString(), out listenIP))
                listenIP = IPAddress.Parse("0.0.0.0");

            uint port = (uint)regionInfo.InternalEndPoint.Port;

            string ClientstackDll = m_config.Configs["Startup"].GetString("ClientStackPlugin", "OpenSim.Region.ClientStack.LindenUDP.dll");

            IClientNetworkServer clientServer = AuroraModuleLoader.LoadPlugin<IClientNetworkServer> (ClientstackDll);
            clientServer.Initialise(
                    listenIP, ref port, 0, regionInfo.m_allow_alternate_ports,
                    m_config, circuitManager);

            regionInfo.InternalEndPoint.Port = (int)port;

            Scene scene = new Scene ();
            scene.AddModuleInterfaces (m_OpenSimBase.ApplicationRegistry.GetInterfaces ());
            scene.Initialize (regionInfo, circuitManager, clientServer);

            StartModules(scene);

            m_clientServers.Add(clientServer);

            //Do this here so that we don't have issues later when startup complete messages start coming in
            m_localScenes.Add(scene);

            return scene;
        }

        #endregion

        #region Reset a region

        public void ResetScene()
        {
            if (m_currentScene == null)
            {
                m_log.Warn("You must use this command on a region. Use 'change region' to change to the region you would like to change");
                return;
            }
            IBackupModule backup = m_currentScene.RequestModuleInterface<IBackupModule>();
            if(backup != null)
                backup.DeleteAllSceneObjects();
            ITerrainModule module = m_currentScene.RequestModuleInterface<ITerrainModule>();
            if (module != null)
            {
                module.ResetTerrain();
            }
        }

        #endregion

        #region Restart a region

        public void HandleRestart(Scene scene)
        {
            RegionInfo info = null;
            int RegionSceneElement = -1;
            for (int i = 0; i < m_localScenes.Count; i++)
            {
                if (scene.RegionInfo.RegionName == m_localScenes[i].RegionInfo.RegionName)
                {
                    RegionSceneElement = i;
                }
            }

            // Now we make sure the region is no longer known about by the SceneManager
            // Prevents duplicates.
            info = m_localScenes[RegionSceneElement].RegionInfo;
            if (RegionSceneElement >= 0)
            {
                m_localScenes.RemoveAt(RegionSceneElement);
            }

            CloseModules(scene);
            ShutdownClientServer(info);
            IScene iscene;
            CreateRegion(info, out iscene);
        }

        #endregion

        #region Shutdown regions

        public void ShutdownClientServer(RegionInfo whichRegion)
        {
            // Close and remove the clientserver for a region
            bool foundClientServer = false;
            int clientServerElement = 0;
            uint x, y;
            Utils.LongToUInts(whichRegion.RegionHandle, out x, out y);

            for (int i = 0; i < m_clientServers.Count; i++)
            {
                if (m_clientServers[i].HandlesRegion(x, y))
                {
                    clientServerElement = i;
                    foundClientServer = true;
                    break;
                }
            }

            if (foundClientServer)
            {
                m_clientServers[clientServerElement].Stop();
                m_clientServers.RemoveAt(clientServerElement);
            }
        }

        public void RemoveRegion(Scene scene, bool cleanup)
        {
            IBackupModule backup = ((Scene)scene).RequestModuleInterface<IBackupModule>();
            if (backup != null)
                backup.DeleteAllSceneObjects();

            CloseRegion(scene);

            if (!cleanup)
                return;

            IRegionLoader[] loaders = m_OpenSimBase.ApplicationRegistry.RequestModuleInterfaces<IRegionLoader>();
            foreach (IRegionLoader loader in loaders)
            {
                loader.DeleteRegion(scene.RegionInfo);
            }
        }

        /// <summary>
        /// Remove a region from the simulator without deleting it permanently.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public void CloseRegion(Scene scene)
        {
            // only need to check this if we are not at the
            // root level
            if ((CurrentScene != null) && (CurrentScene.RegionInfo.RegionID == scene.RegionInfo.RegionID))
            {
                TrySetCurrentScene("..");
            }

            m_localScenes.Remove(scene);
            scene.Close();

            m_log.DebugFormat("[SceneManager]: Shutting down region modules for {0}", scene.RegionInfo.RegionName);
            IRegionModulesController controller;
            if (m_OpenSimBase.ApplicationRegistry.TryRequestModuleInterface<IRegionModulesController>(out controller))
            {
                controller.RemoveRegionFromModules(scene);
            }

            CloseModules(scene);
            ShutdownClientServer(scene.RegionInfo);
        }

        #endregion

        #region ISharedRegionStartupModule initialization

        public void StartModules(Scene scene)
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.Initialise(scene, m_config, m_OpenSimBase);
            }
            if (OnAddedScene != null)
                OnAddedScene(scene);
        }

        public void PostInitModules(Scene scene)
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostInitialise(scene, m_config, m_OpenSimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.FinishStartup(scene, m_config, m_OpenSimBase);
            }
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.PostFinishStartup(scene, m_config, m_OpenSimBase);
            }
        }

        public void StartupCompleteModules()
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.StartupComplete();
            }
        }

        public void CloseModules(Scene scene)
        {
            foreach (ISharedRegionStartupModule module in m_startupPlugins)
            {
                module.Close(scene);
            }
            if (OnCloseScene != null)
                OnCloseScene(scene);
        }

        #endregion

        #region Console Commands

        private void AddConsoleCommands()
        {
            MainConsole.Instance.Commands.AddCommand ("show users", "show users [full]", "Shows users in the given region (if full is added, child agents are shown as well)", HandleShowUsers);
            MainConsole.Instance.Commands.AddCommand ("show regions", "show regions", "Show information about all regions in this instance", HandleShowRegions);
            MainConsole.Instance.Commands.AddCommand ("show maturity", "show maturity", "Show all region's maturity levels", HandleShowMaturity);

            MainConsole.Instance.Commands.AddCommand ("region", false, "force update", "force update", "Force the update of all objects on clients", HandleForceUpdate);

            MainConsole.Instance.Commands.AddCommand("region", false, "debug packet", "debug packet [level]", "Turn on packet debugging", Debug);
            MainConsole.Instance.Commands.AddCommand("region", false, "debug scene", "debug scene [scripting] [collisions] [physics]", "Turn on scene debugging", Debug);

            MainConsole.Instance.Commands.AddCommand("region", false, "change region", "change region [region name]", "Change current console region", ChangeSelectedRegion);

            MainConsole.Instance.Commands.AddCommand("region", false, "load xml2", "load xml2", "Load a region's data from XML2 format", LoadXml2);

            MainConsole.Instance.Commands.AddCommand("region", false, "save xml2", "save xml2", "Save a region's data in XML2 format", SaveXml2);

            MainConsole.Instance.Commands.AddCommand ("load oar", "load oar [--merge] [--skip-assets] [oar name] [OffsetX=#] [OffsetY=#] [OffsetZ=#]", "Load a region's data from OAR archive.  --merge will merge the oar with the existing scene.  --skip-assets will load the oar but ignore the assets it contains. OffsetX will change where the X location of the oar is loaded, and the same for Y and Z.", LoadOar);

            MainConsole.Instance.Commands.AddCommand("save oar", "save oar [-v|--version=N] [<OAR path>]", "Save a region's data to an OAR archive -v|--version=N generates scene objects as per older versions of the serialization (e.g. -v=0)" + Environment.NewLine
                                           + "The OAR path must be a filesystem path."
                                           + "  If this is not given then the oar is saved to region.oar in the current directory.", SaveOar);

            MainConsole.Instance.Commands.AddCommand("region", false, "kick user", "kick user [first] [last] [message]", "Kick a user off the simulator", KickUserCommand);

            MainConsole.Instance.Commands.AddCommand("region", false, "reset region", "reset region", "Reset region to the default terrain, wipe all prims, etc.", RunCommand);

            MainConsole.Instance.Commands.AddCommand("region", false, "restart-instance", "restart-instance", "Restarts the instance (as if you closed and re-opened Aurora)", RunCommand);

            MainConsole.Instance.Commands.AddCommand("region", false, "command-script", "command-script [script]", "Run a command script from file", RunCommand);

            MainConsole.Instance.Commands.AddCommand("region", false, "remove-region", "remove-region [name]", "Remove a region from this simulator", RunCommand);

            MainConsole.Instance.Commands.AddCommand("region", false, "delete-region", "delete-region [name]", "Delete a region from disk", RunCommand);

            MainConsole.Instance.Commands.AddCommand ("modules list", "modules list", "Lists all simulator modules", HandleModulesList);

            MainConsole.Instance.Commands.AddCommand ("modules unload", "modules unload [module]", "Unload the given simulator module", HandleModulesUnload);

            MainConsole.Instance.Commands.AddCommand ("region", false, "kill uuid", "kill uuid [UUID]", "Kill an object by UUID", KillUUID);
        }

        /// <summary>
        /// Kicks users off the region
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams">name of avatar to kick</param>
        private void KickUserCommand(string module, string[] cmdparams)
        {
            string alert = null;
            IList agents = GetCurrentScenePresences();

            if (cmdparams.Length < 4)
            {
                if (cmdparams.Length < 3)
                    return;
                if (cmdparams[2] == "all")
                {
                    foreach (IScenePresence presence in agents)
                    {
                        RegionInfo regionInfo = presence.Scene.RegionInfo;

                        m_log.Info (String.Format ("Kicking user: {0,-16}{1,-37} in region: {2,-16}", presence.Name, presence.UUID, regionInfo.RegionName));

                        // kick client...
                        if (alert != null)
                            presence.ControllingClient.Kick(alert);
                        else
                            presence.ControllingClient.Kick("\nThe Aurora manager kicked you out.\n");

                        // ...and close on our side
                        IEntityTransferModule transferModule = presence.Scene.RequestModuleInterface<IEntityTransferModule> ();
                        if(transferModule != null)
                            transferModule.IncomingCloseAgent (presence.Scene, presence.UUID);
                    }
                }
            }

            if (cmdparams.Length > 4)
                alert = String.Format("\n{0}\n", String.Join(" ", cmdparams, 4, cmdparams.Length - 4));

            foreach (IScenePresence presence in agents)
            {
                RegionInfo regionInfo = presence.Scene.RegionInfo;

                if (presence.Name.Contains (cmdparams[2].ToLower ()))
                {
                    m_log.Info (String.Format ("Kicking user: {0,-16}{1,-37} in region: {2,-16}", presence.Name, presence.UUID, regionInfo.RegionName));

                    // kick client...
                    if (alert != null)
                        presence.ControllingClient.Kick(alert);
                    else
                        presence.ControllingClient.Kick("\nThe Aurora manager kicked you out.\n");

                    // ...and close on our side
                    IEntityTransferModule transferModule = presence.Scene.RequestModuleInterface<IEntityTransferModule> ();
                    if (transferModule != null)
                        transferModule.IncomingCloseAgent (presence.Scene, presence.UUID);
                }
            }
            m_log.Info ("");
        }

        private void HandleClearAssets(string module, string[] args)
        {
            m_log.Info ("Not implemented.");
        }

        /// <summary>
        /// Force resending of all updates to all clients in active region(s)
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        private void HandleForceUpdate(string module, string[] args)
        {
            m_log.Info ("Updating all clients");
            ForEachCurrentScene(delegate(Scene scene)
            {
                ISceneEntity[] EntityList = scene.Entities.GetEntities ();

                foreach (ISceneEntity ent in EntityList)
                {
                    if (ent is SceneObjectGroup)
                    {
                        ((SceneObjectGroup)ent).ScheduleGroupUpdate(PrimUpdateFlags.FullUpdate);
                    }
                }
            });
        }

        /// <summary>
        /// Load, Unload, and list Region modules in use
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd"></param>
        private void HandleModulesUnload(string module, string[] cmd)
        {
            List<string> args = new List<string> (cmd);
            args.RemoveAt (0);
            string[] cmdparams = args.ToArray ();

            IRegionModulesController controller = m_OpenSimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController> ();
            if (cmdparams.Length > 1)
            {
                foreach (IRegionModuleBase irm in controller.AllModules)
                {
                    if (irm.Name.ToLower () == cmdparams[1].ToLower ())
                    {
                        m_log.Info (String.Format ("Unloading module: {0}", irm.Name));
                        foreach (Scene scene in Scenes)
                            irm.RemoveRegion (scene);
                        irm.Close ();
                    }
                }
            }
        }

        /// <summary>
        /// Load, Unload, and list Region modules in use
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmd"></param>
        private void HandleModulesList (string module, string[] cmd)
        {
            List<string> args = new List<string> (cmd);
            args.RemoveAt (0);
            
            IRegionModulesController controller = m_OpenSimBase.ApplicationRegistry.RequestModuleInterface<IRegionModulesController> ();
            foreach (IRegionModuleBase irm in controller.AllModules)
            {
                if (irm is ISharedRegionModule)
                    m_log.Info (String.Format ("Shared region module: {0}", irm.Name));
                else if (irm is INonSharedRegionModule)
                    m_log.Info (String.Format ("Nonshared region module: {0}", irm.Name));
                else
                    m_log.Info (String.Format ("Unknown type " + irm.GetType ().ToString () + " region module: {0}", irm.Name));
            }
        }

        /// <summary>
        /// Serialize region data to XML2Format
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void SaveXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                IRegionSerialiserModule serialiser = CurrentOrFirstScene.RequestModuleInterface<IRegionSerialiserModule>();
                if (serialiser != null)
                    serialiser.SavePrimsToXml2(CurrentOrFirstScene, cmdparams[2]);
            }
            else
            {
                m_log.Warn("Wrong number of parameters!");
            }
        }

        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCommand(string module, string[] cmdparams)
        {
            List<string> args = new List<string>(cmdparams);
            if (args.Count < 1)
                return;

            string command = args[0];
            args.RemoveAt(0);

            cmdparams = args.ToArray();

            switch (command)
            {
                case "reset":
                    if (cmdparams.Length > 0)
                        if (cmdparams[0] == "region")
                            ResetScene();
                    break;
                case "command-script":
                    if (cmdparams.Length > 0)
                    {
                        m_OpenSimBase.RunCommandScript(cmdparams[0]);
                    }
                    break;

                case "remove-region":
                    string regRemoveName = CombineParams(cmdparams, 0);

                    Scene removeScene;
                    if (TryGetScene(regRemoveName, out removeScene))
                        RemoveRegion(removeScene, false);
                    else
                        m_log.Info ("no region with that name");
                    break;

                case "delete-region":
                    string regDeleteName = CombineParams(cmdparams, 0);

                    Scene killScene;
                    if (TryGetScene(regDeleteName, out killScene))
                        RemoveRegion(killScene, true);
                    else
                        m_log.Info ("no region with that name");
                    break;
                case "restart-instance":
                    //This kills the instance and restarts it
                    MainConsole.Instance.EndConsoleProcessing();
                    break;
            }
        }

        /// <summary>
        /// Change the currently selected region.  The selected region is that operated upon by single region commands.
        /// </summary>
        /// <param name="cmdParams"></param>
        protected void ChangeSelectedRegion(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                string newRegionName = CombineParams(cmdparams, 2);
                ChangeSelectedRegion(newRegionName);
            }
            else
            {
                m_log.Info ("Usage: change region <region name>");
            }
        }

        /// <summary>
        /// Turn on some debugging values for OpenSim.
        /// </summary>
        /// <param name="args"></param>
        protected void Debug(string module, string[] args)
        {
            if (args.Length == 1)
                return;

            switch (args[1])
            {
                case "packet":
                    if (args.Length > 2)
                    {
                        int newDebug;
                        if (int.TryParse(args[2], out newDebug))
                        {
                            SetDebugPacketLevelOnCurrentScene(newDebug);
                        }
                        else
                        {
                            m_log.Info ("packet debug should be 0..255");
                        }
                        m_log.Info (String.Format ("New packet debug: {0}", newDebug));
                    }

                    break;
                default:

                    m_log.Info ("Unknown debug");
                    break;
            }
        }

        /// <summary>
        /// Set the debug packet level on the current scene.  This level governs which packets are printed out to the
        /// console.
        /// </summary>
        /// <param name="newDebug"></param>
        private void SetDebugPacketLevelOnCurrentScene(int newDebug)
        {
            ForEachCurrentScene(
                delegate(Scene scene)
                {
                    scene.ForEachScenePresence (delegate (IScenePresence scenePresence)
                    {
                        if (!scenePresence.IsChildAgent)
                        {
                            m_log.DebugFormat("Packet debug for {0} set to {1}",
                                              scenePresence.Name,
                                              newDebug);

                            scenePresence.ControllingClient.SetDebugPacketLevel(newDebug);
                        }
                    });
                }
            );
        }

        public void HandleShowUsers (string mod, string[] cmd)
        {
            List<string> args = new List<string> (cmd);
            args.RemoveAt (0);
            string[] showParams = args.ToArray ();
            
            IList agents;
            if (showParams.Length > 1 && showParams[1] == "full")
            {
                agents = GetCurrentScenePresences ();
            }
            else
            {
                agents = GetCurrentSceneAvatars ();
            }

            m_log.Info (String.Format ("\nAgents connected: {0}\n", agents.Count));

            m_log.Info (String.Format ("{0,-16}{1,-16}{2,-37}{3,-11}{4,-16}{5,-30}", "Firstname", "Lastname", "Agent ID", "Root/Child", "Region", "Position"));

            foreach (IScenePresence presence in agents)
            {
                RegionInfo regionInfo = presence.Scene.RegionInfo;
                string regionName;

                if (regionInfo == null)
                {
                    regionName = "Unresolvable";
                }
                else
                {
                    regionName = regionInfo.RegionName;
                }

                m_log.Info (String.Format ("{0,-16}{1,-37}{2,-11}{3,-16}{4,-30}", presence.Name, presence.UUID, presence.IsChildAgent ? "Child" : "Root", regionName, presence.AbsolutePosition.ToString ()));
            }

            m_log.Info (String.Empty);
            m_log.Info (String.Empty);
        }

        public void HandleShowRegions (string mod, string[] cmd)
        {
            ForEachScene (delegate (Scene scene)
            {
                m_log.Info (scene.ToString ());
            });
        }

        public void HandleShowMaturity (string mod, string[] cmd)
        {
            ForEachCurrentScene (delegate (Scene scene)
            {
                string rating = "";
                if (scene.RegionInfo.RegionSettings.Maturity == 1)
                {
                    rating = "Mature";
                }
                else if (scene.RegionInfo.RegionSettings.Maturity == 2)
                {
                    rating = "Adult";
                }
                else
                {
                    rating = "PG";
                }
                m_log.Info (String.Format ("Region Name: {0}, Region Rating {1}", scene.RegionInfo.RegionName, rating));
            });
        }

        public void SendCommandToPluginModules(string[] cmdparams)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.EventManager.TriggerOnPluginConsole(cmdparams); });
        }

        public void SetBypassPermissionsOnCurrentScene(bool bypassPermissions)
        {
            ForEachCurrentScene(delegate(Scene scene) { scene.Permissions.SetBypassPermissions(bypassPermissions); });
        }

        /// <summary>
        /// Load region data from Xml2Format
        /// </summary>
        /// <param name="module"></param>
        /// <param name="cmdparams"></param>
        protected void LoadXml2(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                try
                {
                    IRegionSerialiserModule serialiser = CurrentOrFirstScene.RequestModuleInterface<IRegionSerialiserModule>();
                    if (serialiser != null)
                        serialiser.LoadPrimsFromXml2(CurrentOrFirstScene, cmdparams[2]);
                }
                catch (FileNotFoundException)
                {
                    m_log.Info ("Specified xml not found. Usage: load xml2 <filename>");
                }
            }
            else
            {
                m_log.Warn("Not enough parameters!");
            }
        }

        /// <summary>
        /// Load a whole region from an opensimulator archive.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void LoadOar(string module, string[] cmdparams)
        {
            try
            {
                IRegionArchiverModule archiver = CurrentOrFirstScene.RequestModuleInterface<IRegionArchiverModule>();
                if (archiver != null)
                    archiver.HandleLoadOarConsoleCommand(string.Empty, cmdparams);
            }
            catch (Exception e)
            {
                m_log.Error (e.ToString ());
            }
        }

        /// <summary>
        /// Save a region to a file, including all the assets needed to restore it.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void SaveOar(string module, string[] cmdparams)
        {
            IRegionArchiverModule archiver = CurrentOrFirstScene.RequestModuleInterface<IRegionArchiverModule>();
            if (archiver != null)
                archiver.HandleSaveOarConsoleCommand(string.Empty, cmdparams);
        }

        private static string CombineParams(string[] commandParams, int pos)
        {
            string result = String.Empty;
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            result = result.TrimEnd(' ');
            return result;
        }

        /// <summary>
        /// Kill an object given its UUID.
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void KillUUID(string module, string[] cmdparams)
        {
            if (cmdparams.Length > 2)
            {
                UUID id = UUID.Zero;
                ISceneEntity grp = null;
                Scene sc = null;

                if (!UUID.TryParse(cmdparams[2], out id))
                {
                    m_log.Info ("[KillUUID]: Error bad UUID format!");
                    return;
                }

                ForEachScene(delegate(Scene scene)
                {
                    ISceneChildEntity part = scene.GetSceneObjectPart (id);
                    if (part == null)
                        return;

                    grp = part.ParentEntity;
                    sc = scene;
                });

                if (grp == null)
                {
                    m_log.Info (String.Format ("[KillUUID]: Given UUID {0} not found!", id));
                }
                else
                {
                    m_log.Info (String.Format ("[KillUUID]: Found UUID {0} in scene {1}", id, sc.RegionInfo.RegionName));
                    try
                    {
                        IBackupModule backup = sc.RequestModuleInterface<IBackupModule>();
                        if (backup != null)
                            backup.DeleteSceneObjects (new ISceneEntity[1] { grp }, true);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[KillUUID]: Error while removing objects from scene: " + e);
                    }
                }
            }
            else
            {
                m_log.Info ("[KillUUID]: Usage: kill uuid <UUID>");
            }
        }

        #endregion
    }
}
