using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Aurora.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace Aurora.Framework
{
    public interface IProfileConnector : IAuroraDataPlugin
	{
        /// <summary>
        /// Gets the profile for an agent
        /// </summary>
        /// <param name="agentID"></param>
        /// <returns></returns>
        IUserProfileInfo GetUserProfile(UUID agentID);

        /// <summary>
        /// Updates the user's profile (Note: the user must already have a profile created)
        /// </summary>
        /// <param name="Profile"></param>
        /// <returns></returns>
        bool UpdateUserProfile(IUserProfileInfo Profile);

        /// <summary>
        /// Creates an new profile for the user
        /// </summary>
        /// <param name="UUID"></param>
        void CreateNewProfile (UUID UUID);

        bool AddClassified (Classified classified);
        Classified GetClassified (UUID queryClassifiedID);
        List<Classified> GetClassifieds (UUID ownerID);
        void RemoveClassified (UUID queryClassifiedID);

        bool AddPick (ProfilePickInfo pick);
        ProfilePickInfo GetPick (UUID queryPickID);
        List<ProfilePickInfo> GetPicks (UUID ownerID);
        void RemovePick (UUID queryPickID);
    }
}
