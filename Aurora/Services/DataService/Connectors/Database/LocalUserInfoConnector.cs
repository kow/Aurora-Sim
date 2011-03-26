using System;
using System.Collections.Generic;
using System.Reflection;
using Aurora.Framework;
using Aurora.DataManager;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using Nini.Config;
using log4net;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using RegionFlags = Aurora.Framework.RegionFlags;

namespace Aurora.Services.DataService
{
    public class LocalUserInfoConnector : IAgentInfoConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
		private IGenericData GD = null;
        private string m_realm = "userinfo";
        protected bool m_allowDuplicatePresences = true;
        protected bool m_checkLastSeen = true;

        public void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore simBase, string defaultConnectionString)
        {
            if(source.Configs["AuroraConnectors"].GetString("UserInfoConnector", "LocalConnector") == "LocalConnector")
            {
                GD = GenericData;

                string connectionString = defaultConnectionString;
                if (source.Configs[Name] != null)
                {
                    connectionString = source.Configs[Name].GetString("ConnectionString", defaultConnectionString);

                    m_allowDuplicatePresences =
                           source.Configs[Name].GetBoolean("AllowDuplicatePresences",
                                                     m_allowDuplicatePresences);
                    m_checkLastSeen =
                           source.Configs[Name].GetBoolean("CheckLastSeen",
                                                     m_checkLastSeen);
                }
                GD.ConnectToDatabase(connectionString, "UserInfo", source.Configs["AuroraConnectors"].GetBoolean("ValidateTables", true));

                DataManager.DataManager.RegisterPlugin(Name, this);
            }
        }

        public string Name
        {
            get { return "IAgentInfoConnector"; }
        }

        public void Dispose()
        {
        }

        #region IUserInfoConnector Members

        public bool Set(UserInfo info)
        {
            object[] values = new object[13];
            values[0] = info.UserID;
            values[1] = info.CurrentRegionID;
            values[2] = Util.ToUnixTime(DateTime.Now); //Convert to binary so that it can be converted easily
            values[3] = info.IsOnline ? 1 : 0;
            values[4] = Util.ToUnixTime(info.LastLogin);
            values[5] = Util.ToUnixTime(info.LastLogout);
            values[6] = OSDParser.SerializeJsonString(info.Info);
            values[7] = info.CurrentRegionID.ToString();
            values[8] = info.CurrentPosition.ToString();
            values[9] = info.CurrentLookAt.ToString();
            values[10] = info.HomeRegionID.ToString();
            values[11] = info.HomePosition.ToString();
            values[12] = info.HomeLookAt.ToString();
            GD.Delete(m_realm, new string[1] { "UserID" }, new object[1] { info.UserID });
            return GD.Insert(m_realm, values);
        }

        public void SetLastPosition(string userID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            string[] keys = new string[5];
            keys[0] = "CurrentRegionID";
            keys[1] = "CurrentPosition";
            keys[2] = "CurrentLookat";
            keys[3] = "LastSeen";//Set the last seen and is online since if the user is moving, they are sending updates
            keys[4] = "IsOnline";
            object[] values = new object[5];
            values[0] = regionID;
            values[1] = lastPosition;
            values[2] = lastLookAt;
            values[3] = Util.ToUnixTime (DateTime.Now); //Convert to binary so that it can be converted easily
            values[4] = 1;
            GD.Update (m_realm, values, keys, new string[1] { "UserID" }, new object[1] { userID });
        }

        public void SetHomePosition(string userID, UUID regionID, Vector3 Position, Vector3 LookAt)
        {
            string[] keys = new string[4];
            keys[0] = "HomeRegionID";
            keys[1] = "LastSeen";
            keys[2] = "HomePosition";
            keys[3] = "HomeLookat";
            object[] values = new object[4];
            values[0] = regionID;
            values[1] = Util.ToUnixTime (DateTime.Now); //Convert to binary so that it can be converted easily
            values[2] = Position;
            values[3] = LookAt;
            GD.Update(m_realm, values, keys, new string[1] { "UserID" }, new object[1] { userID });
        }

        public UserInfo Get(string userID)
        {
            List<string> query = GD.Query("UserID", userID, m_realm, "*");
            if (query.Count == 0)
                return null;
            UserInfo user = new UserInfo();
            user.UserID = query[0];
            user.CurrentRegionID = UUID.Parse(query[1]);
            user.IsOnline = query[3] == "1" ? true : false;
            user.LastLogin = Util.ToDateTime(int.Parse(query[4]));
            user.LastLogout = Util.ToDateTime(int.Parse(query[5]));
            user.Info = (OSDMap)OSDParser.DeserializeJson(query[6]);
            try
            {
                user.CurrentRegionID = UUID.Parse(query[7]);
                if(query[8] != "")
                    user.CurrentPosition = Vector3.Parse(query[8]);
                if (query[9] != "")
                    user.CurrentLookAt = Vector3.Parse(query[9]);
                user.HomeRegionID = UUID.Parse(query[10]);
                if (query[11] != "")
                    user.HomePosition = Vector3.Parse(query[11]);
                if (query[12] != "")
                    user.HomeLookAt = Vector3.Parse(query[12]);
            }
            catch
            {
            }

            //Check LastSeen
            if (m_checkLastSeen && user.IsOnline && (Util.ToDateTime(int.Parse(query[2])).AddHours(1) < DateTime.Now))
            {
                m_log.Warn("[UserInfoService]: Found a user (" + user.UserID + ") that was not seen within the last hour! Logging them out.");
                user.IsOnline = false;
                Set(user);
            }
            return user;
        }

        #endregion
    }
}
