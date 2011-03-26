﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.IO;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using CSJ2K;
using OpenMetaverse.Rendering;
using OpenMetaverse.StructuredData;
using Rednettle.Warp3D;
using OpenSim.Region.CoreModules.World.Warp3DMap;
using WarpRenderer = global::Warp3D.Warp3D;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    public class WarpTileRenderer : IMapTileTerrainRenderer
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRendering m_primMesher;
        private Scene m_scene;
        private IConfigSource m_config;
        private static readonly UUID TEXTURE_METADATA_MAGIC = new UUID("802dc0e0-f080-4931-8b57-d1be8611c4f3");
        private static readonly Color4 WATER_COLOR = new Color4(29, 71, 95, 216);
        private Dictionary<UUID, Color4> m_colors = new Dictionary<UUID, Color4>();
        private bool m_useAntiAliasing = true; // TODO: Make this a config option

        public void Initialise(Scene scene, Nini.Config.IConfigSource config)
        {
            m_scene = scene;
            m_config = config;
        }

        public Bitmap TerrainToBitmap(System.Drawing.Bitmap mapbmp)
        {
            mapbmp = new Bitmap (m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY);
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
                m_log.Info("[MAPTILE]: Loaded prim mesher " + m_primMesher.ToString());
            }
            else
            {
                m_log.Info("[MAPTILE]: No prim mesher loaded, prim rendering will be disabled");
            }

            bool drawPrimVolume = true;
            bool textureTerrain = true;

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                drawPrimVolume = startupConfig.GetBoolean("DrawPrimOnMapTile", drawPrimVolume);
                textureTerrain = startupConfig.GetBoolean("TextureOnMapTile", textureTerrain);
            }
            catch
            {
                m_log.Warn("[MAPTILE]: Failed to load StartupConfig");
            }

            m_colors.Clear();

            Vector3 camPos = new Vector3 (m_scene.RegionInfo.RegionSizeX / 2, m_scene.RegionInfo.RegionSizeY / 2, 221.7025033688163f);
            Viewport viewport = new Viewport(camPos, -Vector3.UnitZ, 1024f, 0.1f, m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY, m_scene.RegionInfo.RegionSizeX, m_scene.RegionInfo.RegionSizeY);

            int width = viewport.Width;
            int height = viewport.Height;

            if (m_useAntiAliasing)
            {
                width *= 2;
                height *= 2;
            }

            WarpRenderer renderer = new WarpRenderer();
            renderer.CreateScene(width, height);
            renderer.Scene.autoCalcNormals = false;

            #region Camera

            warp_Vector pos = ConvertVector(viewport.Position);
            pos.z -= 0.001f; // Works around an issue with the Warp3D camera
            warp_Vector lookat = warp_Vector.add(ConvertVector(viewport.Position), ConvertVector(viewport.LookDirection));

            renderer.Scene.defaultCamera.setPos(pos);
            renderer.Scene.defaultCamera.lookAt(lookat);

            if (viewport.Orthographic)
            {
                renderer.Scene.defaultCamera.isOrthographic = true;
                renderer.Scene.defaultCamera.orthoViewWidth = viewport.OrthoWindowWidth;
                renderer.Scene.defaultCamera.orthoViewHeight = viewport.OrthoWindowHeight;
            }
            else
            {
                float fov = viewport.FieldOfView;
                fov *= 1.75f; // FIXME: ???
                renderer.Scene.defaultCamera.setFov(fov);
            }

            #endregion Camera

            renderer.Scene.addLight("Light1", new warp_Light(new warp_Vector(0.2f, 0.2f, 1f), 0xffffff, 320, 80));
            renderer.Scene.addLight("Light2", new warp_Light(new warp_Vector(-1f, -1f, 1f), 0xffffff, 100, 40));

            CreateWater(renderer);
            CreateTerrain(renderer, textureTerrain);
            if (drawPrimVolume && m_primMesher != null)
                m_scene.ForEachSOG(delegate (SceneObjectGroup group)
                {
                    group.ForEachPart(delegate(SceneObjectPart part) { CreatePrim(renderer, part); });
                });

            renderer.Render();
            Bitmap bitmap = renderer.Scene.getImage();

            renderer.Scene.removeAllObjects();
            renderer = null;
            viewport = null;
            m_primMesher = null;
            m_colors.Clear();

            //Force GC to try to clean this mess up
            GC.GetTotalMemory(true);

            if (m_useAntiAliasing)
                bitmap = ImageUtils.ResizeImage(bitmap, width / 2, height / 2);

            return bitmap;
        }

        #region Rendering Methods

        private void CreateWater(WarpRenderer renderer)
        {
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            renderer.AddPlane("Water", 256f * 0.5f);
            renderer.Scene.sceneobject("Water").setPos(127.5f, waterHeight, 127.5f);

            renderer.AddMaterial("WaterColor", ConvertColor(WATER_COLOR));
            renderer.Scene.material("WaterColor").setTransparency((byte)((1f - WATER_COLOR.A) * 255f));
            renderer.SetObjectMaterial("Water", "WaterColor");
        }

        private void CreateTerrain(WarpRenderer renderer, bool textureTerrain)
        {
            ITerrainChannel terrain = m_scene.RequestModuleInterface<ITerrainChannel>();

            warp_Object obj = new warp_Object (m_scene.RegionInfo.RegionSizeX * m_scene.RegionInfo.RegionSizeY, (m_scene.RegionInfo.RegionSizeX - 1) * (m_scene.RegionInfo.RegionSizeY - 1) * 2);

            for (int y = 0; y < m_scene.RegionInfo.RegionSizeY; y++)
            {
                for (int x = 0; x < m_scene.RegionInfo.RegionSizeX; x++)
                {
                    float height = terrain[x, y];

                    warp_Vector pos = ConvertVector(new Vector3(x, y, height));
                    obj.addVertex (new warp_Vertex (pos, (float)x / (m_scene.RegionInfo.RegionSizeX - 1), (float)((m_scene.RegionInfo.RegionSizeX - 1) - y) / (m_scene.RegionInfo.RegionSizeX - 1)));
                }
            }

            for (float y = 0; y < m_scene.RegionInfo.RegionSizeY; y += 1)
            {
                for (float x = 0; x < m_scene.RegionInfo.RegionSizeX; x += 1)
                {
                    if (x < m_scene.RegionInfo.RegionSizeX - 1 && y < m_scene.RegionInfo.RegionSizeY - 1)
                    {
                        float v = y * m_scene.RegionInfo.RegionSizeX + x;

                        // Normal
                        warp_Vector norm = new warp_Vector(x, y, terrain.GetNormalizedGroundHeight((int)x, (int)y));
                        norm = norm.reverse();
                        obj.vertex ((int)v).n = norm;

                        // Triangle 1
                        obj.addTriangle(
                            (int)v,
                            (int)v + 1,
                            (int)v + m_scene.RegionInfo.RegionSizeX);

                        // Triangle 2
                        obj.addTriangle(
                            (int)v + m_scene.RegionInfo.RegionSizeX + 1,
                            (int)v + m_scene.RegionInfo.RegionSizeX,
                            (int)v + 1);
                    }
                }
            }

            renderer.Scene.addObject("Terrain", obj);

            UUID[] textureIDs = new UUID[4];
            float[] startHeights = new float[4];
            float[] heightRanges = new float[4];

            RegionSettings regionInfo = m_scene.RegionInfo.RegionSettings;

            textureIDs[0] = regionInfo.TerrainTexture1;
            textureIDs[1] = regionInfo.TerrainTexture2;
            textureIDs[2] = regionInfo.TerrainTexture3;
            textureIDs[3] = regionInfo.TerrainTexture4;

            startHeights[0] = (float)regionInfo.Elevation1SW;
            startHeights[1] = (float)regionInfo.Elevation1NW;
            startHeights[2] = (float)regionInfo.Elevation1SE;
            startHeights[3] = (float)regionInfo.Elevation1NE;

            heightRanges[0] = (float)regionInfo.Elevation2SW;
            heightRanges[1] = (float)regionInfo.Elevation2NW;
            heightRanges[2] = (float)regionInfo.Elevation2SE;
            heightRanges[3] = (float)regionInfo.Elevation2NE;

            uint globalX, globalY;
            Utils.LongToUInts(m_scene.RegionInfo.RegionHandle, out globalX, out globalY);

            Bitmap image = TerrainSplat.Splat(terrain, textureIDs, startHeights, heightRanges, new Vector3d(globalX, globalY, 0.0), m_scene.AssetService, textureTerrain);
            warp_Texture texture = new warp_Texture(image);
            warp_Material material = new warp_Material(texture);
            material.setReflectivity(50);
            renderer.Scene.addMaterial("TerrainColor", material);
            renderer.SetObjectMaterial("Terrain", "TerrainColor");
        }

        private void CreatePrim(WarpRenderer renderer, SceneObjectPart prim)
        {
            const float MIN_SIZE = 2f;

            if ((PCode)prim.Shape.PCode != PCode.Prim)
                return;
            if (prim.Scale.LengthSquared() < MIN_SIZE * MIN_SIZE)
                return;

            Primitive omvPrim = prim.Shape.ToOmvPrimitive(prim.OffsetPosition, prim.RotationOffset);
            FacetedMesh renderMesh = m_primMesher.GenerateFacetedMesh(omvPrim, DetailLevel.Medium);
            if (renderMesh == null)
                return;

            warp_Vector primPos = ConvertVector(prim.GetWorldPosition());
            warp_Quaternion primRot = ConvertQuaternion(prim.RotationOffset);

            warp_Matrix m = warp_Matrix.quaternionMatrix(primRot);

            if (prim.ParentID != 0)
            {
                ISceneEntity group = m_scene.GetGroupByPrim (prim.LocalId);
                if (group != null)
                    m.transform(warp_Matrix.quaternionMatrix(ConvertQuaternion(group.RootChild.RotationOffset)));
            }

            warp_Vector primScale = ConvertVector(prim.Scale);

            string primID = prim.UUID.ToString();

            // Create the prim faces
            for (int i = 0; i < renderMesh.Faces.Count; i++)
            {
                Face face = renderMesh.Faces[i];
                string meshName = primID + "-Face-" + i.ToString();

                warp_Object faceObj = new warp_Object(face.Vertices.Count, face.Indices.Count / 3);

                for (int j = 0; j < face.Vertices.Count; j++)
                {
                    Vertex v = face.Vertices[j];

                    warp_Vector pos = ConvertVector(v.Position);
                    warp_Vector norm = ConvertVector(v.Normal);

                    if (prim.Shape.SculptTexture == UUID.Zero)
                        norm = norm.reverse();
                    warp_Vertex vert = new warp_Vertex(pos, norm, v.TexCoord.X, v.TexCoord.Y);

                    faceObj.addVertex(vert);
                }

                for (int j = 0; j < face.Indices.Count; j += 3)
                {
                    faceObj.addTriangle(
                        face.Indices[j + 0],
                        face.Indices[j + 1],
                        face.Indices[j + 2]);
                }

                Primitive.TextureEntryFace teFace = prim.Shape.Textures.GetFace((uint)i);
                Color4 faceColor = GetFaceColor(teFace);
                string materialName = GetOrCreateMaterial(renderer, faceColor);

                faceObj.transform(m);
                faceObj.setPos(primPos);
                faceObj.scaleSelf(primScale.x, primScale.y, primScale.z);

                renderer.Scene.addObject(meshName, faceObj);

                renderer.SetObjectMaterial(meshName, materialName);
            }
        }

        private Color4 GetFaceColor(Primitive.TextureEntryFace face)
        {
            Color4 color;

            if (face.TextureID == UUID.Zero)
                return face.RGBA;

            if (!m_colors.TryGetValue(face.TextureID, out color))
            {
                bool fetched = false;

                // Attempt to fetch the texture metadata
                UUID metadataID = UUID.Combine(face.TextureID, TEXTURE_METADATA_MAGIC);
                AssetBase metadata = m_scene.AssetService.GetCached(metadataID.ToString());
                if (metadata != null)
                {
                    OSDMap map = null;
                    try { map = OSDParser.Deserialize(metadata.Data) as OSDMap; }
                    catch { }

                    if (map != null)
                    {
                        color = map["X-JPEG2000-RGBA"].AsColor4();
                        fetched = true;
                    }
                }

                if (!fetched)
                {
                    // Fetch the texture, decode and get the average color,
                    // then save it to a temporary metadata asset
                    AssetBase textureAsset = m_scene.AssetService.Get(face.TextureID.ToString());
                    if (textureAsset != null)
                    {
                        int width, height;
                        color = GetAverageColor(textureAsset.FullID, textureAsset.Data, out width, out height);

                        OSDMap data = new OSDMap { { "X-JPEG2000-RGBA", OSD.FromColor4(color) } };
                        metadata = new AssetBase
                        {
                            Data = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data)),
                            Description = "Metadata for JPEG2000 texture " + face.TextureID.ToString(),
                            Flags = AssetFlags.Collectable,
                            FullID = metadataID,
                            ID = metadataID.ToString(),
                            Local = true,
                            Temporary = true,
                            Name = String.Empty,
                            Type = (sbyte)AssetType.Simstate // Make something up to get around OpenSim's myopic treatment of assets
                        };
                        m_scene.AssetService.Store(metadata);
                    }
                    else
                    {
                        color = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                    }
                }

                m_colors[face.TextureID] = color;
            }

            return color * face.RGBA;
        }

        private string GetOrCreateMaterial(WarpRenderer renderer, Color4 color)
        {
            string name = color.ToString();

            warp_Material material = renderer.Scene.material(name);
            if (material != null)
                return name;

            renderer.AddMaterial(name, ConvertColor(color));
            if (color.A < 1f)
                renderer.Scene.material(name).setTransparency((byte)((1f - color.A) * 255f));
            return name;
        }

        #endregion Rendering Methods

        #region Static Helpers

        private static warp_Vector ConvertVector(Vector3 vector)
        {
            return new warp_Vector(vector.X, vector.Z, vector.Y);
        }

        private static warp_Quaternion ConvertQuaternion(Quaternion quat)
        {
            return new warp_Quaternion(quat.X, quat.Z, quat.Y, -quat.W);
        }

        private static int ConvertColor(Color4 color)
        {
            int c = warp_Color.getColor((byte)(color.R * 255f), (byte)(color.G * 255f), (byte)(color.B * 255f));
            if (color.A < 1f)
                c |= (byte)(color.A * 255f) << 24;

            return c;
        }

        public static Color4 GetAverageColor(UUID textureID, byte[] j2kData, out int width, out int height)
        {
            ulong r = 0;
            ulong g = 0;
            ulong b = 0;
            ulong a = 0;

            using (MemoryStream stream = new MemoryStream(j2kData))
            {
                try
                {
                    Bitmap bitmap = (Bitmap)J2kImage.FromStream(stream);
                    width = bitmap.Width;
                    height = bitmap.Height;

                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                    int pixelBytes = (bitmap.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;

                    // Sum up the individual channels
                    unsafe
                    {
                        if (pixelBytes == 4)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                                for (int x = 0; x < width; x++)
                                {
                                    b += row[x * pixelBytes + 0];
                                    g += row[x * pixelBytes + 1];
                                    r += row[x * pixelBytes + 2];
                                    a += row[x * pixelBytes + 3];
                                }
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                                for (int x = 0; x < width; x++)
                                {
                                    b += row[x * pixelBytes + 0];
                                    g += row[x * pixelBytes + 1];
                                    r += row[x * pixelBytes + 2];
                                }
                            }
                        }
                    }

                    // Get the averages for each channel
                    const decimal OO_255 = 1m / 255m;
                    decimal totalPixels = (decimal)(width * height);

                    decimal rm = ((decimal)r / totalPixels) * OO_255;
                    decimal gm = ((decimal)g / totalPixels) * OO_255;
                    decimal bm = ((decimal)b / totalPixels) * OO_255;
                    decimal am = ((decimal)a / totalPixels) * OO_255;

                    if (pixelBytes == 3)
                        am = 1m;

                    return new Color4((float)rm, (float)gm, (float)bm, (float)am);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("[MAPTILE]: Error decoding JPEG2000 texture {0} ({1} bytes): {2}", textureID, j2kData.Length, ex.Message);
                    width = 0;
                    height = 0;
                    return new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                }
            }
        }

        #endregion Static Helpers
    }

    public static class ImageUtils
    {
        /// <summary>
        /// Performs bilinear interpolation between four values
        /// </summary>
        /// <param name="v00">First, or top left value</param>
        /// <param name="v01">Second, or top right value</param>
        /// <param name="v10">Third, or bottom left value</param>
        /// <param name="v11">Fourth, or bottom right value</param>
        /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
        /// <param name="yPercent">Interpolation value on fht Y axis, between 0.0 and 1.0</param>
        /// <returns>The bilinearly interpolated result</returns>
        public static float Bilinear(float v00, float v01, float v10, float v11, float xPercent, float yPercent)
        {
            return Utils.Lerp(Utils.Lerp(v00, v01, xPercent), Utils.Lerp(v10, v11, xPercent), yPercent);
        }

        /// <summary>
        /// Performs a high quality image resize
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        /// <returns>Resized image</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            return result;
        }
    }
}
