﻿//#define BlockUnsupportedVersions
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using log4net.Config;
using Nini.Config;
using OpenSim.Framework;

namespace Aurora.Simulation.Base
{
    /// <summary>
    /// Starting class for the Aurora Server
    /// </summary>
    public class BaseApplication
    {
        /// <summary>
        /// Text Console Logger
        /// </summary>
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Save Crashes in the bin/crashes folder.  Configurable with m_crashDir
        /// </summary>
        public static bool m_saveCrashDumps = false;

        /// <summary>
        /// Should we send an error report?
        /// </summary>
        public static bool m_sendErrorReport = false;

        /// <summary>
        /// Where to post errors
        /// </summary>
        public static string m_urlToPostErrors = "http://auroraserver.ath.cx/posterror.php";

        /// <summary>
        /// Directory to save crash reports to.  Relative to bin/
        /// </summary>
        public static string m_crashDir = "crashes";

        //could move our main function into OpenSimMain and kill this class
        public static void BaseMain(string[] args, string defaultIniFile, ISimulationBase simBase)
        {
            // First line, hook the appdomain to the crash reporter
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

            // Add the arguments supplied when running the application to the configuration
            ArgvConfigSource configSource = new ArgvConfigSource(args);

            // Configure Log4Net
            configSource.AddSwitch("Startup", "logconfig");
            string logConfigFile = configSource.Configs["Startup"].GetString("logconfig", String.Empty);
            if (logConfigFile != String.Empty)
            {
                XmlConfigurator.Configure(new System.IO.FileInfo(logConfigFile));
                //m_log.InfoFormat("[OPENSIM MAIN]: configured log4net using \"{0}\" as configuration file",
                //                 logConfigFile);
            }
            else
            {
                XmlConfigurator.Configure();
                //m_log.Info("[OPENSIM MAIN]: configured log4net using default OpenSim.exe.config");
            }

            // Increase the number of IOCP threads available. Mono defaults to a tragically low number
            int workerThreads, iocpThreads;
            System.Threading.ThreadPool.GetMaxThreads(out workerThreads, out iocpThreads);
            //m_log.InfoFormat("[OPENSIM MAIN]: Runtime gave us {0} worker threads and {1} IOCP threads", workerThreads, iocpThreads);
            if (workerThreads < 500 || iocpThreads < 1000)
            {
                workerThreads = 500;
                iocpThreads = 1000;
                //m_log.Info("[OPENSIM MAIN]: Bumping up to 500 worker threads and 1000 IOCP threads");
                System.Threading.ThreadPool.SetMaxThreads(workerThreads, iocpThreads);
            }

            // Check if the system is compatible with OpenSimulator.
            // Ensures that the minimum system requirements are met
            m_log.Info("[Setup]: Performing compatibility checks... \n");
            string supported = String.Empty;
            if (Util.IsEnvironmentSupported(ref supported))
            {
                m_log.Info("[Setup]: Environment is compatible.\n");
            }
            else
            {
                m_log.Warn("[Setup]: Environment is unsupported (" + supported + ")\n");
                #if BlockUnsupportedVersions
                    Thread.Sleep(10000); //Sleep 10 seconds
                    return;
                #endif
            }

            // Configure nIni aliases and localles
            Culture.SetCurrentCulture();
            configSource.Alias.AddAlias("On", true);
            configSource.Alias.AddAlias("Off", false);
            configSource.Alias.AddAlias("True", true);
            configSource.Alias.AddAlias("False", false);

            ///Command line switches
            configSource.AddSwitch("Startup", "inifile");
            configSource.AddSwitch("Startup", "inimaster");
            configSource.AddSwitch("Startup", "inidirectory");
            configSource.AddSwitch("Console", "Console");
            configSource.AddSwitch("Startup", "inidbg");

            configSource.AddConfig("Network");

            IConfigSource m_configSource = Configuration(configSource, defaultIniFile);

            // Check if we're saving crashes
            m_saveCrashDumps = m_configSource.Configs["Startup"].GetBoolean("save_crashes", m_saveCrashDumps);

            // load Crash directory config
            m_crashDir = m_configSource.Configs["Startup"].GetString("crash_dir", m_crashDir);

            // check auto restart
            bool AutoRestart = m_configSource.Configs["Startup"].GetBoolean("AutoRestartOnCrash", true);

            //Set up the error reporting
            if (m_configSource.Configs["ErrorReporting"] != null)
            {
                m_sendErrorReport = m_configSource.Configs["ErrorReporting"].GetBoolean("SendErrorReports", true);
                m_urlToPostErrors = m_configSource.Configs["ErrorReporting"].GetString("ErrorReportingURL", m_urlToPostErrors);
            }

            bool Running = true;
            //If auto restart is set, then we always run.
            // otherwise, just run the first time that Running == true
            while (AutoRestart || Running)
            {
                //Always run once, then disable this
                Running = false;
                //Initialize the sim base now
                Startup(configSource, m_configSource, simBase.Copy());
            }
        }

        public static void Startup(ArgvConfigSource originalConfigSource, IConfigSource configSource, ISimulationBase simBase)
        {
            //Get it ready to run
            simBase.Initialize(originalConfigSource, configSource);
            try
            {
                //Start it. This starts ALL modules and completes the startup of the application
                simBase.Startup();
                //Run the console now that we are done
                simBase.Run();
            }
            catch (Exception ex)
            {
                if (ex.Message != "Restart") //Internal needs a restart message
                {
                    string mes = "[AURORA]: Aurora has crashed! Error: " + ex + ", Stack trace: " + ex.StackTrace;

                    m_log.Error(mes);
                    handleException(mes, ex);
                    //Just clean it out as good as we can
                    simBase.Shutdown(false);
                }
                //Then let it restart if it needs by sending it back up to 'while (AutoRestart || Running)' above
                return;
            }
            //If it didn't throw an error, it wants to quit
            Environment.Exit(0);
        }

        /// <summary>
        /// Load the configuration for the Application
        /// </summary>
        /// <param name="configSource"></param>
        /// <param name="defaultIniFile"></param>
        /// <returns></returns>
        private static IConfigSource Configuration(IConfigSource configSource, string defaultIniFile)
        {
            ConfigurationLoader m_configLoader = new ConfigurationLoader();
            if(defaultIniFile != "")
                m_configLoader.defaultIniFile = defaultIniFile;
            return m_configLoader.LoadConfigSettings(configSource);
        }

        private static bool _IsHandlingException = false; // Make sure we don't go recursive on ourself

        /// <summary>
        /// Global exception handler -- all unhandlet exceptions end up here :)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (_IsHandlingException)
                return;

            _IsHandlingException = true;

            string msg = String.Empty;
            msg += "\r\n";
            msg += "APPLICATION EXCEPTION DETECTED: " + e.ToString() + "\r\n";
            msg += "\r\n";

            msg += "Exception: " + e.ExceptionObject.ToString() + "\r\n";
            Exception ex = (Exception)e.ExceptionObject;
            if (ex.InnerException != null)
            {
                msg += "InnerException: " + ex.InnerException.ToString() + "\r\n";
            }

            msg += "\r\n";
            msg += "Application is terminating: " + e.IsTerminating.ToString() + "\r\n";

            m_log.ErrorFormat("[APPLICATION]: {0}", msg);

            handleException(msg, ex);

            _IsHandlingException = false;
        }

        /// <summary>
        /// Deal with sending the error to the error reporting service and saving the dump to the harddrive if needed
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="ex"></param>
        public static void handleException(string msg, Exception ex)
        {
            if (m_saveCrashDumps)
            {
                // Log exception to disk
                try
                {
                    if (!Directory.Exists(m_crashDir))
                        Directory.CreateDirectory(m_crashDir);

                    string log = Path.Combine(m_crashDir, Util.GetUniqueFilename ("crashDump" +
                        DateTime.Now.Day + DateTime.Now.Month + DateTime.Now.Year + ".mdmp"));
                    using (FileStream fs = new FileStream (log, FileMode.Create, FileAccess.ReadWrite, FileShare.Write))
                    {
                        MiniDump.Write (fs.SafeFileHandle, MiniDump.Option.WithThreadInfo | MiniDump.Option.WithProcessThreadData | MiniDump.Option.WithUnloadedModules | MiniDump.Option.WithHandleData | MiniDump.Option.WithDataSegs | MiniDump.Option.WithCodeSegs, MiniDump.ExceptionInfo.Present);
                    }
                }
                catch (Exception e2)
                {
                    m_log.ErrorFormat("[CRASH LOGGER CRASHED]: {0}", e2);
                }
            }

            if (m_sendErrorReport)
            {
                List<string> parameters = new List<string>();
                parameters.Add(VersionInfo.Version); //Aurora version
                parameters.Add(msg); //The error
                parameters.Add(Environment.OSVersion.Platform.ToString()); //The operating system
                ConfigurableKeepAliveXmlRpcRequest req;
                req = new ConfigurableKeepAliveXmlRpcRequest("SendErrorReport", parameters, true);
                try
                {
                    req.Send(m_urlToPostErrors, 10000);
                }
                catch
                {
                }
            }
        }
    }
    public static class MiniDump
    {
        // Taken almost verbatim from http://blog.kalmbach-software.de/2008/12/13/writing-minidumps-in-c/ 
        [Flags]
        public enum Option : uint
        {
            // From dbghelp.h: 
            Normal = 0x00000000,
            WithDataSegs = 0x00000001,
            WithFullMemory = 0x00000002,
            WithHandleData = 0x00000004,
            FilterMemory = 0x00000008,
            ScanMemory = 0x00000010,
            WithUnloadedModules = 0x00000020,
            WithIndirectlyReferencedMemory = 0x00000040,
            FilterModulePaths = 0x00000080,
            WithProcessThreadData = 0x00000100,
            WithPrivateReadWriteMemory = 0x00000200,
            WithoutOptionalData = 0x00000400,
            WithFullMemoryInfo = 0x00000800,
            WithThreadInfo = 0x00001000,
            WithCodeSegs = 0x00002000,
            WithoutAuxiliaryState = 0x00004000,
            WithFullAuxiliaryState = 0x00008000,
            WithPrivateWriteCopyMemory = 0x00010000,
            IgnoreInaccessibleMemory = 0x00020000,
            ValidTypeFlags = 0x0003ffff,
        };

        public enum ExceptionInfo
        {
            None,
            Present
        }

        //typedef struct _MINIDUMP_EXCEPTION_INFORMATION { 
        //    DWORD ThreadId; 
        //    PEXCEPTION_POINTERS ExceptionPointers; 
        //    BOOL ClientPointers; 
        //} MINIDUMP_EXCEPTION_INFORMATION, *PMINIDUMP_EXCEPTION_INFORMATION; 
        [StructLayout (LayoutKind.Sequential, Pack = 4)]  // Pack=4 is important! So it works also for x64! 
        public struct MiniDumpExceptionInformation
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            [MarshalAs (UnmanagedType.Bool)]
            public bool ClientPointers;
        }

        //BOOL 
        //WINAPI 
        //MiniDumpWriteDump( 
        //    __in HANDLE hProcess, 
        //    __in DWORD ProcessId, 
        //    __in HANDLE hFile, 
        //    __in MINIDUMP_TYPE DumpType, 
        //    __in_opt PMINIDUMP_EXCEPTION_INFORMATION ExceptionParam, 
        //    __in_opt PMINIDUMP_USER_STREAM_INFORMATION UserStreamParam, 
        //    __in_opt PMINIDUMP_CALLBACK_INFORMATION CallbackParam 
        //    ); 

        // Overload requiring MiniDumpExceptionInformation 
        [DllImport ("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump (IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType, ref MiniDumpExceptionInformation expParam, IntPtr userStreamParam, IntPtr callbackParam);

        // Overload supporting MiniDumpExceptionInformation == NULL 
        [DllImport ("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool MiniDumpWriteDump (IntPtr hProcess, uint processId, SafeHandle hFile, uint dumpType, IntPtr expParam, IntPtr userStreamParam, IntPtr callbackParam);

        [DllImport ("kernel32.dll", EntryPoint = "GetCurrentThreadId", ExactSpelling = true)]
        static extern uint GetCurrentThreadId ();

        public static bool Write (SafeHandle fileHandle, Option options, ExceptionInfo exceptionInfo)
        {
            Process currentProcess = Process.GetCurrentProcess ();
            IntPtr currentProcessHandle = currentProcess.Handle;
            uint currentProcessId = (uint)currentProcess.Id;
            MiniDumpExceptionInformation exp;
            exp.ThreadId = GetCurrentThreadId ();
            exp.ClientPointers = false;
            exp.ExceptionPointers = IntPtr.Zero;
            if (exceptionInfo == ExceptionInfo.Present)
            {
                exp.ExceptionPointers = System.Runtime.InteropServices.Marshal.GetExceptionPointers ();
            }
            bool bRet = false;
            if (exp.ExceptionPointers == IntPtr.Zero)
            {
                bRet = MiniDumpWriteDump (currentProcessHandle, currentProcessId, fileHandle, (uint)options, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                bRet = MiniDumpWriteDump (currentProcessHandle, currentProcessId, fileHandle, (uint)options, ref exp, IntPtr.Zero, IntPtr.Zero);
            }
            return bRet;
        }

        public static bool Write (SafeHandle fileHandle, Option dumpType)
        {
            return Write (fileHandle, dumpType, ExceptionInfo.None);
        }
    } 
}