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

using System.Collections.Generic;
using System.Data;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.Framework
{
    /// <summary>
    /// OpenSimulator Application Plugin framework interface
    /// </summary>
    public interface IApplicationPlugin: IPlugin
    {
        /// <summary>
        /// Initialize the Plugin
        /// </summary>
        /// <param name="openSim">The Application instance</param>
        void Initialize(ISimulationBase openSim);

        /// <summary>
        /// Called when the application initialization is completed 
        /// </summary>
        void PostInitialise();

        /// <summary>
        /// Called when the application loading is completed 
        /// </summary>
        void Start();

        /// <summary>
        /// Called when the application loading is completed 
        /// </summary>
        void PostStart();

        /// <summary>
        /// Close out the module
        /// </summary>
        void Close();

        /// <summary>
        /// The configuration has changed, make sure that everything is updated with the new info
        /// </summary>
        /// <param name="m_config"></param>
        void ReloadConfiguration(IConfigSource m_config);
    }
}
namespace Aurora.Framework
{
    public interface IGenericData
    {
        /// <summary>
        /// update 'table' set 'setRow' = 'setValue' WHERE 'keyRow' = 'keyValue'
        /// </summary>
        bool Update(string table, object[] setValues, string[] setRows, string[] keyRows, object[] keyValues);
        
        /// <summary>
        /// select 'wantedValue' from 'table' where 'keyRow' = 'keyValue'
        /// </summary>
        List<string> Query(string keyRow, object keyValue, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'whereClause'
        /// </summary>
        List<string> Query(string whereClause, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' 'whereClause'
        /// </summary>
        List<string> QueryFullData(string whereClause, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' 'whereClause'
        /// </summary>
        IDataReader QueryDataFull(string whereClause, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'whereClause'
        /// </summary>
        IDataReader QueryData(string whereClause, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'keyRow' = 'keyValue' 'Order'
        /// </summary>
        List<string> Query (string keyRow, object keyValue, string table, string wantedValue, string Order);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'keyRow' = 'keyValue'
        /// </summary>
        List<string> Query (string[] keyRow, object[] keyValue, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'keyRow' = 'keyValue'
        /// This gives the row names as well as the values
        /// </summary>
        Dictionary<string, List<string>> QueryNames (string[] keyRow, object[] keyValue, string table, string wantedValue);

        /// <summary>
        /// select 'wantedValue' from 'table' where 'keyRow' = 'keyValue'
        /// </summary>
        IDataReader QueryReader(string keyRow, object keyValue, string table, string wantedValue);

        /// <summary>
        /// insert into 'table' values ('values')
        /// </summary>
        bool Insert(string table, object[] values);
        
        /// <summary>
        /// insert into 'table' where 'keys' = 'values'
        /// </summary>
        /// <param name="table"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool Insert(string table, string[] keys, object[] values);

        /// <summary>
        /// delete from 'table' where 'keys' = 'values'
        /// </summary>
        /// <param name="table"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool Delete(string table, string[] keys, object[] values);

        /// <summary>
        /// Formats a datetime string for the given time
        /// 0 returns now()
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        string FormatDateTimeString(int time);

        /// <summary>
        /// delete from 'table' where 'key' < now()
        /// </summary>
        /// <param name="table"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool DeleteByTime(string table, string keys);

        /// <summary>
        /// delete from 'table' where whereclause
        /// </summary>
        /// <param name="table"></param>
        /// <param name="whereclause"></param>
        /// <returns></returns>
        bool Delete(string table, string whereclause);

        /// <summary>
        /// Replace into 'table' ('keys') values ('values')
        /// </summary>
        /// <param name="table"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool Replace(string table, string[] keys, object[] values);

        /// <summary>
        /// Same as replace, but without any '' around the values
        /// </summary>
        /// <param name="table"></param>
        /// <param name="keys"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        bool DirectReplace(string table, string[] keys, object[] values);

        /// <summary>
        /// Inserts a row into the database 
        /// insert into 'table' values ('values') ON DUPLICATE KEY UPDATE 'updateKey' = 'updateValue'
        /// </summary>
        /// <param name="table">table name</param>
        /// <param name="values">All values to be inserted in the correct table order</param>
        /// <param name="updateKey">If a row is already existing, update this key</param>
        /// <param name="updateValue">If a row is already existing, update this value</param>
        /// <returns></returns>
        bool Insert(string table, object[] values, string updateKey, object updateValue);
        
        /// <summary>
        /// Connects to the database and then performs migrations
        /// </summary>
        /// <param name="connectionString"></param>
        void ConnectToDatabase(string connectionString, string migrationName, bool validateTables);

        /// <summary>
        /// Makes a copy of the IGenericData plugin
        /// </summary>
        /// <returns></returns>
        IGenericData Copy();

        /// <summary>
        /// Returns alternative value if field is null
        /// </summary>
        /// <param name="Field"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        string IsNull(string Field, string defaultValue);

        /// <summary>
        /// in the sql the strings will return joined fields
        /// </summary>
        /// <param name="toConCat"></param>
        /// <returns></returns>
        string ConCat(string[] toConCat);
    }

    public interface IAuroraDataPlugin : IPlugin
    {
        /// <summary>
        /// Starts the database plugin, performs migrations if needed
        /// </summary>
        /// <param name="GenericData">The Database Plugin</param>
        /// <param name="source">Config if more parameters are needed</param>
        /// <param name="DefaultConnectionString">The connection string to use</param>
        void Initialize(IGenericData GenericData, IConfigSource source, IRegistryCore simBase, string DefaultConnectionString);
    }
}