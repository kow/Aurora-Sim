using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Aurora.Framework;
using Aurora.DataManager;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using log4net;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Aurora.Simulation.Base;

namespace Aurora.Services.DataService
{
    public class RemoteAssetConnector : IAssetConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        private IRegistryCore m_registry;

        public void Initialize(IGenericData unneeded, IConfigSource source, IRegistryCore simBase, string defaultConnectionString)
        {
            if (source.Configs["AuroraConnectors"].GetString("AssetConnector", "LocalConnector") == "RemoteConnector")
            {
                m_registry = simBase;
                DataManager.DataManager.RegisterPlugin(Name, this);
            }
        }

        public string Name
        {
            get { return "IAssetConnector"; }
        }

        public void Dispose()
        {
        }

        #region IAssetConnector Members

        public void UpdateLSLData(string token, string key, string value)
        {
            Dictionary<string, object> sendData = new Dictionary<string,object>();

            sendData["token"] = token;
            sendData["key"] = key;
            sendData["value"] = value;
            sendData["METHOD"] = "updatelsldata";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf("RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    AsynchronousRestObjectRequester.MakeRequest("POST",
                           m_ServerURI,
                           reqString);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteAssetConnector]: Exception when contacting server: {0}", e.ToString());
            }
        }

        public List<string> FindLSLData(string token, string key)
        {
            List<string> data = new List<string>();
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["token"] = token;
            sendData["key"] = key;
            sendData["METHOD"] = "findlsldata";

            string reqString = WebUtils.BuildQueryString(sendData);

            try
            {
                List<string> m_ServerURIs = m_registry.RequestModuleInterface<IConfigurationService>().FindValueOf("RemoteServerURI");
                foreach (string m_ServerURI in m_ServerURIs)
                {
                    string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                           m_ServerURI,
                           reqString);

                    if (reply != string.Empty)
                    {
                        Dictionary<string, object> replyData = WebUtils.ParseXmlResponse(reply);

                        if (replyData != null)
                        {
                            if (!replyData.ContainsKey("result"))
                                return data;
                            foreach (object obj in replyData.Values)
                            {
                                if (obj is Dictionary<string, object>)
                                {
                                    Dictionary<string, object> dictionary = obj as Dictionary<string, object>;
                                    foreach (object value in dictionary)
                                    {
                                        KeyValuePair<string, object> valuevalue = (KeyValuePair<string, object>)value;
                                        data.Add(valuevalue.Value.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AuroraRemoteAssetConnector]: Exception when contacting server: {0}", e.ToString());
            }
            return data;
        }

        #endregion
    }
}
