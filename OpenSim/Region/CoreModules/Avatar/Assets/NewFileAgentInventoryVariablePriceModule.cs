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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Capabilities;

namespace OpenSim.Region.CoreModules.Avatar.Assets
{
    public class NewFileAgentInventoryVariablePriceModule : INonSharedRegionModule
    {
        private Scene m_scene;

        #region IRegionModuleBase Members


        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
           
        }

        public void AddRegion(Scene pScene)
        {
            m_scene = pScene;
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        #endregion


        #region IRegionModule Members

       

        public void Close() { }

        public string Name { get { return "NewFileAgentInventoryVariablePriceModule"; } }


        public OSDMap RegisterCaps(UUID agentID, IHttpServer server)
        {
            OSDMap retVal = new OSDMap();
            retVal["NewFileAgentInventoryVariablePrice"] = CapsUtil.CreateCAPS("NewFileAgentInventoryVariablePrice", "");
            server.AddStreamHandler(new RestStreamHandler("POST", retVal["NewFileAgentInventoryVariablePrice"],
                delegate(string request, string path, string param,
                                             OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                       {
                                                           return NewAgentInventoryRequest(request, agentID);
                                                       }));
            return retVal;
        }

        #endregion

        public string NewAgentInventoryRequest(string request, UUID agentID)
        {
            OSDMap map = (OSDMap)OSDParser.DeserializeLLSDXml(request);

            //TODO:  The Mesh uploader uploads many types of content. If you're going to implement a Money based limit
            // You need to be aware of this and 


            //if (llsdRequest.asset_type == "texture" ||
            //     llsdRequest.asset_type == "animation" ||
            //     llsdRequest.asset_type == "sound")
            // {
            IClientAPI client = null;


            IMoneyModule mm = m_scene.RequestModuleInterface<IMoneyModule>();

            if (mm != null)
            {
                if (m_scene.TryGetClient(agentID, out client))
                {
                    if (!mm.ApplyCharge(agentID, mm.UploadCharge, "Asset upload"))
                    {
                        if (client != null)
                            client.SendAgentAlertMessage("Unable to upload asset. Insufficient funds.", false);
                        map = new OSDMap();
                        map["rsvp"] = "";
                        map["state"] = "error";
                        return OSDParser.SerializeLLSDXmlString(map);
                    }
                }
            }
            // }



            string asset_type = map["asset_type"].AsString();
            string assetName = map["name"].AsString();
            string assetDes = map["description"].AsString();
            string capsBase = "/CAPS/NewFileAgentInventoryVariablePrice/";
            string inventory_type = map["inventory_type"].AsString();
            UUID newAsset = UUID.Random();
            UUID newInvItem = UUID.Random();
            UUID parentFolder = map["folder_id"].AsUUID();
            string uploaderPath = Util.RandomClass.Next(5000, 8000).ToString("0000") + "/";

            AssetUploader uploader =
                new AssetUploader(assetName, assetDes, newAsset, newInvItem, parentFolder, inventory_type,
                                  asset_type, capsBase + uploaderPath, MainServer.Instance, agentID, this);
            MainServer.Instance.AddStreamHandler(
                new BinaryStreamHandler("POST", capsBase + uploaderPath, uploader.uploaderCaps));

            string uploaderURL = m_scene.RegionInfo.ServerURI + capsBase +
                                 uploaderPath;

            map = new OSDMap();
            map["rsvp"] = uploaderURL;
            map["state"] = "upload";
            map["resource_cost"] = 0;
            map["upload_price"] = 0;
            return OSDParser.SerializeLLSDXmlString(map);
        }

       
        public void UploadCompleteHandler(string assetName, string assetDescription, UUID assetID,
                                          UUID inventoryItem, UUID parentFolder, byte[] data, string inventoryType,
                                          string assetType,UUID AgentID)
        {
            
            sbyte assType = 0;
            sbyte inType = 0;

            if (inventoryType == "sound")
            {
                inType = 1;
                assType = 1;
            }
            else if (inventoryType == "animation")
            {
                inType = 19;
                assType = 20;
            }
            else if (inventoryType == "wearable")
            {
                inType = 18;
                switch (assetType)
                {
                    case "bodypart":
                        assType = 13;
                        break;
                    case "clothing":
                        assType = 5;
                        break;
                }
            }
            else if (inventoryType == "mesh")
            {
                inType = (sbyte)InventoryType.Mesh; 
                assType = (sbyte)AssetType.Mesh;
            }

            AssetBase asset;
            asset = new AssetBase(assetID, assetName, assType, AgentID.ToString());
            asset.Data = data;
    
            if (m_scene.AssetService != null)
                m_scene.AssetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Owner = AgentID;
            item.CreatorId = AgentID.ToString();
            item.ID = inventoryItem;
            item.AssetID = asset.FullID;
            item.Description = assetDescription;
            item.Name = assetName;
            item.AssetType = assType;
            item.InvType = inType;
            item.Folder = parentFolder;
            item.CurrentPermissions = (uint)PermissionMask.All;
            item.BasePermissions = (uint)PermissionMask.All;
            item.EveryOnePermissions = 0;
            item.NextPermissions = (uint)(PermissionMask.Move | PermissionMask.Modify | PermissionMask.Transfer);
            item.CreationDate = Util.UnixTimeSinceEpoch();
            ILLClientInventory inventoryModule = m_scene.RequestModuleInterface<ILLClientInventory>();
            if(inventoryModule != null)
                inventoryModule.AddInventoryItem(item);
        }

        public class AssetUploader
        {
            private string uploaderPath = String.Empty;
            private UUID newAssetID;
            private UUID inventoryItemID;
            private UUID parentFolder;
            private IHttpServer httpListener;
            private string m_assetName = String.Empty;
            private string m_assetDes = String.Empty;
            private NewFileAgentInventoryVariablePriceModule m_mod;
            private UUID m_agentID;

            private string m_invType = String.Empty;
            private string m_assetType = String.Empty;

            public AssetUploader(string assetName, string description, UUID assetID, UUID inventoryItem,
                                 UUID parentFolderID, string invType, string assetType, string path,
                                 IHttpServer httpServer, UUID AgentID, NewFileAgentInventoryVariablePriceModule mod)
            {
                m_assetName = assetName;
                m_assetDes = description;
                newAssetID = assetID;
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                httpListener = httpServer;
                parentFolder = parentFolderID;
                m_assetType = assetType;
                m_invType = invType;
                m_agentID = AgentID;
                m_mod = mod;
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="data"></param>
            /// <param name="path"></param>
            /// <param name="param"></param>
            /// <returns></returns>
            public string uploaderCaps(byte[] data, string path, string param)
            {
                UUID inv = inventoryItemID;
                string res = String.Empty;
                OSDMap map = new OSDMap();
                map["new_asset"] = newAssetID.ToString();
                map["new_inventory_item"] = inv;
                map["state"] = "complete";
                res = OSDParser.SerializeLLSDXmlString(map);

                httpListener.RemoveStreamHandler("POST", uploaderPath);

                m_mod.UploadCompleteHandler(m_assetName, m_assetDes, newAssetID, inv, parentFolder, data, m_invType, m_assetType, m_agentID);

                return res;
            }
            ///Left this in and commented in case there are unforseen issues
            //private void SaveAssetToFile(string filename, byte[] data)
            //{
            //    FileStream fs = File.Create(filename);
            //    BinaryWriter bw = new BinaryWriter(fs);
            //    bw.Write(data);
            //    bw.Close();
            //    fs.Close();
            //}
            private static void SaveAssetToFile(string filename, byte[] data)
            {
                string assetPath = "UserAssets";
                if (!Directory.Exists(assetPath))
                {
                    Directory.CreateDirectory(assetPath);
                }
                FileStream fs = File.Create(Path.Combine(assetPath, Util.safeFileName(filename)));
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }
        }
    }
}
