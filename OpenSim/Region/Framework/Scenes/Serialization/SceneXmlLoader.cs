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
using System.Xml;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Framework.Scenes.Serialization
{
    /// <summary>
    /// Static methods to serialize and deserialize scene objects to and from XML
    /// </summary>
    public class SceneXmlLoader
    {
        public static void LoadPrimsFromXml(Scene scene, string fileName, bool newIDS, Vector3 loadOffset)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;

            if (fileName.StartsWith("http:") || File.Exists(fileName))
            {
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
                rootNode = doc.FirstChild;
                foreach (XmlNode aPrimNode in rootNode.ChildNodes)
                {
                    SceneObjectGroup group = SceneObjectSerializer.FromOriginalXmlFormat(aPrimNode.OuterXml, scene);
                    if (group == null)
                        return;

                    group.IsDeleted = false;
                    group.m_isLoaded = true;
                    foreach (SceneObjectPart part in group.ChildrenList)
                    {
                        part.IsLoading = false;
                    }
                    scene.SceneGraph.AddPrimToScene(group);
                }
            }
            else
            {
                throw new Exception("Could not open file " + fileName + " for reading");
            }
        }

        public static void SavePrimsToXml(Scene scene, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            foreach (ISceneEntity ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(SceneObjectSerializer.ToOriginalXmlFormat((SceneObjectGroup)ent));
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
        }

        public static string SaveGroupToXml2(SceneObjectGroup grp)
        {
            return SceneObjectSerializer.ToXml2Format(grp);
        }

        public static SceneObjectGroup DeserializeGroupFromXml2(string xmlString, Scene scene)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;

            XmlTextReader reader = new XmlTextReader(new StringReader(xmlString));
            reader.WhitespaceHandling = WhitespaceHandling.None;
            doc.Load(reader);
            reader.Close();
            rootNode = doc.FirstChild;

            return SceneObjectSerializer.FromXml2Format(rootNode.OuterXml, scene);
        }

        /// <summary>
        /// Load prims from the xml2 format.  This method will close the reader
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="reader"></param>
        /// <param name="startScripts"></param>
        public static void LoadPrimsFromXml2(Scene scene, XmlTextReader reader, bool startScripts)
        {
            XmlDocument doc = new XmlDocument();
            reader.WhitespaceHandling = WhitespaceHandling.None;
            doc.Load(reader);
            reader.Close();
            XmlNode rootNode = doc.FirstChild;

            ICollection<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();
            foreach (XmlNode aPrimNode in rootNode.ChildNodes)
            {
                SceneObjectGroup obj = CreatePrimFromXml2(scene, aPrimNode.OuterXml);
                if (obj != null)
                    sceneObjects.Add(obj);
            }

            foreach (SceneObjectGroup sceneObject in sceneObjects)
            {
                if (scene.SceneGraph.AddPrimToScene(sceneObject))
                {
                    sceneObject.ScheduleGroupUpdate(PrimUpdateFlags.FullUpdate);
                    if (startScripts)
                    {
                        sceneObject.CreateScriptInstances(0, false, 0, UUID.Zero);
                        sceneObject.ResumeScripts();
                    }
                }
            }
        }

        /// <summary>
        /// Create a prim from the xml2 representation.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="xmlData"></param>
        /// <returns>The scene object created.  null if the scene object already existed</returns>
        protected static SceneObjectGroup CreatePrimFromXml2(Scene scene, string xmlData)
        {
            SceneObjectGroup obj = SceneXmlLoader.DeserializeGroupFromXml2(xmlData, scene);
            return obj;
        }

        public static void SavePrimsToXml2(Scene scene, string fileName)
        {
            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            SavePrimListToXml2(entityList, fileName);
        }

        public static void SavePrimsToXml2(Scene scene, TextWriter stream, Vector3 min, Vector3 max)
        {
            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            SavePrimListToXml2(entityList, stream, min, max);
        }
        
        public static void SaveNamedPrimsToXml2(Scene scene, string primName, string fileName)
        {
            //m_log.InfoFormat(
            //    "[SERIALISER]: Saving prims with name {0} in xml2 format for region {1} to {2}", 
            //    primName, scene.RegionInfo.RegionName, fileName);

            ISceneEntity[] entityList = scene.Entities.GetEntities ();
            List<ISceneEntity> primList = new List<ISceneEntity> ();

            foreach (ISceneEntity ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (ent.Name == primName)
                    {
                        primList.Add(ent);
                    }
                }
            }

            SavePrimListToXml2(primList.ToArray(), fileName);
        }

        public static void SavePrimListToXml2 (ISceneEntity[] entityList, string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            try
            {
                StreamWriter stream = new StreamWriter(file);
                try
                {
                    SavePrimListToXml2(entityList, stream, Vector3.Zero, Vector3.Zero);
                }
                finally
                {
                    stream.Close();
                }
            }
            finally
            {
                file.Close();
            }
        }

        public static void SavePrimListToXml2 (ISceneEntity[] entityList, TextWriter stream, Vector3 min, Vector3 max)
        {
            int primCount = 0;
            stream.WriteLine("<scene>\n");

            foreach (ISceneEntity ent in entityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup g = (SceneObjectGroup)ent;
                    if (!min.Equals(Vector3.Zero) || !max.Equals(Vector3.Zero))
                    {
                        Vector3 pos = g.RootPart.GetWorldPosition();
                        if (min.X > pos.X || min.Y > pos.Y || min.Z > pos.Z)
                            continue;
                        if (max.X < pos.X || max.Y < pos.Y || max.Z < pos.Z)
                            continue;
                    }

                    stream.WriteLine(SceneObjectSerializer.ToXml2Format(g));
                    //SceneObjectSerializer.SOGToXml2(writer, (SceneObjectGroup)ent);
                    //stream.WriteLine();

                    primCount++;
                }
            }

            stream.WriteLine("</scene>\n");
            stream.Flush();
        }

    }
}
