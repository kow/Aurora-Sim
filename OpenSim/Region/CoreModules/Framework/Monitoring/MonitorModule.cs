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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.Monitoring.Alerts;
using OpenSim.Region.CoreModules.Framework.Monitoring.Monitors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Aurora.Simulation.Base;

namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    public class MonitorModule : IApplicationPlugin, IMonitorModule
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected Dictionary<string, MonitorRegistry> m_registry = new Dictionary<string, MonitorRegistry>();
        protected ISimulationBase m_simulationBase = null;

        #region Enums

        public enum Stats : uint
        {
            TimeDilation,
            FPS,
            PhysFPS,
            AgentUpdates,
            FrameMS,
            NetMS,
            SimOtherMS,
            SimPhysicsMS,
            AgentMS,
            ImagesMS,
            ScriptMS,
            TotalObjects,
            ActiveObjects,
            NumAgentMain,
            NumAgentChild,
            NumScriptActive,
            LSLIPS, //Lines per second
            InPPS,//Packets per second
            OutPPS,//Packets per second
            PendingDownloads,
            PendingUploads,
            VirtualSizeKB,
            ResidentSizeKB,
            PendingLocalUploads,
            TotalUnackedBytes,
            PhysicsPinnedTasks,
            PhysicsLODTasks,
            SimPhysicsStepMS,
            SimPhysicsShape,
            SimPhysicsOtherMS,
            SimPhysicsMemory,
            ScriptEPS,//Events per second
            SimSpareTime,
            SimSleepTime,
            IOPumpTime
        }

        #endregion

        #region Events

        public event SendStatResult OnSendStatsResult;

        #endregion

        public class MonitorRegistry
        {
            #region Declares

            protected Dictionary<string, IMonitor> m_monitors = new Dictionary<string, IMonitor>();
            protected Dictionary<string, IAlert> m_alerts = new Dictionary<string, IAlert>();
            protected Scene m_currentScene = null;
            private Timer m_report = new Timer();
            // Sending a stats update every 2 seconds-
            private int statsUpdatesEveryMS = 2000;
            // How long between the sending of stats updates in seconds
            private float statsUpdateFactor = 2;
            //The estate module to pull out the region flags
            private IEstateModule m_estateModule;
            private MonitorModule m_module;

            private float[] lastReportedSimStats = new float[35];
            /// <summary>
            /// The last reported stats for this region
            /// </summary>
            public float[] LastReportedSimStats
            {
                get { return lastReportedSimStats; }
            }
        
            #endregion

            #region Constructor

            /// <summary>
            /// Constructor, set the MonitorModule ref up
            /// </summary>
            /// <param name="module"></param>
            public MonitorRegistry(MonitorModule module)
            {
                m_module = module;
                for (int i = 0; i < sb.Length; i++)
                {
                    sb[i] = new SimStatsPacket.StatBlock();
                }
            }

            #endregion

            #region Add Region

            /// <summary>
            /// Set the scene for this instance, add the HTTP handler for the monitor stats,
            ///   add all the given monitors and alerts, and start the stats heartbeat.
            /// </summary>
            /// <param name="scene"></param>
            public void AddScene(Scene scene)
            {
                if (scene != null)
                {
                    m_currentScene = scene;
                    //Add the HTTP handler
                    MainServer.Instance.AddHTTPHandler("/monitorstats/" + scene.RegionInfo.RegionID + "/", StatsPage);
                    //Add all of the region monitors
                    AddRegionMonitors(scene);

                    //Set the correct update time
                    SetUpdateMS(2000);
                    //Start the stats heartbeat
                    m_report.AutoReset = true;
                    m_report.Interval = statsUpdatesEveryMS;
                    m_report.Elapsed += new ElapsedEventHandler(statsHeartBeat);
                    m_report.Enabled = true;
                }
                else //As we arn't a scene, we add all of the monitors that do not need the scene and run for the entire instance
                    AddDefaultMonitors();
            }

            public void Close()
            {
                if (m_currentScene != null)
                {
                    //Kill the stats heartbeat and http handler
                    m_report.Stop();
                    MainServer.Instance.RemoveHTTPHandler("POST", "/monitorstats/" + m_currentScene.RegionInfo.RegionID + "/");
                }
                //Remove all monitors/alerts
                m_alerts.Clear();
                m_monitors.Clear();
            }

            #endregion

            #region Default

            /// <summary>
            /// Add the monitors that are for the entire instance
            /// </summary>
            protected void AddDefaultMonitors()
            {
                AddMonitor(new AssetMonitor());
                AddMonitor(new GCMemoryMonitor());
                AddMonitor(new LoginMonitor());
                AddMonitor(new PWSMemoryMonitor());
                AddMonitor(new ThreadCountMonitor());
            }

            /// <summary>
            /// Add the monitors that are for each scene
            /// </summary>
            /// <param name="scene"></param>
            protected void AddRegionMonitors(Scene scene)
            {
                AddMonitor(new AgentCountMonitor(scene));
                AddMonitor(new AgentUpdateMonitor(scene));
                AddMonitor(new ChildAgentCountMonitor(scene));
                AddMonitor(new ImageFrameTimeMonitor(scene));
                AddMonitor(new LastFrameTimeMonitor(scene));
                AddMonitor(new NetworkMonitor(scene));
                AddMonitor(new ObjectCountMonitor(scene));
                AddMonitor(new OtherFrameMonitor(scene));
                AddMonitor(new ObjectUpdateMonitor(scene));
                AddMonitor(new PhysicsFrameMonitor(scene));
                AddMonitor(new PhysicsUpdateFrameMonitor(scene));
                AddMonitor(new PhysicsSyncFrameMonitor(scene));
                AddMonitor(new ScriptCountMonitor(scene));
                AddMonitor(new ScriptFrameTimeMonitor(scene));
                AddMonitor(new SimFrameMonitor(scene));
                AddMonitor(new SleepFrameMonitor(scene));
                AddMonitor(new TimeDilationMonitor(scene));
                AddMonitor(new TotalFrameMonitor(scene));

                AddAlert(new DeadlockAlert(GetMonitor("Last Completed Frame At") as LastFrameTimeMonitor));
            }

            #endregion

            #region Add/Remove/Get Monitor and Alerts

            /// <summary>
            /// Add a new monitor to the monitor list
            /// </summary>
            /// <param name="monitor"></param>
            public void AddMonitor(IMonitor monitor)
            {
                m_monitors.Add(monitor.GetName(), monitor);
            }

            /// <summary>
            /// Add a new alert
            /// </summary>
            /// <param name="alert"></param>
            public void AddAlert(IAlert alert)
            {
                alert.OnTriggerAlert += OnTriggerAlert;
                m_alerts.Add(alert.GetName(), alert);
            }

            /// <summary>
            /// Remove a known monitor from the list
            /// </summary>
            /// <param name="Name"></param>
            public void RemoveMonitor(string Name)
            {
                m_monitors.Remove(Name);
            }

            /// <summary>
            /// Remove a known alert from the list
            /// </summary>
            /// <param name="Name"></param>
            public void RemoveAlert(string Name)
            {
                m_alerts.Remove(Name);
            }

            /// <summary>
            /// Get a known monitor from the list, if not known, it will return null
            /// </summary>
            /// <param name="Name"></param>
            /// <returns></returns>
            public IMonitor GetMonitor(string Name)
            {
                if (m_monitors.ContainsKey(Name))
                    return m_monitors[Name];
                return null;
            }

            /// <summary>
            /// Gets a known alert from the list, if not known, it will return null
            /// </summary>
            /// <param name="Name"></param>
            /// <returns></returns>
            public IAlert GetAlert(string Name)
            {
                if (m_alerts.ContainsKey(Name))
                    return m_alerts[Name];
                return null;
            }

            #endregion

            #region Trigger Alert

            /// <summary>
            /// This occurs when the alert has been triggered and it alerts the console about it
            /// </summary>
            /// <param name="reporter"></param>
            /// <param name="reason"></param>
            /// <param name="fatal"></param>
            void OnTriggerAlert(System.Type reporter, string reason, bool fatal)
            {
                string regionName = m_currentScene != null ? " for " + m_currentScene.RegionInfo.RegionName : "";
                m_log.Error("[Monitor] " + reporter.Name + regionName + " reports " + reason + " (Fatal: " + fatal + ")");
            }

            #endregion

            #region Report

            /// <summary>
            /// Create a string that has the info from all monitors that we know of
            /// </summary>
            /// <returns></returns>
            public string Report()
            {
                string report = "";
                foreach (IMonitor monitor in m_monitors.Values)
                {
                    string regionName = m_currentScene != null ? m_currentScene.RegionInfo.RegionName + ": " : "";
                    report += regionName + monitor.GetName() + " reports " + monitor.GetFriendlyValue() + "\n";
                }
                return report;
            }

            #endregion

            #region HTTP Stats page

            /// <summary>
            /// Return a hashable of the monitors, or just the ones requested
            /// </summary>
            /// <param name="request"></param>
            /// <returns></returns>
            public Hashtable StatsPage(Hashtable request)
            {
                // If request was for a specific monitor
                // eg url/?monitor=Monitor.Name
                if (request.ContainsKey("monitor"))
                {
                    string monID = (string)request["monitor"];

                    foreach (IMonitor monitor in m_monitors.Values)
                    {
                        string elemName = monitor.ToString();
                        if (elemName.StartsWith(monitor.GetType().Namespace))
                            elemName = elemName.Substring(monitor.GetType().Namespace.Length + 1);
                        if (elemName == monID || monitor.ToString() == monID)
                        {
                            Hashtable ereply3 = new Hashtable();

                            ereply3["int_response_code"] = 404; // 200 OK
                            ereply3["str_response_string"] = monitor.GetValue().ToString();
                            ereply3["content_type"] = "text/plain";

                            return ereply3;
                        }
                    }

                    // No monitor with that name
                    Hashtable ereply2 = new Hashtable();

                    ereply2["int_response_code"] = 404; // 200 OK
                    ereply2["str_response_string"] = "No such monitor";
                    ereply2["content_type"] = "text/plain";

                    return ereply2;
                }

                string xml = "<data>";
                foreach (IMonitor monitor in m_monitors.Values)
                {
                    string elemName = monitor.ToString();
                    if (elemName.StartsWith(monitor.GetType().Namespace))
                        elemName = elemName.Substring(monitor.GetType().Namespace.Length + 1);

                    xml += "<" + elemName + ">" + monitor.GetValue() + "</" + elemName + ">";
                }
                xml += "</data>";

                Hashtable ereply = new Hashtable();

                ereply["int_response_code"] = 200; // 200 OK
                ereply["str_response_string"] = xml;
                ereply["content_type"] = "text/xml";

                return ereply;
            }

            #endregion

            #region Stats Heartbeat

            private SimStatsPacket.StatBlock[] sb = new SimStatsPacket.StatBlock[35];

            /// <summary>
            /// This is called by a timer and makes a SimStats class of the current stats that we have in this simulator.
            ///  It then sends the packet to the client and triggers the events to tell followers about the updated stats
            ///   and updates the LastSet* values for monitors.
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            protected void statsHeartBeat(object sender, EventArgs e)
            {
                SimStatsPacket.RegionBlock rb = new SimStatsPacket.RegionBlock();

                // Know what's not thread safe in Mono... modifying timers.
                lock (m_report)
                {
                    uint regionFlags = 0;

                    try
                    {
                        if (m_estateModule == null)
                            m_estateModule = m_currentScene.RequestModuleInterface<IEstateModule>();
                        regionFlags = m_estateModule != null ? m_estateModule.GetRegionFlags() : (uint)0;
                    }
                    catch (Exception)
                    {
                        // leave region flags at 0
                    }

                    rb.ObjectCapacity = (uint)m_currentScene.RegionInfo.ObjectCapacity;
                    rb.RegionFlags = regionFlags;
                    rb.RegionX = (uint)m_currentScene.RegionInfo.RegionLocX / Constants.RegionSize;
                    rb.RegionY = (uint)m_currentScene.RegionInfo.RegionLocY / Constants.RegionSize;

                    ISimFrameMonitor simFrameMonitor = (ISimFrameMonitor)GetMonitor("SimFrameStats");
                    ITimeDilationMonitor timeDilationMonitor = (ITimeDilationMonitor)GetMonitor("Time Dilation");
                    ITotalFrameTimeMonitor totalFrameMonitor = (ITotalFrameTimeMonitor)GetMonitor("Total Frame Time");
                    ITimeMonitor sleepFrameMonitor = (ITimeMonitor)GetMonitor("Sleep Frame Time");
                    ITimeMonitor otherFrameMonitor = (ITimeMonitor)GetMonitor("Other Frame Time");
                    IPhysicsFrameMonitor physicsFrameMonitor = (IPhysicsFrameMonitor)GetMonitor("Total Physics Frame Time");
                    ITimeMonitor physicsSyncFrameMonitor = (ITimeMonitor)GetMonitor("Physics Sync Frame Time");
                    ITimeMonitor physicsTimeFrameMonitor = (ITimeMonitor)GetMonitor("Physics Update Frame Time");
                    IAgentUpdateMonitor agentUpdateFrameMonitor = (IAgentUpdateMonitor)GetMonitor("Agent Update Count");
                    INetworkMonitor networkMonitor = (INetworkMonitor)GetMonitor("Network Monitor");
                    IMonitor imagesMonitor = GetMonitor("Images Frame Time");
                    ITimeMonitor scriptMonitor = (ITimeMonitor)GetMonitor("Script Frame Time");
                    IScriptCountMonitor totalScriptMonitor = (IScriptCountMonitor)GetMonitor("Total Script Count");
                    
                    #region various statistic googly moogly

                    float simfps = simFrameMonitor.SimFPS / statsUpdateFactor;
                    // save the reported value so there is something available for llGetRegionFPS 
                    simFrameMonitor.LastReportedSimFPS = simfps;

                    float physfps = (float)physicsFrameMonitor.PhysicsFPS / statsUpdateFactor;
                    physicsFrameMonitor.LastReportedPhysicsFPS = physfps;
                    //Update the time dilation with the newest physicsFPS
                    timeDilationMonitor.SetPhysicsFPS(physfps);

                    #endregion

                    #region Add the stats packets

                    //Some info on this packet http://wiki.secondlife.com/wiki/Statistics_Bar_Guide

                    sb[0].StatID = (uint)Stats.TimeDilation;
                    sb[0].StatValue = (float)timeDilationMonitor.GetValue();

                    sb[1].StatID = (uint)Stats.FPS;
                    sb[1].StatValue = simfps;

                    float realsimfps = simfps * 2;

                    sb[2].StatID = (uint)Stats.PhysFPS;
                    sb[2].StatValue = physfps;

                    sb[3].StatID = (uint)Stats.AgentUpdates;
                    sb[3].StatValue = (agentUpdateFrameMonitor.AgentUpdates / realsimfps);

                    sb[4].StatID = (uint)Stats.FrameMS;
                    float TotalFrames = (float)(totalFrameMonitor.GetValue() / realsimfps);
                    sb[4].StatValue = TotalFrames;

                    sb[5].StatID = (uint)Stats.NetMS;
                    sb[5].StatValue = 0;//TODO: Implement this

                    sb[6].StatID = (uint)Stats.SimOtherMS;
                    sb[6].StatValue = (float)(otherFrameMonitor.GetValue() / realsimfps);

                    sb[7].StatID = (uint)Stats.SimPhysicsMS;
                    float PhysicsMS = (float)(physicsTimeFrameMonitor.GetValue() / realsimfps);
                    sb[7].StatValue = PhysicsMS;

                    sb[8].StatID = (uint)Stats.AgentMS;
                    sb[8].StatValue = (agentUpdateFrameMonitor.AgentFrameTime / realsimfps);

                    sb[9].StatID = (uint)Stats.ImagesMS;
                    sb[9].StatValue = (float)(imagesMonitor.GetValue() / realsimfps);

                    sb[10].StatID = (uint)Stats.ScriptMS;
                    float ScriptMS = (float)(scriptMonitor.GetValue() / realsimfps);
                    sb[10].StatValue = ScriptMS;

                    sb[11].StatID = (uint)Stats.TotalObjects;
                    sb[12].StatID = (uint)Stats.ActiveObjects;
                    sb[13].StatID = (uint)Stats.NumAgentMain;
                    sb[14].StatID = (uint)Stats.NumAgentChild;

                    IEntityCountModule entityCountModule = m_currentScene.RequestModuleInterface<IEntityCountModule>();
                    if (entityCountModule != null)
                    {
                        sb[11].StatValue = entityCountModule.Objects;

                        sb[12].StatValue = entityCountModule.ActiveObjects;

                        sb[13].StatValue = entityCountModule.RootAgents;

                        sb[14].StatValue = entityCountModule.ChildAgents;
                    }

                    sb[15].StatID = (uint)Stats.NumScriptActive;
                    sb[15].StatValue = totalScriptMonitor.ActiveScripts;

                    sb[16].StatID = (uint)Stats.LSLIPS;
                    sb[16].StatValue = 0; //This isn't used anymore, and has been superseeded by LSLEPS

                    sb[17].StatID = (uint)Stats.InPPS;
                    sb[17].StatValue = (float)(networkMonitor.InPacketsPerSecond / statsUpdateFactor);

                    sb[18].StatID = (uint)Stats.OutPPS;
                    sb[18].StatValue = (float)(networkMonitor.OutPacketsPerSecond / statsUpdateFactor);

                    sb[19].StatID = (uint)Stats.PendingDownloads;
                    sb[19].StatValue = (float)(networkMonitor.PendingDownloads);

                    sb[20].StatID = (uint)Stats.PendingUploads;
                    sb[20].StatValue = (float)(networkMonitor.PendingUploads);

                    //21 and 22 are forced to the GC memory as they WILL make memory usage go up rapidly otherwise!
                    sb[21].StatID = (uint)Stats.VirtualSizeKB;
                    sb[21].StatValue = GC.GetTotalMemory(false) / 1024;// System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024);

                    sb[22].StatID = (uint)Stats.ResidentSizeKB;
                    sb[22].StatValue = GC.GetTotalMemory(false) / 1024;//(float)System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / (1024);
                    
                    sb[23].StatID = (uint)Stats.PendingLocalUploads;
                    sb[23].StatValue = (float)(networkMonitor.PendingUploads / statsUpdateFactor);
                    
                    sb[24].StatID = (uint)Stats.TotalUnackedBytes;
                    sb[24].StatValue = (float)(networkMonitor.UnackedBytes);

                    sb[25].StatID = (uint)Stats.PhysicsPinnedTasks;
                    sb[25].StatValue = 0;

                    sb[26].StatID = (uint)Stats.PhysicsLODTasks;
                    sb[26].StatValue = 0;

                    sb[27].StatID = (uint)Stats.SimPhysicsStepMS;
                    sb[27].StatValue = m_currentScene.PhysicsScene.StepTime;
                    
                    sb[28].StatID = (uint)Stats.SimPhysicsShape;
                    sb[28].StatValue = 0;

                    sb[29].StatID = (uint)Stats.SimPhysicsOtherMS;
                    sb[29].StatValue = (float)(physicsSyncFrameMonitor.GetValue() / realsimfps);

                    sb[30].StatID = (uint)Stats.SimPhysicsMemory;
                    sb[30].StatValue = 0;

                    sb[31].StatID = (uint)Stats.ScriptEPS;
                    sb[31].StatValue = totalScriptMonitor.ScriptEPS / statsUpdateFactor;
                    
                    sb[32].StatID = (uint)Stats.SimSpareTime;
                    //Spare time is the total time minus the stats that are in the same category in the client
                    // It is the sleep time, physics step, update physics shape, physics other, and pumpI0.
                    // Note: take out agent Update and script time for now, as they are not a part of the heartbeat right now and will mess this calc up
                    float SpareTime = (float)(TotalFrames - (/*NetMS + */ PhysicsMS + 
                        otherFrameMonitor.GetValue() + /*(agentUpdateFrameMonitor.AgentFrameTime / statsUpdateFactor) +*/
                        (imagesMonitor.GetValue() / statsUpdateFactor) /* + ScriptMS*/));
                    
                    sb[32].StatValue = SpareTime;

                    sb[33].StatID = (uint)Stats.SimSleepTime;
                    sb[33].StatValue = (float)(sleepFrameMonitor.GetValue() / realsimfps);

                    //Info about this stat: http://blogs.secondlife.com/message/66098
                    sb[34].StatID = (uint)Stats.IOPumpTime;
                    sb[34].StatValue = 0;//TODO: implement this

                    #endregion

                    for (int i = 0; i < sb.Length; i++)
                    {
                        if (float.IsInfinity(sb[i].StatValue) ||
                            float.IsNaN(sb[i].StatValue))
                            sb[i].StatValue = 0;
                        lastReportedSimStats[i] = sb[i].StatValue;
                    }

                    SimStats simStats
                        = new SimStats(rb, sb, m_currentScene.RegionInfo.RegionID);

                    //Fire the event and tell followers about the new stats
                    m_module.SendStatsResults(simStats);

                    //Tell all the scene presences about the new stats
                    foreach (IScenePresence agent in m_currentScene.GetScenePresences ())
                    {
                        if (!agent.IsChildAgent)
                            agent.ControllingClient.SendSimStats(simStats);
                    }
                    //Now fix any values that require reseting
                    ResetValues();
                }
            }

            /// <summary>
            /// Reset the values of the stats that require resetting (ones that use += and not =)
            /// </summary>
            public void ResetValues()
            {
                ITotalFrameTimeMonitor totalFrameMonitor = (ITotalFrameTimeMonitor)GetMonitor("Total Frame Time");
                totalFrameMonitor.ResetStats();
                SimFrameMonitor simMonitor = (SimFrameMonitor)GetMonitor("SimFrameStats");
                simMonitor.ResetStats();
                PhysicsFrameMonitor physMonitor = (PhysicsFrameMonitor)GetMonitor("Total Physics Frame Time");
                physMonitor.ResetStats();
                AgentUpdateMonitor agentUpdateMonitor = (AgentUpdateMonitor)GetMonitor("Agent Update Count");
                agentUpdateMonitor.ResetStats();
                ObjectUpdateMonitor objectUpdateMonitor = (ObjectUpdateMonitor)GetMonitor("PrimUpdates");
                objectUpdateMonitor.ResetStats();
                ImageFrameTimeMonitor imagesMonitor = (ImageFrameTimeMonitor)GetMonitor("Images Frame Time");
                imagesMonitor.ResetStats();
                ITimeMonitor physicsUpdateTime = (ITimeMonitor)GetMonitor("Physics Update Frame Time");
                physicsUpdateTime.ResetStats();
                ITimeMonitor physicsSyncTime = (ITimeMonitor)GetMonitor("Physics Sync Frame Time");
                physicsSyncTime.ResetStats();
                ITimeMonitor OtherFrameMonitor = (ITimeMonitor)GetMonitor("Other Frame Time");
                OtherFrameMonitor.ResetStats();
                ITimeMonitor SleepFrameMonitor = (ITimeMonitor)GetMonitor("Sleep Frame Time");
                SleepFrameMonitor.ResetStats();
                ScriptFrameTimeMonitor ScriptFrameTime = (ScriptFrameTimeMonitor)GetMonitor("Script Frame Time");
                ScriptFrameTime.ResetStats();
            }

            /// <summary>
            /// Set the correct update time for the timer
            /// </summary>
            /// <param name="ms"></param>
            public void SetUpdateMS(int ms)
            {
                statsUpdatesEveryMS = ms;
                statsUpdateFactor = (float)(statsUpdatesEveryMS / 1000);
                m_report.Interval = statsUpdatesEveryMS;
            }

            #endregion
        }
        
        #endregion

        #region Get

        /// <summary>
        /// Get a monitor from the given Key (RegionID or "" for the base instance) by Name
        /// </summary>
        /// <param name="Key"></param>
        /// <param name="Name"></param>
        /// <returns></returns>
        public IMonitor GetMonitor(string Key, string Name)
        {
            if (m_registry.ContainsKey(Key))
            {
                return m_registry[Key].GetMonitor(Name);
            }
            return null;
        }

        /// <summary>
        /// Get the latest region stats for the given regionID
        /// </summary>
        /// <param name="Key"></param>
        /// <returns></returns>
        public float[] GetRegionStats(string Key)
        {
            if (m_registry.ContainsKey(Key))
            {
                return m_registry[Key].LastReportedSimStats;
            }
            return new float[0];
        }

        #endregion

        #region Console

        /// <summary>
        /// This shows stats for ALL regions in the instance
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        protected void DebugMonitors(string module, string[] args)
        {
            //Dump all monitors to the console
            foreach (MonitorRegistry registry in m_registry.Values)
            {
                m_log.Info("[Stats] " + registry.Report());
                m_log.Info ("");
            }
        }

        /// <summary>
        /// This shows the stats for the given region
        /// </summary>
        /// <param name="module"></param>
        /// <param name="args"></param>
        protected void DebugMonitorsInCurrentRegion(string module, string[] args)
        {
            SceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<SceneManager>();
            if (manager != null)
            {
                //Dump the all instance one first
                m_log.Info("[Stats] " + m_registry[""].Report());
                m_log.Info ("");

                //Then dump for each scene
                manager.ForEachCurrentScene(delegate(Scene scene)
                {
                    m_log.Info("[Stats] " + m_registry[scene.RegionInfo.RegionID.ToString()].Report());
                    m_log.Info ("");
                });
            }
            else
            {
                //Dump all the monitors to the console
                DebugMonitors(module, args);
            }
        }

        protected void HandleShowThreads (string mod, string[] cmd)
        {
            m_log.Info (GetThreadsReport ());
        }

        protected void HandleShowUptime (string mod, string[] cmd)
        {
            m_log.Info (GetUptimeReport ());
        }

        protected void HandleShowStats (string mod, string[] cmd)
        {
            DebugMonitorsInCurrentRegion(mod, cmd);
        }

        protected void HandleShowQueues (string mod, string[] cmd)
        {
            List<string> args = new List<string> (cmd);
            args.RemoveAt (0);
            string[] showParams = args.ToArray ();
            m_log.Info (GetQueuesReport (showParams));
        }

        /// <summary>
        /// Print UDP Queue data for each client
        /// </summary>
        /// <returns></returns>
        protected string GetQueuesReport(string[] showParams)
        {
            bool showChildren = false;

            if (showParams.Length > 1 && showParams[1] == "full")
                showChildren = true;

            StringBuilder report = new StringBuilder();

            int columnPadding = 2;
            int maxNameLength = 18;
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;

            report.AppendFormat("{0,-" + maxNameLength + "}{1,-" + columnPadding + "}", "User", "");
            report.AppendFormat("{0,-" + maxRegionNameLength + "}{1,-" + columnPadding + "}", "Region", "");
            report.AppendFormat("{0,-" + maxTypeLength + "}{1,-" + columnPadding + "}", "Type", "");

            report.AppendFormat(
                "{0,9} {1,9} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Packets",
                "Packets",
                "Packets",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes",
                "Bytes");

            report.AppendFormat("{0,-" + totalInfoFieldsLength + "}", "");
            report.AppendFormat(
                "{0,9} {1,9} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Out",
                "In",
                "Unacked",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset",
                "State");

            SceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<SceneManager>();
            if (manager != null)
            {
                manager.ForEachScene(
                    delegate(Scene scene)
                    {
                        scene.ForEachClient(
                            delegate(IClientAPI client)
                            {
                                if (client is IStatsCollector)
                                {
                                    IScenePresence SP = scene.GetScenePresence (client.AgentId);
                                    if (SP == null || (SP.IsChildAgent && !showChildren))
                                        return;

                                    string name = client.Name;
                                    string regionName = scene.RegionInfo.RegionName;

                                    report.AppendFormat(
                                        "{0,-" + maxNameLength + "}{1,-" + columnPadding + "}",
                                        name.Length > maxNameLength ? name.Substring(0, maxNameLength) : name, "");
                                    report.AppendFormat(
                                        "{0,-" + maxRegionNameLength + "}{1,-" + columnPadding + "}",
                                        regionName.Length > maxRegionNameLength ? regionName.Substring(0, maxRegionNameLength) : regionName, "");
                                    report.AppendFormat(
                                        "{0,-" + maxTypeLength + "}{1,-" + columnPadding + "}",
                                        SP.IsChildAgent ? "Child" : "Root", "");

                                    IStatsCollector stats = (IStatsCollector)client;

                                    report.AppendLine(stats.Report());
                                }
                            });
                    });
            }

            return report.ToString();
        }

        #endregion

        #region Diagonistics

        /// <summary>
        /// Print statistics to the logfile, if they are active
        /// </summary>
        private void LogDiagnostics(object source, ElapsedEventArgs e)
        {
            m_log.Debug(LogDiagnostics());
        }

        public string LogDiagnostics()
        {
            StringBuilder sb = new StringBuilder("DIAGNOSTICS\n\n");
            sb.Append(GetUptimeReport());

            foreach (MonitorRegistry registry in m_registry.Values)
            {
                sb.Append(registry.Report());
            }

            sb.Append(Environment.NewLine);
            sb.Append(GetThreadsReport());

            return sb.ToString();
        }

        public ProcessThreadCollection GetThreads()
        {
            Process thisProc = Process.GetCurrentProcess();
            return thisProc.Threads;
        }

        /// <summary>
        /// Get a report about the registered threads in this server.
        /// </summary>
        public string GetThreadsReport()
        {
            StringBuilder sb = new StringBuilder();

            ProcessThreadCollection threads = GetThreads();
            if (threads == null)
            {
                sb.Append("OpenSim thread tracking is only enabled in DEBUG mode.");
            }
            else
            {
                sb.Append(threads.Count + " threads are being tracked:" + Environment.NewLine);
                foreach (ProcessThread t in threads)
                {
                    sb.Append("ID: " + t.Id + ", TotalProcessorTime: " + t.TotalProcessorTime + ", TimeRunning: " +
                        (DateTime.Now - t.StartTime) + ", Pri: " + t.CurrentPriority + ", State: " + t.ThreadState);
                    if (t.ThreadState == System.Diagnostics.ThreadState.Wait)
                        sb.Append(", Reason: " + t.WaitReason + Environment.NewLine);
                    else
                        sb.Append(Environment.NewLine);

                }
            }
            int workers = 0, ports = 0, maxWorkers = 0, maxPorts = 0;
            System.Threading.ThreadPool.GetAvailableThreads(out workers, out ports);
            System.Threading.ThreadPool.GetMaxThreads(out maxWorkers, out maxPorts);

            sb.Append(Environment.NewLine + "*** ThreadPool threads ***" + Environment.NewLine);
            sb.Append("workers: " + (maxWorkers - workers) + " (" + maxWorkers + "); ports: " + (maxPorts - ports) + " (" + maxPorts + ")" + Environment.NewLine);

            return sb.ToString();
        }

        /// <summary>
        /// Return a report about the uptime of this server
        /// </summary>
        /// <returns></returns>
        public string GetUptimeReport()
        {
            StringBuilder sb = new StringBuilder(String.Format("Time now is {0}\n", DateTime.Now));
            sb.Append(String.Format("Server has been running since {0}, {1}\n", m_simulationBase.StartupTime.DayOfWeek, m_simulationBase.StartupTime));
            sb.Append(String.Format("That is an elapsed time of {0}\n", DateTime.Now - m_simulationBase.StartupTime));

            return sb.ToString();
        }

        #endregion

        #region IApplicationPlugin Members

        public void Initialize(ISimulationBase simulationBase)
        {
            m_simulationBase = simulationBase;

            Timer PeriodicDiagnosticsTimer = new Timer(60 * 60 * 1000); // One hour
            PeriodicDiagnosticsTimer.Elapsed += LogDiagnostics;
            PeriodicDiagnosticsTimer.Enabled = true;
            PeriodicDiagnosticsTimer.Start ();
            MainConsole.Instance.Commands.AddCommand ("show threads", "show threads", "List tracked threads", HandleShowThreads);
            MainConsole.Instance.Commands.AddCommand ("show uptime", "show uptime", "Show server startup time and uptime", HandleShowUptime);
            MainConsole.Instance.Commands.AddCommand ("show queues", "show queues [full]", "Shows the queues for the given agent (if full is given as a parameter, child agents are displayed as well)", HandleShowQueues);
            MainConsole.Instance.Commands.AddCommand ("show stats", "show stats", "Show statistical information for this server", HandleShowStats);
            
            MainConsole.Instance.Commands.AddCommand("Stats", false, "stats report",
                               "stats report",
                               "Returns a variety of statistics about the current region and/or simulator",
                               DebugMonitors);

            MonitorRegistry reg = new MonitorRegistry(this);
            //This registers the default commands, but not region specific ones
            reg.AddScene(null);
            m_registry.Add("", reg);

            m_simulationBase.ApplicationRegistry.RegisterModuleInterface<IMonitorModule>(this);
        }

        public void ReloadConfiguration(IConfigSource config)
        {
        }

        public void PostInitialise()
        {
            SceneManager manager = m_simulationBase.ApplicationRegistry.RequestModuleInterface<SceneManager>();
            if (manager != null)
            {
                manager.OnAddedScene += OnAddedScene;
                manager.OnCloseScene += OnCloseScene;
            }
        }

        public void OnAddedScene(Scene scene)
        {
            if (m_registry.ContainsKey(scene.RegionInfo.RegionID.ToString()))
            {
                //Kill the old!
                m_registry[scene.RegionInfo.RegionID.ToString()].Close();
                m_registry.Remove(scene.RegionInfo.RegionID.ToString());
            }
            //Register all the commands for this region
            MonitorRegistry reg = new MonitorRegistry(this);
            reg.AddScene(scene);
            m_registry[scene.RegionInfo.RegionID.ToString()] = reg;
            scene.RegisterModuleInterface<IMonitorModule>(this);
        }

        public void OnCloseScene(Scene scene)
        {
            m_registry.Remove(scene.RegionInfo.RegionID.ToString());
        }

        public void Start()
        {
        }

        public void PostStart()
        {
        }

        public void Dispose()
        {
        }

        public string Name
        {
            get { return "StatsModule"; }
        }

        public void Close()
        {
        }

        #endregion

        #region Event Handlers

        public void SendStatsResults(SimStats simStats)
        {
            SendStatResult handlerSendStatResult = OnSendStatsResult;
            if (handlerSendStatResult != null)
            {
                handlerSendStatResult(simStats);
            }
        }

        #endregion
    }
}
