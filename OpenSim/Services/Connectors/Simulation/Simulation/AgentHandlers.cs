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
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using Aurora.Simulation.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;

namespace OpenSim.Services
{
    public class AgentHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ISimulationService m_SimulationService;

        protected bool m_Proxy = false;
        protected bool m_secure = true;

        public AgentHandler() { }

        public AgentHandler(ISimulationService sim, bool secure)
        {
            m_SimulationService = sim;
            m_secure = secure;
        }

        public Hashtable Handler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: AgentHandler Called");

            //m_log.Debug("---------------------------");
            //m_log.Debug(" >> uri=" + request["uri"]);
            //m_log.Debug(" >> content-type=" + request["content-type"]);
            //m_log.Debug(" >> http-method=" + request["http-method"]);
            //m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";
            responsedata["keepalive"] = false;


            UUID agentID;
            UUID regionID;
            string action;
            string uri = ((string)request["uri"]);
            if(m_secure)
                uri = uri.Remove(0, 37); //Remove the secure UUID from the uri
            if (!WebUtils.GetParams(uri, out agentID, out regionID, out action))
            {
                m_log.InfoFormat("[AGENT HANDLER]: Invalid parameters for agent message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("PUT"))
            {
                DoAgentPut(request, responsedata);
                return responsedata;
            }
            else if (method.Equals("POST"))
            {
                DoAgentPost(request, responsedata, agentID);
                return responsedata;
            }
            else if (method.Equals("GET"))
            {
                DoAgentGet(request, responsedata, agentID, regionID);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoAgentDelete(request, responsedata, agentID, action, regionID);
                return responsedata;
            } 
            else if (method.Equals ("QUERYACCESS"))
            {
                responsedata["int_response_code"] = HttpStatusCode.OK;

                OSDMap resp = new OSDMap (2);

                resp["success"] = OSD.FromBoolean (true);
                resp["reason"] = OSD.FromString ("");

                responsedata["str_response_string"] = OSDParser.SerializeJsonString (resp);
                return responsedata;
            }
            else
            {
                m_log.InfoFormat("[AGENT HANDLER]: method {0} not supported in agent message", method);
                responsedata["int_response_code"] = HttpStatusCode.MethodNotAllowed;
                responsedata["str_response_string"] = "Method not allowed";

                return responsedata;
            }
        }

        protected void DoAgentPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            OSDMap args = WebUtils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            // retrieve the input arguments
            int x = 0, y = 0;
            UUID uuid = UUID.Zero;
            string regionname = string.Empty;
            uint teleportFlags = 0;
            if (args.ContainsKey("destination_x") && args["destination_x"] != null)
                Int32.TryParse(args["destination_x"].AsString(), out x);
            else
                m_log.WarnFormat("  -- request didn't have destination_x");
            if (args.ContainsKey("destination_y") && args["destination_y"] != null)
                Int32.TryParse(args["destination_y"].AsString(), out y);
            else
                m_log.WarnFormat("  -- request didn't have destination_y");
            if (args.ContainsKey("destination_uuid") && args["destination_uuid"] != null)
                UUID.TryParse(args["destination_uuid"].AsString(), out uuid);
            if (args.ContainsKey("destination_name") && args["destination_name"] != null)
                regionname = args["destination_name"].ToString();
            if (args.ContainsKey("teleport_flags") && args["teleport_flags"] != null)
                teleportFlags = args["teleport_flags"].AsUInteger();
            
            AgentData agent = null;
            if (args.ContainsKey("agent_data") && args["agent_data"] != null)
            {
                try
                {
                    OSDMap agentDataMap = (OSDMap)args["agent_data"];
                    agent = new AgentData();
                    agent.Unpack(agentDataMap);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildCreate message {0}", ex.ToString());
                }
            }

            GridRegion destination = new GridRegion();
            destination.RegionID = uuid;
            destination.RegionLocX = x;
            destination.RegionLocY = y;
            destination.RegionName = regionname;

            AgentCircuitData aCircuit = new AgentCircuitData();
            try
            {
                aCircuit.UnpackAgentCircuitData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildCreate message {0}", ex.ToString());
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            OSDMap resp = new OSDMap(3);
            string reason = String.Empty;

            // This is the meaning of POST agent
            bool result = CreateAgent(destination, aCircuit, teleportFlags, agent, out reason);

            resp["reason"] = reason;
            resp["success"] = OSD.FromBoolean(result);
            // Let's also send out the IP address of the caller back to the caller (HG 1.5)
            resp["your_ip"] = OSD.FromString(GetCallerIP(request));

            // TODO: add reason if not String.Empty?
            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        private string GetCallerIP(Hashtable request)
        {
            if (!m_Proxy)
                return Util.GetCallerIP(request);

            // We're behind a proxy
            Hashtable headers = (Hashtable)request["headers"];
            if (headers.ContainsKey("X-Forwarded-For") && headers["X-Forwarded-For"] != null)
            {
                IPEndPoint ep = Util.GetClientIPFromXFF((string)headers["X-Forwarded-For"]);
                if (ep != null)
                    return ep.Address.ToString();
            }

            // Oops
            return Util.GetCallerIP(request);
        }

        // subclasses can override this
        protected virtual bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint teleportFlags, AgentData agent, out string reason)
        {
            return m_SimulationService.CreateAgent(destination, aCircuit, teleportFlags, agent, out reason);
        }

        protected void DoAgentPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = WebUtils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            // retrieve the input arguments
            int x = 0, y = 0;
            UUID uuid = UUID.Zero;
            string regionname = string.Empty;
            if (args.ContainsKey("destination_x") && args["destination_x"] != null)
                Int32.TryParse(args["destination_x"].AsString(), out x);
            if (args.ContainsKey("destination_y") && args["destination_y"] != null)
                Int32.TryParse(args["destination_y"].AsString(), out y);
            if (args.ContainsKey("destination_uuid") && args["destination_uuid"] != null)
                UUID.TryParse(args["destination_uuid"].AsString(), out uuid);
            if (args.ContainsKey("destination_name") && args["destination_name"] != null)
                regionname = args["destination_name"].ToString();

            GridRegion destination = new GridRegion();
            destination.RegionID = uuid;
            destination.RegionLocX = x;
            destination.RegionLocY = y;
            destination.RegionName = regionname;

            string messageType;
            if (args["message_type"] != null)
                messageType = args["message_type"].AsString();
            else
            {
                m_log.Warn("[AGENT HANDLER]: Agent Put Message Type not found. ");
                messageType = "AgentData";
            }

            bool result = true;
            if ("AgentData".Equals(messageType))
            {
                AgentData agent = new AgentData();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.ToString());
                    responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                    responsedata["str_response_string"] = "Bad request";
                    return;
                }

                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = UpdateAgent(destination, agent);

            }
            else if ("AgentPosition".Equals(messageType))
            {
                AgentPosition agent = new AgentPosition();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[AGENT HANDLER]: exception on unpacking ChildAgentUpdate message {0}", ex.ToString());
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_SimulationService.UpdateAgent(destination, agent);

            }
            OSDMap resp = new OSDMap();
            resp["Updated"] = result;
            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        // subclasses can override this
        protected virtual bool UpdateAgent(GridRegion destination, AgentData agent)
        {
            return m_SimulationService.UpdateAgent(destination, agent);
        }

        protected virtual void DoAgentGet(Hashtable request, Hashtable responsedata, UUID id, UUID regionID)
        {
            if (m_SimulationService == null)
            {
                m_log.Debug("[AGENT HANDLER]: Agent GET called. Harmless but useless.");
                responsedata["content_type"] = "application/json";
                responsedata["int_response_code"] = HttpStatusCode.NotImplemented;
                responsedata["str_response_string"] = string.Empty;

                return;
            }

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            IAgentData agent = null;
            bool result = m_SimulationService.RetrieveAgent(destination, id, out agent);
            OSDMap map = null;
            string strBuffer = "";
            if (result)
            {
                if (agent != null) // just to make sure
                {
                    map = agent.Pack();
                    try
                    {
                        strBuffer = OSDParser.SerializeJsonString(map);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[AGENT HANDLER]: Exception thrown on serialization of DoAgentGet: {0}", e.ToString());
                        responsedata["int_response_code"] = HttpStatusCode.InternalServerError;
                        // ignore. buffer will be empty, caller should check.
                    }
                }
                else
                {
                    map = new OSDMap();
                    map["Result"] = "Internal error";
                }
            }
            else
            {
                map = new OSDMap();
                map["Result"] = "Not Found";
            }
            strBuffer = OSDParser.SerializeJsonString(map);

            responsedata["content_type"] = "application/json";
            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = strBuffer;
        }

        protected void DoAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, UUID regionID)
        {
            m_log.Debug(" >>> DoDelete action:" + action + "; RegionID:" + regionID);

            GridRegion destination = new GridRegion();
            destination.RegionID = regionID;

            if (action.Equals("release"))
            {
            }
            else
                m_SimulationService.CloseAgent(destination, id);

            responsedata["int_response_code"] = HttpStatusCode.OK;
            OSDMap map = new OSDMap();
            map["Agent"] = id;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(map);

            m_log.Debug("[AGENT HANDLER]: Agent Released/Deleted.");
        }
    }
}
