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
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.IO;
using System.Text;
using Microsoft.CSharp;
//using Microsoft.JScript;
using Microsoft.VisualBasic;
using log4net;
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;

namespace Aurora.ScriptEngine.AuroraDotNetEngine.CompilerTools
{
    public class Compiler
    {
        #region Declares

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // * Uses "LSL2Converter" to convert LSL to C# if necessary.
        // * Compiles C#-code into an assembly
        // * Returns assembly name ready for AppDomain load.
        //
        // Assembly is compiled using LSL_BaseClass as base. Look at debug C# code file created when LSL script is compiled for full details.
        //

        private string DefaultCompileLanguage;
        private bool WriteScriptSourceToDebugFile;
        private bool CompileWithDebugInformation;
        public Dictionary<string, IScriptConverter> AllowedCompilers = new Dictionary<string, IScriptConverter>(StringComparer.CurrentCultureIgnoreCase);
        List<IScriptConverter> converters = new List<IScriptConverter>();
        private Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> PositionMap;

        public bool firstStartup = true;
        private string FilePrefix;

        private List<string> m_warnings = new List<string>();

        private List<string> m_errors = new List<string>();

        private List<string> m_referencedFiles = new List<string>();

        private static UInt64 scriptCompileCounter = 0; // And a counter

        public UInt64 ScriptCompileCounter
        {
            get { return scriptCompileCounter; }
        }

        private ScriptEngine m_scriptEngine;

        public ScriptEngine ScriptEngine
        {
            get { return m_scriptEngine; }
        }

        #endregion

        #region Setup

        public Compiler(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
            ReadConfig();
            SetupApis();
        }

        private void ReadConfig()
        {
            // Get some config
            WriteScriptSourceToDebugFile = m_scriptEngine.Config.GetBoolean("WriteScriptSourceToDebugFile", false);
            CompileWithDebugInformation = m_scriptEngine.Config.GetBoolean("CompileWithDebugInformation", true);

            MakeFilePrefixSafe();
            //Set up the compilers
            SetupCompilers();
            //Find the default compiler
            FindDefaultCompiler();
        }

        private void MakeFilePrefixSafe()
        {
            // Get file prefix from scriptengine name and make it file system safe:
            FilePrefix = "CommonCompiler";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                FilePrefix = FilePrefix.Replace(c, '_');
            }
        }

        private void FindDefaultCompiler()
        {
            // Allowed compilers
            string allowComp = m_scriptEngine.Config.GetString("AllowedCompilers", "lsl");
            AllowedCompilers.Clear();

            // Default language
            DefaultCompileLanguage = m_scriptEngine.Config.GetString("DefaultCompileLanguage", "lsl").ToLower();

            bool found = false;
            foreach (string strl in allowComp.Split(','))
            {
                string strlan = strl.Trim(" \t".ToCharArray()).ToLower();
                foreach (IScriptConverter converter in converters)
                {
                    if (converter.Name == strlan)
                    {
                        AllowedCompilers.Add(strlan, converter);
                        if (converter.Name == DefaultCompileLanguage)
                        {
                            found = true;
                        }
                    }
                }
            }
            if (AllowedCompilers.Count == 0)
                m_log.Error("[Compiler]: Config error. Compiler could not recognize any language in \"AllowedCompilers\". Scripts will not be executed!");
            
            if (!found)
                m_log.Error("[Compiler]: " +
                                            "Config error. Default language \"" + DefaultCompileLanguage + "\" specified in \"DefaultCompileLanguage\" is not recognized as a valid language. Changing default to: \"lsl\".");

            // Is this language in allow-list?
            if (!AllowedCompilers.ContainsKey(DefaultCompileLanguage))
            {
                m_log.Error("[Compiler]: " +
                            "Config error. Default language \"" + DefaultCompileLanguage + "\"specified in \"DefaultCompileLanguage\" is not in list of \"AllowedCompilers\". Scripts may not be executed!");
            }

            // We now have an allow-list, a mapping list, and a default language
        }

        private void SetupCompilers()
        {
            converters = Aurora.Framework.AuroraModuleLoader.PickupModules<IScriptConverter>();
            foreach (IScriptConverter convert in converters)
            {
                convert.Initialise(this);
            }
        }

        private void SetupApis()
        {
            //Get all of the Apis that are allowed (this does check for it)
            IScriptApi[] apis = m_scriptEngine.GetAPIs();
            //Now we need to pull the files they will need to access from them
            foreach (IScriptApi api in apis)
            {
                m_referencedFiles.AddRange(api.ReferencedAssemblies);
            }
        }

        #endregion

        #region Compile script

        /// <summary>
        /// Converts script (if needed) and compiles
        /// </summary>
        /// <param name="Script">LSL script</param>
        /// <returns>Filename to .dll assembly</returns>
        public void PerformScriptCompile(string Script, UUID itemID, UUID ownerUUID, int VersionID, out string assembly)
        {
            assembly = "";
            
            if (Script == String.Empty)
            {
                AddError("No script text present");
                return;
            }

            m_warnings.Clear();
            m_errors.Clear();

            UUID assemblyGuid = UUID.Random();

//            assembly = CheckDirectories(FilePrefix + "_compiled_" + itemID.ToString() + "V" + VersionID + ".dll", itemID);

            assembly = CheckDirectories(assemblyGuid.ToString() + ".dll", assemblyGuid);          

            IScriptConverter converter;
            string compileScript;
            CheckLanguageAndConvert(Script, ownerUUID, out converter, out compileScript);
            if (GetErrors().Length != 0)
                return;
            if (converter == null)
            {
                AddError("No Compiler found for this type of script.");
                return;
            }

            CompileFromDotNetText(compileScript, converter, assembly, Script, false);
        }

        /// <summary>
        /// Converts script (if needed) and compiles into memory
        /// </summary>
        /// <param name="Script"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public void PerformInMemoryScriptCompile(string Script, UUID itemID)
        {
            if (Script == String.Empty)
            {
                AddError("No script text present");
                return;
            } 

            m_warnings.Clear();
            m_errors.Clear();

            IScriptConverter converter;
            string compileScript;
            CheckLanguageAndConvert(Script, UUID.Zero, out converter, out compileScript);
            if (GetErrors().Length != 0)
                return;
            if (converter == null)
            {
                AddError("No Compiler found for this type of script.");
                return;
            } 

            CompileFromDotNetText(compileScript, converter, "", Script, true);
        }

        public string FindDefaultStateForScript (string Script)
        {
            return FindConverterForScript(Script).DefaultState;
        }

        public IScriptConverter FindConverterForScript(string Script)
        {
            IScriptConverter language = null;
            foreach (IScriptConverter convert in converters)
            {
                if (convert.Name == DefaultCompileLanguage)
                {
                    language = convert;
                    break;
                }
            }
            foreach (IScriptConverter convert in converters)
            {
                if (Script.StartsWith ("//" + convert.Name, true, CultureInfo.InvariantCulture))
                    language = convert;
            }

            return language;
        }

        private void CheckLanguageAndConvert(string Script, UUID ownerID, out IScriptConverter converter, out string compileScript)
        {
            compileScript = Script;
            converter = null;
            string language = DefaultCompileLanguage;

            foreach (IScriptConverter convert in converters)
            {
                if (Script.StartsWith("//" + convert.Name, true, CultureInfo.InvariantCulture))
                    language = convert.Name;
            }

            if (!AllowedCompilers.ContainsKey(language))
            {
                // Not allowed to compile to this language!
                AddError("The compiler for language \"" + language.ToString() + "\" is not in list of allowed compilers. Script will not be executed!");

                return;
            }

            if (m_scriptEngine.Worlds[0].Permissions.CanCompileScript(ownerID, language) == false)
            {
                // Not allowed to compile to this language!
                AddError(ownerID + " is not in list of allowed users for this scripting language. Script will not be executed!");
                return;
            }

            AllowedCompilers.TryGetValue(language, out converter);

            converter.Convert(Script, out compileScript, out PositionMap);
        }

        public void RecreateDirectory()
        {
            if (Directory.Exists(m_scriptEngine.ScriptEnginesPath))
            {
                string[] directories = Directory.GetDirectories(m_scriptEngine.ScriptEnginesPath);
                foreach (string dir in directories)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception) { }
                }
            }
            if (!Directory.Exists(m_scriptEngine.ScriptEnginesPath))
            {
                try
                {
                    Directory.CreateDirectory(m_scriptEngine.ScriptEnginesPath);
                }
                catch (Exception) { }
            }
        }
        
        private string CheckDirectories(string assembly, UUID itemID)
        {
            string dirName = itemID.ToString().Substring(0, 3);
            if (!Directory.Exists(m_scriptEngine.ScriptEnginesPath))
            {
                try
                {
                    Directory.CreateDirectory(m_scriptEngine.ScriptEnginesPath);
                }
                catch (Exception)
                {
                }
            }
            if (!Directory.Exists(Path.Combine(m_scriptEngine.ScriptEnginesPath, dirName)))
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(m_scriptEngine.ScriptEnginesPath, dirName));
                }
                catch (Exception)
                {
                }
            }
            assembly = Path.Combine(Path.Combine(m_scriptEngine.ScriptEnginesPath, dirName), assembly);
            assembly = CheckAssembly(assembly, 0);
            return assembly;
        }

        private string CheckAssembly(string assembly, int i)
        {
            if (File.Exists(assembly) || File.Exists(assembly.Remove(assembly.Length - 4) + ".pdb"))
            {
                try
                {
                    File.Delete(assembly);
                    File.Delete(assembly.Remove(assembly.Length - 4) + ".pdb");
                }
                catch (Exception) // NOTLEGIT - Should be just FileIOException
                {
                    assembly = assembly.Remove(assembly.Length - 4);
                    assembly += "A.dll";
                    i++;
                    return CheckAssembly(assembly, i);
                }
            }
            return assembly;
        }

        public string[] GetWarnings()
        {
            return m_warnings.ToArray();
        }

        public void AddWarning(string warning)
        {
            if (!m_warnings.Contains(warning))
            {
                m_warnings.Add(warning);
            }
        }

        public string[] GetErrors()
        {
            return m_errors.ToArray();
        }

        public void AddError(string error)
        {
            if (!m_errors.Contains(error))
            {
                m_errors.Add(error);
            }
        }

        /// <summary>
        /// Compile .NET script to .Net assembly (.dll)
        /// </summary>
        /// <param name="Script">CS script</param>
        /// <returns>Filename to .dll assembly</returns>
        internal void CompileFromDotNetText(string Script, IScriptConverter converter, string assembly, string originalScript, bool inMemory)
        {
            string ext = "." + converter.Name;

            if (!inMemory)
            {
                // Output assembly name
                scriptCompileCounter++;
                try
                {
                    File.Delete(assembly);
                }
                catch (Exception e) // NOTLEGIT - Should be just FileIOException
                {
                    AddError("Unable to delete old existing " +
                            "script-file before writing new. Compile aborted: " +
                            e.ToString());
                    return;
                }

                // DEBUG - write source to disk
                if (WriteScriptSourceToDebugFile)
                {
                    string srcFileName = Path.GetFileNameWithoutExtension(assembly) + ext;
                    try
                    {
                        File.WriteAllText(Path.Combine(m_scriptEngine.ScriptEnginesPath,
                            srcFileName), Script);
                    }
                    catch (Exception ex) //NOTLEGIT - Should be just FileIOException
                    {
                        m_log.Error("[Compiler]: Exception while " +
                                    "trying to write script source to file \"" +
                                    srcFileName + "\": " + ex.ToString());
                    }
                }
            }

            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            parameters.IncludeDebugInformation = true;

            string rootPath =
                            Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "Aurora.ScriptEngine.AuroraDotNetEngine.dll"));
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                    "OpenSim.Framework.dll"));

            if (converter.Name == "yp")
            {
                parameters.ReferencedAssemblies.Add(Path.Combine(rootPath,
                        "OpenSim.Region.ScriptEngine.Shared.YieldProlog.dll"));
            }

            parameters.ReferencedAssemblies.AddRange(m_referencedFiles.ToArray());

            parameters.GenerateExecutable = false;
            parameters.GenerateInMemory = inMemory;
            parameters.OutputAssembly = assembly;
            parameters.IncludeDebugInformation = CompileWithDebugInformation;
            //parameters.WarningLevel = 1; // Should be 4?
            parameters.TreatWarningsAsErrors = false;

            CompilerResults results = converter.Compile(parameters, Script);
            parameters = null;
            //
            // WARNINGS AND ERRORS
            //

            if (results.Errors.Count > 0)
            {
                string srcFileName = FilePrefix + "_source_" +
                        Path.GetFileNameWithoutExtension(assembly) + ext;
                try
                {
                    File.WriteAllText(Path.Combine(m_scriptEngine.ScriptEnginesPath,
                        srcFileName), Script);
                }
                catch (Exception ex) //NOTLEGIT - Should be just FileIOException
                {
                    m_log.Error("[Compiler]: Exception while " +
                                "trying to write script source to file \"" +
                                srcFileName + "\": " + ex.ToString());
                }

                foreach (CompilerError CompErr in results.Errors)
                {
                    string severity = CompErr.IsWarning ? "Warning" : "Error";
                
                    KeyValuePair<int, int> lslPos;

                    // Show 5 errors max, but check entire list for errors

                    string errtext = String.Empty;

                    if (severity == "Error")
                    {
                        string text = CompErr.ErrorText;
                        int LineN = 0;
                        int CharN = 0;
                        // Use LSL type names
                        if (converter.Name == "lsl")
                        {
                            text = ReplaceTypes(CompErr.ErrorText);
                            text = CleanError(text);
                            lslPos = FindErrorPosition(CompErr.Line, CompErr.Column, PositionMap);
                            LineN = lslPos.Key - 1;
                            CharN = lslPos.Value - 1;
                            if (LineN <= 0 && CharN != 0)
                            {
                                string[] lines = originalScript.Split('\n');
                                int charCntr = 0;
                                int lineCntr = 0;
                                foreach(string line in lines)
                                {
                                    if (charCntr + line.Length > CharN)
                                    {
                                        //Its in this line
                                        CharN -= charCntr;
                                        LineN = lineCntr;
                                        break;
                                    }
                                    charCntr += line.Length - 1;
                                    lineCntr++;
                                }
                            }
                        }
                        else
                        {
                            LineN = CompErr.Line;
                            CharN = CompErr.Column;
                        }

                        //This will crash some viewers if the pos is 0,0!
                        if (LineN <= 0 && CharN <= 0)
                        {
                            CharN = 1;
                            LineN = 1;
                        }
                            

                        // The Second Life viewer's script editor begins
                        // countingn lines and columns at 0, so we subtract 1.
                        errtext += String.Format("({0},{1}): {3}: {2}\n",
                                LineN, CharN, text, severity);
                        AddError(errtext);
                    }
                    else
                    {
                        string text = CompErr.ErrorText;
                        int LineN = 0;
                        int CharN = 0;
                        // Use LSL type names
                        if (converter.Name == "lsl")
                        {
                            text = ReplaceTypes(CompErr.ErrorText);
                            text = CleanError(text);
                            lslPos = FindErrorPosition(CompErr.Line, CompErr.Column, PositionMap);
                            LineN = lslPos.Key - 1;
                            CharN = lslPos.Value - 1;
                            if (LineN <= 0 && CharN != 0)
                            {
                                string[] lines = originalScript.Split('\n');
                                int charCntr = 0;
                                int lineCntr = 0;
                                foreach(string line in lines)
                                {
                                    if (charCntr + line.Length > CharN)
                                    {
                                        //Its in this line
                                        CharN -= charCntr;
                                        LineN = lineCntr;
                                        break;
                                    }
                                    charCntr += line.Length - 1;
                                    lineCntr++;
                                }
                            }
                        }
                        else
                        {
                            LineN = CompErr.Line;
                            CharN = CompErr.Column;
                        }

                        //This will crash some viewers if the pos is 0,0!
                        if (LineN == 0 && CharN == 0)
                        {
                            CharN = 1;
                            LineN = 1;
                        }

                        // The Second Life viewer's script editor begins
                        // countingn lines and columns at 0, so we subtract 1.
                        errtext += String.Format("({0},{1}): {3}: {2}\n",
                                LineN, CharN, text, severity);
                        AddWarning(errtext);
                    }
                }
            }
            results = null;
            if (m_errors.Count != 0) // Quit early then
                return;

            //  On today's highly asynchronous systems, the result of
            //  the compile may not be immediately apparent. Wait a 
            //  reasonable amount of time before giving up on it.

            if (!inMemory)
            {
                if (!File.Exists(assembly))
                {
                    for (int i = 0; i < 500 && !File.Exists(assembly); i++)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                    AddError("No compile error. But not able to locate compiled file.");
                }
            }
        }

        internal void FinishCompile (ScriptData scriptData, IScript Script)
        {
            FindConverterForScript (scriptData.Source).FinishCompile (m_scriptEngine, scriptData, Script);
        }

        private class kvpSorter : IComparer<KeyValuePair<int, int>>
        {
            public int Compare(KeyValuePair<int, int> a,
                    KeyValuePair<int, int> b)
            {
                return a.Key.CompareTo(b.Key);
            }
        }

        public static KeyValuePair<int, int> FindErrorPosition(int line,
                int col, Dictionary<KeyValuePair<int, int>,
                KeyValuePair<int, int>> positionMap)
        {
            if (positionMap == null || positionMap.Count == 0)
                return new KeyValuePair<int, int>(line, col);

            KeyValuePair<int, int> ret = new KeyValuePair<int, int>();

             if (positionMap.TryGetValue(new KeyValuePair<int, int>(line, col),
                    out ret))
                return ret;

            List<KeyValuePair<int, int>> sorted =
                    new List<KeyValuePair<int, int>>(positionMap.Keys);

            sorted.Sort(new kvpSorter());

            int l = 1;
            int c = 1;

            foreach (KeyValuePair<int, int> cspos in sorted)
            {
                if (cspos.Key >= line)
                {
                    if (cspos.Key > line)
                        return new KeyValuePair<int, int>(l, c);
                    if (cspos.Value > col)
                        return new KeyValuePair<int, int>(l, c);
                    c = cspos.Value;
                    if (c == 0)
                        c++;
                }
                else
                {
                    l = cspos.Key;
                }
            }
            return new KeyValuePair<int, int>(l, c);
        }

        string ReplaceTypes(string message)
        {
            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLString",
                    "string");

            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLInteger",
                    "integer");

            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.LSLFloat",
                    "float");

            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.list",
                    "list");

            return message;
        }

        string CleanError(string message)
        {
            //Remove these long strings
            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.Runtime.ScriptBaseClass.",
                    "");
            message = message.Replace(
                    "Aurora.ScriptEngine.AuroraDotNetEngine.LSL_Types.",
                    "");
            if (message.Contains("The best overloaded method match for"))
            {
                string[] messageSplit = message.Split('\'');
                string Function = messageSplit[1];
                string[] FunctionSplit = Function.Split('(');
                string FunctionName = FunctionSplit[0];
                string Arguments = FunctionSplit[1].Split(')')[0];
                message = "Incorrect argument in " + FunctionName + ", arguments should be " + Arguments + "\n";
            }
            if (message == "Unexpected EOF")
            {
                message = "Missing one or more }." + "\n";
            }
            return message;
        }
        #endregion
    }
}
