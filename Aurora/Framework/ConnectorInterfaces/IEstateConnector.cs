using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using Aurora.Framework;
using OpenSim.Framework;

namespace Aurora.Framework
{
    public interface IEstateConnector : IAuroraDataPlugin
	{
        /// <summary>
        /// Loads the estate data for the given region
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
		bool LoadEstateSettings(UUID regionID, out EstateSettings settings);

        /// <summary>
        /// Updates the given Estate data in the database
        /// </summary>
        /// <param name="es"></param>
        void SaveEstateSettings(EstateSettings es);

        /// <summary>
        /// Gets the estates that have the given name
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        List<int> GetEstates(string name);

        /// <summary>
        /// Get all regions in the current estate
        /// </summary>
        /// <param name="estateID"></param>
        /// <returns></returns>
        List<UUID> GetRegions(uint estateID);

        /// <summary>
        /// Gets the estates that have the given owner
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        List<EstateSettings> GetEstates(UUID OwnerID);

        /// <summary>
        /// Add a new region to the estate, authenticates with the password
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="estateID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        bool LinkRegion(UUID regionID, int estateID, string password);

        /// <summary>
        /// Remove an existing region from the estate, authenticates with the password
        /// </summary>
        /// <param name="regionID"></param>
        /// <param name="estateID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        bool DelinkRegion(UUID regionID, string password);

        /// <summary>
        /// Deletes the given estate by its estate ID, must be authenticated with the password
        /// </summary>
        /// <param name="estateID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
		bool DeleteEstate(int estateID, string password);

        /// <summary>
        /// Creates a new estate from the given info, returns the updated info
        /// </summary>
        /// <param name="ES"></param>
        /// <param name="RegionID"></param>
        /// <returns></returns>
        EstateSettings CreateEstate(EstateSettings ES, UUID RegionID);
    }
}
