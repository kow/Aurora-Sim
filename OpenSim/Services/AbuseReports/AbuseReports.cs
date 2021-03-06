﻿/*
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

using OpenMetaverse;
using OpenSim.Framework;
using System;
using System.Collections.Generic;
using OpenSim.Services.Interfaces;
using OpenSim.Data;
using Nini.Config;
using log4net;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using Aurora.Framework;
using Aurora.Simulation.Base;

namespace OpenSim.Services.AbuseReports
{
    public class AbuseReports : IAbuseReports, IService
    {
        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            string dllName = String.Empty;
            string connString = String.Empty;

            //
            // Try reading the [AbuseReportsService] section first, if it exists
            //
            IConfig AbuseReportsConfig = config.Configs["AbuseReportsService"];
            if (AbuseReportsConfig != null)
            {
                dllName = AbuseReportsConfig.GetString("StorageProvider", dllName);
                connString = AbuseReportsConfig.GetString("ConnectionString", connString);
            }

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (String.Empty.Equals(dllName))
                throw new Exception("No StorageProvider configured");


            registry.RegisterModuleInterface<IAbuseReports>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
        }

        public void FinishedStartup()
        {
        }

        public string Name
        {
            get { return GetType().Name; }
        }

        public void AddAbuseReport(AbuseReport abuse_report)
        {
            IAbuseReportsConnector conn = Aurora.DataManager.DataManager.RequestPlugin<IAbuseReportsConnector>();
            if (conn != null)
                conn.AddAbuseReport(abuse_report);
        }

        public AbuseReport GetAbuseReport(int Number, string Password)
        {
            IAbuseReportsConnector conn = Aurora.DataManager.DataManager.RequestPlugin<IAbuseReportsConnector>();
            if (conn != null)
                return conn.GetAbuseReport(Number, Password);
            else
                return null;
        }

        public void UpdateAbuseReport(AbuseReport report, string Password)
        {
            IAbuseReportsConnector conn = Aurora.DataManager.DataManager.RequestPlugin<IAbuseReportsConnector>();
            if (conn != null)
                conn.UpdateAbuseReport(report, Password);
        }

        public List<AbuseReport> GetAbuseReports(int start, int count, string filter)
        {
            IAbuseReportsConnector conn = Aurora.DataManager.DataManager.RequestPlugin<IAbuseReportsConnector>();
            if (conn != null)
            {
                return conn.GetAbuseReports(start, count, filter);
            }
            else
                return null;
        }
    }
}
