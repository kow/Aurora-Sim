using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using log4net;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using Nini.Config;

namespace Aurora.Modules
{
    public class ObjectCacheModule : ISharedRegionModule, IObjectCache
    {
        #region Declares

        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected bool m_Enabled = true;
        private Dictionary<UUID, Dictionary<uint, uint>> ObjectCacheAgents = new Dictionary<UUID, Dictionary<uint, uint>>();
        private string m_filePath = "ObjectCache/";

        #endregion

        #region ISharedRegionModule

        public virtual void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["ObjectCache"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetString("Module", "") == Name;
                m_filePath = moduleConfig.GetString("PathToSaveFiles", m_filePath);
            }
            if (!Directory.Exists(m_filePath))
            {
                try
                {
                    Directory.CreateDirectory(m_filePath);
                }
                catch (Exception) { }
            }
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IObjectCache>(this);
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClosingClient += OnClosingClient;
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<IObjectCache>(this);
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClosingClient -= OnClosingClient;
        }

        public virtual void RegionLoaded(Scene scene)
        {
        }

        public virtual void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "ObjectCacheModule"; }
        }

        #region Events

        public void OnNewClient(IClientAPI client)
        {
            IScenePresence sp;
            client.Scene.TryGetScenePresence(client.AgentId, out sp);
            //Create the client's cache
            //This is shared, so all get saved into one file
            if (sp != null && !sp.IsChildAgent)
            {
                Util.FireAndForget(LoadFileOnNewClient, sp.UUID);
            }
        }

        /// <summary>
        /// Load the file for the client async so that we don't lock up the system for too long
        /// </summary>
        /// <param name="o"></param>
        public void LoadFileOnNewClient(object o)
        {
            UUID agentID = (UUID)o;
            LoadFromFileForClient(agentID);
        }

        public void OnClosingClient(IClientAPI client)
        {
            //Save the cache to the file for the client
            IScenePresence sp;
            client.Scene.TryGetScenePresence(client.AgentId, out sp);
            //This is shared, so all get saved into one file
            if (sp != null && !sp.IsChildAgent)
                SaveToFileForClient(client.AgentId);
            //Remove the client's cache
            lock (ObjectCacheAgents)
            {
                ObjectCacheAgents.Remove(client.AgentId);
            }
        }

        #endregion

        #region Serialization

        public string SerializeAgentCache(Dictionary<uint, uint> cache)
        {
            OSDMap cachedMap = new OSDMap();
            foreach (KeyValuePair<uint, uint> kvp in cache)
            {
                cachedMap.Add(kvp.Key.ToString(), OSD.FromUInteger(kvp.Value));
            }
            return OSDParser.SerializeJsonString(cachedMap);
        }

        public Dictionary<uint, uint> DeserializeAgentCache(string osdMap)
        {
            Dictionary<uint, uint> cache = new Dictionary<uint, uint>();
            try
            {
                OSDMap cachedMap = (OSDMap)OSDParser.DeserializeJson(osdMap);
                foreach (KeyValuePair<string, OSD> kvp in cachedMap)
                {
                    cache[uint.Parse(kvp.Key)] = kvp.Value.AsUInteger();
                }
            }
            catch
            {
                //It has an error, destroy the cache
                //null will tell the caller that it errored out and needs to be removed
                cache = null;
            }
            return cache;
        }

        #endregion

        #region Load/Save from file

        public void SaveToFileForClient(UUID AgentID)
        {
            Dictionary<uint, uint> cache;
            lock (ObjectCacheAgents)
            {
                if (!ObjectCacheAgents.ContainsKey(AgentID))
                    return;
                cache = new Dictionary<uint, uint>(ObjectCacheAgents[AgentID]);
            }
            FileStream stream = new FileStream(m_filePath + AgentID + ".oc", FileMode.OpenOrCreate);
            StreamWriter m_streamWriter = new StreamWriter(stream);
            m_streamWriter.WriteLine(SerializeAgentCache(cache));
            m_streamWriter.Close();
        }

        public void LoadFromFileForClient(UUID AgentID)
        {
            FileStream stream = new FileStream(m_filePath + AgentID + ".oc", FileMode.OpenOrCreate);
            StreamReader m_streamReader = new StreamReader(stream);
            string file = m_streamReader.ReadToEnd();
            m_streamReader.Close();
            //Read file here
            if (file != "") //New file
            {
                Dictionary<uint, uint> cache = DeserializeAgentCache(file);
                if (cache == null)
                {
                    //Something went wrong, delete the file
                    try
                    {
                        File.Delete (m_filePath + AgentID + ".oc");
                    }
                    catch
                    {
                    }
                    return;
                }
                lock (ObjectCacheAgents)
                {
                    ObjectCacheAgents[AgentID] = cache;
                }
            }
        }

        #endregion

        #endregion

        #region IObjectCache

        /// <summary>
        /// Check whether we can send a CachedObjectUpdate to the client
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="localID"></param>
        /// <param name="CurrentEntityCRC"></param>
        /// <returns></returns>
        public bool UseCachedObject(UUID AgentID, uint localID, uint CurrentEntityCRC)
        {
            lock (ObjectCacheAgents)
            {
                Dictionary<uint, uint> InternalCache;
                if(ObjectCacheAgents.TryGetValue(AgentID, out InternalCache))
                {
                    uint CurrentCachedCRC = 0;
                    if(InternalCache.TryGetValue(localID, out CurrentCachedCRC ))
                    {
                         if (CurrentEntityCRC == CurrentCachedCRC)
                         {
                             //The client knows of the newest version
                             return true;
                         }
                         //else, update below
                    }
                }
                else
                {
                    InternalCache = new Dictionary<uint, uint>();
                }
                InternalCache[localID] = CurrentEntityCRC;
                ObjectCacheAgents[AgentID] = InternalCache;
                return false;
            }
        }

        #endregion
    }
}
