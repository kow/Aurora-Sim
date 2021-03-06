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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Timers;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using Aurora.Framework;

namespace OpenSim.Framework
{
    public interface ISimulationBase
    {
        IHttpServer GetHttpServer(uint port);
        IConfigSource ConfigSource { get; set; }
        IRegistryCore ApplicationRegistry { get; }
        DateTime StartupTime { get; }
        AuroraEventManager EventManager { get; }
        string Version { get; }
        void RunStartupCommands();
        void RunCommandScript(string p);

        /// <summary>
        /// Shut down the simulation and close
        /// </summary>
        /// <param name="shouldForceExit">Runs Environment.Exit(0) if true</param>
        void Shutdown(bool shouldForceExit);

        /// <summary>
        /// Make a copy of the simulation base
        /// </summary>
        /// <returns></returns>
        ISimulationBase Copy();

        /// <summary>
        /// Start the base with the given parametsr
        /// </summary>
        /// <param name="originalConfigSource">The settings parsed from the command line</param>
        /// <param name="configSource">The .ini config</param>
        void Initialize(IConfigSource originalConfigSource, IConfigSource configSource);

        /// <summary>
        /// Start up any modules and run the HTTP server
        /// </summary>
        void Startup();

        /// <summary>
        /// Start console processing
        /// </summary>
        void Run();
    }
}
