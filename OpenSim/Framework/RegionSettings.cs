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
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public class RegionSettings
    {
        public delegate void SaveDelegate(RegionSettings rs);

        public event SaveDelegate OnSave;
        
        /// <value>
        /// These appear to be terrain textures that are shipped with the client.
        /// </value>
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_1 = new UUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_2 = new UUID("abb783e6-3e93-26c0-248a-247666855da3");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_3 = new UUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
        public static readonly UUID DEFAULT_TERRAIN_TEXTURE_4 = new UUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");

        public void Save()
        {
            if (OnSave != null)
                OnSave(this);
        }

        private UUID m_RegionUUID = UUID.Zero;

        public UUID RegionUUID
        {
            get { return m_RegionUUID; }
            set { m_RegionUUID = value; }
        }

        private bool m_BlockTerraform = false;

        public bool BlockTerraform
        {
            get { return m_BlockTerraform; }
            set { m_BlockTerraform = value; }
        }

        private bool m_BlockFly = false;

        public bool BlockFly
        {
            get { return m_BlockFly; }
            set { m_BlockFly = value; }
        }

        private bool m_AllowDamage = false;

        public bool AllowDamage
        {
            get { return m_AllowDamage; }
            set { m_AllowDamage = value; }
        }

        private bool m_RestrictPushing = false;

        public bool RestrictPushing
        {
            get { return m_RestrictPushing; }
            set { m_RestrictPushing = value; }
        }

        private bool m_AllowLandResell = true;

        public bool AllowLandResell
        {
            get { return m_AllowLandResell; }
            set { m_AllowLandResell = value; }
        }

        private bool m_AllowLandJoinDivide = true;

        public bool AllowLandJoinDivide
        {
            get { return m_AllowLandJoinDivide; }
            set { m_AllowLandJoinDivide = value; }
        }

        private bool m_BlockShowInSearch = false;

        public bool BlockShowInSearch
        {
            get { return m_BlockShowInSearch; }
            set { m_BlockShowInSearch = value; }
        }

        private int m_AgentLimit = 40;

        public int AgentLimit
        {
            get { return m_AgentLimit; }
            set { m_AgentLimit = value; }
        }

        private double m_ObjectBonus = 1.0;

        public double ObjectBonus
        {
            get { return m_ObjectBonus; }
            set { m_ObjectBonus = value; }
        }

        private int m_Maturity = 1;

        public int Maturity
        {
            get { return m_Maturity; }
            set { m_Maturity = value; }
        }

        private bool m_DisableScripts = false;

        public bool DisableScripts
        {
            get { return m_DisableScripts; }
            set { m_DisableScripts = value; }
        }

        private bool m_DisableCollisions = false;

        public bool DisableCollisions
        {
            get { return m_DisableCollisions; }
            set { m_DisableCollisions = value; }
        }

        private bool m_DisablePhysics = false;

        public bool DisablePhysics
        {
            get { return m_DisablePhysics; }
            set { m_DisablePhysics = value; }
        }

        private UUID m_TerrainTexture1 = UUID.Zero;

        public UUID TerrainTexture1
        {
            get { return m_TerrainTexture1; }
            set
            {
                if (value == UUID.Zero)
                    m_TerrainTexture1 = DEFAULT_TERRAIN_TEXTURE_1;
                else
                    m_TerrainTexture1 = value;
            }
        }

        private int m_MinimumAge = 0;
        public int MinimumAge
        {
            get
            {
                return m_MinimumAge;
            }
            set
            {
                m_MinimumAge = value;
            }
        }

        private UUID m_TerrainTexture2 = UUID.Zero;

        public UUID TerrainTexture2
        {
            get { return m_TerrainTexture2; }
            set
            {
                if (value == UUID.Zero)
                    m_TerrainTexture2 = DEFAULT_TERRAIN_TEXTURE_2;
                else
                    m_TerrainTexture2 = value;
            }
        }

        private UUID m_TerrainTexture3 = UUID.Zero;

        public UUID TerrainTexture3
        {
            get { return m_TerrainTexture3; }
            set
            {
                if (value == UUID.Zero)
                    m_TerrainTexture3 = DEFAULT_TERRAIN_TEXTURE_3;
                else
                    m_TerrainTexture3 = value;
            }
        }

        private UUID m_TerrainTexture4 = UUID.Zero;

        public UUID TerrainTexture4
        {
            get { return m_TerrainTexture4; }
            set
            {
                if (value == UUID.Zero)
                    m_TerrainTexture4 = DEFAULT_TERRAIN_TEXTURE_4;
                else
                    m_TerrainTexture4 = value;
            }
        }

        private double m_Elevation1NW = 10;

        public double Elevation1NW
        {
            get { return m_Elevation1NW; }
            set { m_Elevation1NW = value; }
        }

        private double m_Elevation2NW = 60;

        public double Elevation2NW
        {
            get { return m_Elevation2NW; }
            set { m_Elevation2NW = value; }
        }

        private double m_Elevation1NE = 10;

        public double Elevation1NE
        {
            get { return m_Elevation1NE; }
            set { m_Elevation1NE = value; }
        }

        private double m_Elevation2NE = 60;

        public double Elevation2NE
        {
            get { return m_Elevation2NE; }
            set { m_Elevation2NE = value; }
        }

        private double m_Elevation1SE = 10;

        public double Elevation1SE
        {
            get { return m_Elevation1SE; }
            set { m_Elevation1SE = value; }
        }

        private double m_Elevation2SE = 60;

        public double Elevation2SE
        {
            get { return m_Elevation2SE; }
            set { m_Elevation2SE = value; }
        }

        private double m_Elevation1SW = 10;

        public double Elevation1SW
        {
            get { return m_Elevation1SW; }
            set { m_Elevation1SW = value; }
        }

        private double m_Elevation2SW = 60;

        public double Elevation2SW
        {
            get { return m_Elevation2SW; }
            set { m_Elevation2SW = value; }
        }

        private double m_WaterHeight = 20;

        public double WaterHeight
        {
            get { return m_WaterHeight; }
            set { m_WaterHeight = value; }
        }

        private double m_TerrainRaiseLimit = 100;

        public double TerrainRaiseLimit
        {
            get { return m_TerrainRaiseLimit; }
            set { m_TerrainRaiseLimit = value; }
        }

        private double m_TerrainLowerLimit = -100;

        public double TerrainLowerLimit
        {
            get { return m_TerrainLowerLimit; }
            set { m_TerrainLowerLimit = value; }
        }

        private bool m_UseEstateSun = true;

        public bool UseEstateSun
        {
            get { return m_UseEstateSun; }
            set { m_UseEstateSun = value; }
        }

        private bool m_Sandbox = false;

        public bool Sandbox
        {
            get { return m_Sandbox; }
            set { m_Sandbox = value; }
        }

        private Vector3 m_SunVector;

        public Vector3 SunVector
        {
            get { return m_SunVector; }
            set { m_SunVector = value; }
        }

        private UUID m_TerrainImageID;
        /// <summary>
        /// Terrain (and probably) prims asset ID for the map
        /// </summary>
        public UUID TerrainImageID
        {
            get { return m_TerrainImageID; }
            set { m_TerrainImageID = value; }
        }

        private UUID m_TerrainMapImageID;
        /// <summary>
        /// Terrain only asset ID for the map
        /// </summary>
        public UUID TerrainMapImageID
        {
            get { return m_TerrainMapImageID; }
            set { m_TerrainMapImageID = value; }
        }

        private bool m_FixedSun = false;

        public bool FixedSun
        {
            get { return m_FixedSun; }
            set { m_FixedSun = value; }
        }

        private double m_SunPosition = 0.0;

        public double SunPosition
        {
            get { return m_SunPosition; }
            set { m_SunPosition = value; }
        }

        private UUID m_Covenant = UUID.Zero;

        public UUID Covenant
        {
            get { return m_Covenant; }
            set { m_Covenant = value; }
        }

        private int m_CovenantLastUpdated = 0;

        public int CovenantLastUpdated
        {
            get { return m_CovenantLastUpdated; }
            set { m_CovenantLastUpdated = value; }
        }

        private OSDMap m_Generic = new OSDMap();

        public OSDMap Generic
        {
            get { return m_Generic; }
            set { m_Generic = value; }
        }

        public void AddGeneric(string key, OSD value)
        {
            m_Generic[key] = value;
        }

        public void RemoveGeneric(string key)
        {
            if (m_Generic.ContainsKey(key))
                m_Generic.Remove(key);
        }

        public OSD GetGeneric(string key)
        {
            OSD value;
            m_Generic.TryGetValue(key, out value);
            return value;
        }

        private int m_LoadedCreationDateTime;
        public int LoadedCreationDateTime
        {
            get { return m_LoadedCreationDateTime; }
            set { m_LoadedCreationDateTime = value; }
        }
        
        public String LoadedCreationDate
        {
            get 
            { 
                TimeSpan ts = new TimeSpan(0, 0, LoadedCreationDateTime);
                DateTime stamp = new DateTime(1970, 1, 1) + ts;
                return stamp.ToLongDateString(); 
            }
        }

        public String LoadedCreationTime
        {
            get 
            { 
                TimeSpan ts = new TimeSpan(0, 0, LoadedCreationDateTime);
                DateTime stamp = new DateTime(1970, 1, 1) + ts;
                return stamp.ToLongTimeString(); 
            }
        }

        private String m_LoadedCreationID = String.Empty;
        public String LoadedCreationID
        {
            get { return m_LoadedCreationID; }
            set { m_LoadedCreationID = value; }
        }

        public OSDMap ToOSD()
        {
            OSDMap map = new OSDMap();

            map["AgentLimit"] = this.AgentLimit;
            map["AllowDamage"] = this.AllowDamage;
            map["AllowLandJoinDivide"] = this.AllowLandJoinDivide;
            map["AllowLandResell"] = this.AllowLandResell;
            map["BlockFly"] = this.BlockFly;
            map["BlockShowInSearch"] = this.BlockShowInSearch;
            map["BlockTerraform"] = this.BlockTerraform;
            map["Covenant"] = this.Covenant;
            map["CovenantLastUpdated"] = this.CovenantLastUpdated;
            map["DisableCollisions"] = this.DisableCollisions;
            map["DisablePhysics"] = this.DisablePhysics;
            map["DisableScripts"] = this.DisableScripts;
            map["Elevation1NE"] = this.Elevation1NE;
            map["Elevation1NW"] = this.Elevation1NW;
            map["Elevation1SE"] = this.Elevation1SE;
            map["Elevation1SW"] = this.Elevation1SW;
            map["Elevation2NE"] = this.Elevation2NE;
            map["Elevation2NW"] = this.Elevation2NW;
            map["Elevation2SE"] = this.Elevation2SE;
            map["Elevation2SW"] = this.Elevation2SW;
            map["FixedSun"] = this.FixedSun;
            map["Generic"] = this.Generic;
            map["LoadedCreationDateTime"] = this.LoadedCreationDateTime;
            map["LoadedCreationID"] = this.LoadedCreationID;
            map["Maturity"] = this.Maturity;
            map["MinimumAge"] = this.MinimumAge;
            map["ObjectBonus"] = this.ObjectBonus;
            map["RegionUUID"] = this.RegionUUID;
            map["RestrictPushing"] = this.RestrictPushing;
            map["Sandbox"] = this.Sandbox;
            map["SunPosition"] = this.SunPosition;
            map["SunVector"] = this.SunVector;
            map["TerrainImageID"] = this.TerrainImageID;
            map["TerrainLowerLimit"] = this.TerrainLowerLimit;
            map["TerrainMapImageID"] = this.TerrainMapImageID;
            map["TerrainRaiseLimit"] = this.TerrainRaiseLimit;
            map["TerrainTexture1"] = this.TerrainTexture1;
            map["TerrainTexture2"] = this.TerrainTexture2;
            map["TerrainTexture3"] = this.TerrainTexture3;
            map["TerrainTexture4"] = this.TerrainTexture4;
            map["UseEstateSun"] = this.UseEstateSun;
            map["WaterHeight"] = this.WaterHeight;

            return map;
        }

        public void FromOSD(OSDMap map)
        {
            this.AgentLimit = map["AgentLimit"];
            this.AllowLandJoinDivide = map["AllowLandJoinDivide"];
            this.AllowLandResell = map["AllowLandResell"];
            this.BlockFly = map["BlockFly"];
            this.BlockShowInSearch = map["BlockShowInSearch"];
            this.BlockTerraform = map["BlockTerraform"];
            this.Covenant = map["Covenant"];
            this.CovenantLastUpdated = map["CovenantLastUpdated"];
            this.DisableCollisions = map["DisableCollisions"];
            this.DisablePhysics = map["DisablePhysics"];
            this.DisableScripts = map["DisableScripts"];
            this.Elevation1NE = map["Elevation1NE"];
            this.Elevation1NW = map["Elevation1NW"];
            this.Elevation1SE = map["Elevation1SE"];
            this.Elevation1SW = map["Elevation1SW"];
            this.Elevation2NE = map["Elevation2NE"];
            this.Elevation2NW = map["Elevation2NW"];
            this.Elevation2SE = map["Elevation2SE"];
            this.Elevation2SW = map["Elevation2SW"];
            this.FixedSun = map["FixedSun"];
            this.Generic = (OSDMap)map["Generic"];
            this.LoadedCreationDateTime = map["LoadedCreationDateTime"];
            this.LoadedCreationID = map["LoadedCreationID"];
            this.Maturity = map["Maturity"];
            this.MinimumAge = map["MinimumAge"];
            this.ObjectBonus = map["ObjectBonus"];
            this.RegionUUID = map["RegionUUID"];
            this.RestrictPushing = map["RestrictPushing"];
            this.Sandbox = map["Sandbox"];
            this.SunPosition = map["SunPosition"];
            this.SunVector = map["SunVector"];
            this.TerrainImageID = map["TerrainImageID"];
            this.TerrainLowerLimit = map["TerrainLowerLimit"];
            this.TerrainMapImageID = map["TerrainMapImageID"];
            this.TerrainRaiseLimit = map["TerrainRaiseLimit"];
            this.TerrainTexture1 = map["TerrainTexture1"];
            this.TerrainTexture2 = map["TerrainTexture2"];
            this.TerrainTexture3 = map["TerrainTexture3"];
            this.TerrainTexture4 = map["TerrainTexture4"];
            this.UseEstateSun = map["UseEstateSun"];
            this.WaterHeight = map["WaterHeight"];
        }
    }
}
