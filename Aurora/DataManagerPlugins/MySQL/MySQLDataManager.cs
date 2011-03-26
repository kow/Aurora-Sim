using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using C5;
using MySql.Data.MySqlClient;
using Aurora.DataManager;
using Aurora.Framework;
using OpenSim.Framework;
using OpenMetaverse;
using Aurora.DataManager.Migration;

namespace Aurora.DataManager.MySQL
{
    public class MySQLDataLoader : DataManagerBase
    {
        private string connectionString = "";
        private MySqlConnection m_connection = null;

        public override string Identifier
        {
            get { return "MySQLData"; }
        }

        public MySqlConnection GetLockedConnection()
        {
            if (m_connection == null)
            {
                m_connection = new MySqlConnection(connectionString);
                m_connection.Open();
                return m_connection;
            }
            else
            {
                MySqlConnection clone = (MySqlConnection)((ICloneable)m_connection).Clone();
                //MySqlConnection clone = m_connection.Clone();
                clone.Open();
                return clone;
            }
        }

        public IDbCommand Query(string sql, Dictionary<string, object> parameters, MySqlConnection dbcon)
        {
            MySqlCommand dbcommand;
            try
            {
                dbcommand = (MySqlCommand)dbcon.CreateCommand();
                dbcommand.CommandText = sql;
                foreach (System.Collections.Generic.KeyValuePair<string, object> param in parameters)
                {
                    dbcommand.Parameters.AddWithValue(param.Key, param.Value);
                }
                return (IDbCommand)dbcommand;
            }
            catch (Exception)
            {
                // Return null if it fails.
                return null;
            }
        }

        public override void ConnectToDatabase(string connectionstring, string migratorName, bool validateTables)
        {
            connectionString = connectionstring;
            MySqlConnection dbcon = GetLockedConnection();
            CloseDatabase(dbcon);

            var migrationManager = new MigrationManager(this, migratorName, validateTables);
            migrationManager.DetermineOperation();
            migrationManager.ExecuteOperation();
        }

        public override List<string> Query(string keyRow, object keyValue, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result = null;
            IDataReader reader = null;
            List<string> RetVal = new List<string>();
            string query = "";
            if (keyRow == "")
            {
                query = String.Format("select {0} from {1}",
                                      wantedValue, table);
            }
            else
            {
                query = String.Format("select {0} from {1} where {2} = '{3}'",
                                      wantedValue, table, keyRow, keyValue.ToString());
            }
            try
            {
                using (result = Query(query, new Dictionary<string, object>(), dbcon))
                {
                    using (reader = result.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                if (reader[i] is byte[])
                                    RetVal.Add(OpenMetaverse.Utils.BytesToString((byte[])reader[i]));
                                else
                                    RetVal.Add(reader.GetString(i));
                            }
                        }
                        return RetVal;
                    }
                }
            }
            catch
            {
                return RetVal;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
                result.Dispose();
                CloseDatabase(dbcon);
            }
        }

        public override IDataReader QueryReader(string keyRow, object keyValue, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result = null;
            IDataReader reader = null;
            string query = "";
            if (keyRow == "")
            {
                query = String.Format("select {0} from {1}",
                                      wantedValue, table);
            }
            else
            {
                query = String.Format("select {0} from {1} where {2} = '{3}'",
                                      wantedValue, table, keyRow, keyValue.ToString());
            }
            try
            {
                result = Query(query, new Dictionary<string, object>(), dbcon);
                    reader = result.ExecuteReader();
                    return reader;
            }
            catch
            {
                return null;
            }
            finally
            {
            }
        }

        public override List<string> Query(string whereClause, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;
            List<string> RetVal = new List<string>();
            string query = String.Format("select {0} from {1} where {2}",
                                      wantedValue, table, whereClause);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (reader = result.ExecuteReader())
                {
                    try
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                if(reader[i] != DBNull.Value)
                                    RetVal.Add(reader.GetString(i));
                            }
                        }
                        return RetVal;
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
            }
        }

        public override List<string> QueryFullData(string whereClause, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;
            List<string> RetVal = new List<string>();
            string query = String.Format("select {0} from {1} {2}",
                                      wantedValue, table, whereClause);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (reader = result.ExecuteReader())
                {
                    try
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                RetVal.Add(reader.GetString(i));
                            }
                        }
                        return RetVal;
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
            }
        }

        public override IDataReader QueryDataFull(string whereClause, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = String.Format("select {0} from {1} {2}",
                                      wantedValue, table, whereClause);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                return result.ExecuteReader();
            }
        }

        public override IDataReader QueryData(string whereClause, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = String.Format("select {0} from {1} where {2}",
                                      wantedValue, table, whereClause);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                return result.ExecuteReader();
            }
        }

        public override List<string> Query(string keyRow, object keyValue, string table, string wantedValue, string order)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;
            List<string> RetVal = new List<string>();
            string query = "";
            if (keyRow == "")
            {
                query = String.Format("select {0} from {1}",
                                      wantedValue, table);
            }
            else
            {
                query = String.Format("select {0} from {1} where {2} = '{3}'",
                                      wantedValue, table, keyRow, keyValue);
            }
            using (result = Query(query + order, new Dictionary<string, object>(), dbcon))
            {
                using (reader = result.ExecuteReader())
                {
                    try
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Type r = reader[i].GetType();
                                if (r == typeof(DBNull))
                                    RetVal.Add(null);
                                else
                                    RetVal.Add(reader.GetString(i));
                            }
                        }
                        return RetVal;
                    }
                    catch (Exception)
                    {
                        return new List<string>();
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
            }
        }

        public override List<string> Query (string[] keyRow, object[] keyValue, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection ();
            IDbCommand result;
            IDataReader reader;
            List<string> RetVal = new List<string> ();
            string query = String.Format ("select {0} from {1} where ",
                                      wantedValue, table);
            int i = 0;
            foreach (object value in keyValue)
            {
                query += String.Format ("{0} = '{1}' and ", keyRow[i], value);
                i++;
            }
            query = query.Remove (query.Length - 5);


            using (result = Query (query, new Dictionary<string, object> (), dbcon))
            {
                using (reader = result.ExecuteReader ())
                {
                    try
                    {
                        while (reader.Read ())
                        {
                            for (i = 0; i < reader.FieldCount; i++)
                            {
                                Type r = reader[i].GetType ();
                                if (r == typeof (DBNull))
                                    RetVal.Add (null);
                                else
                                    RetVal.Add (reader.GetString (i));
                            }
                        }
                        return RetVal;
                    }
                    catch
                    {
                        return null;
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close ();
                            reader.Dispose ();
                        }
                        result.Dispose ();
                        CloseDatabase (dbcon);
                    }
                }
            }
        }

        public override Dictionary<string, List<string>> QueryNames (string[] keyRow, object[] keyValue, string table, string wantedValue)
        {
            MySqlConnection dbcon = GetLockedConnection ();
            IDbCommand result;
            IDataReader reader;
            Dictionary<string, List<string>> RetVal = new Dictionary<string, List<string>> ();
            string query = String.Format ("select {0} from {1} where ",
                                      wantedValue, table);
            int i = 0;
            foreach (object value in keyValue)
            {
                query += String.Format ("{0} = '{1}' and ", keyRow[i], value);
                i++;
            }
            query = query.Remove (query.Length - 5);


            using (result = Query (query, new Dictionary<string, object> (), dbcon))
            {
                using (reader = result.ExecuteReader ())
                {
                    try
                    {
                        while (reader.Read ())
                        {
                            for (i = 0; i < reader.FieldCount; i++)
                            {
                                Type r = reader[i].GetType ();
                                if (r == typeof (DBNull))
                                    AddValueToList (ref RetVal, reader.GetName (i), null);
                                else
                                    AddValueToList (ref RetVal, reader.GetName (i), reader[i].ToString ());
                            }
                        }
                        return RetVal;
                    }
                    catch
                    {
                        return null;
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close ();
                            reader.Dispose ();
                        }
                        result.Dispose ();
                        CloseDatabase (dbcon);
                    }
                }
            }
        }

        private void AddValueToList (ref Dictionary<string, List<string>> dic, string key, string value)
        {
            if (!dic.ContainsKey (key))
                dic.Add (key, new List<string> ());

            dic[key].Add (value);
        }

        public override bool Update(string table, object[] setValues, string[] setRows, string[] keyRows, object[] keyValues)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;
            string query = String.Format("update {0} set ", table);
            int i = 0;
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            foreach (object value in setValues)
            {
                query += string.Format("{0} = ?{1},", setRows[i], setRows[i]);
                string valueSTR = value.ToString();
                if(valueSTR == "")
                    valueSTR = " ";
                parameters["?" + setRows[i]] = valueSTR;
                i++;
            }
            i = 0;
            query = query.Remove(query.Length - 1);
            query += " where ";
            foreach (object value in keyValues)
            {
                query += String.Format("{0}  = '{1}' and ", keyRows[i], value);
                i++;
            }
            query = query.Remove(query.Length - 5);
            try
            {
                using (result = Query(query, parameters, dbcon))
                {
                    using (reader = result.ExecuteReader())
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
            }
            catch (MySqlException)
            {
            }
            return true;
        }

        public override bool Insert(string table, object[] values)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;

            string query = String.Format("insert into {0} values (", table);
            foreach (object value in values)
            {
                query += String.Format("'{0}',", value);
            }
            query = query.Remove(query.Length - 1);
            query += ")";

            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                try
                {
                    using (reader = result.ExecuteReader())
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
                catch { }
            }
            return true;
        }

        public override bool Insert(string table, string[] keys, object[] values)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;

            string query = String.Format("insert into {0} (", table);
            Dictionary<string, object> param = new Dictionary<string, object>();

            int i = 0;
            foreach (object key in keys)
            {
                param.Add("?" + key, values[i]);
                query += String.Format("{0},", key);
                i++;
            }
            query = query.Remove(query.Length - 1);
            query += ") values (";

            foreach (object key in keys)
            {
                query += String.Format("?{0},", key);
            }
            query = query.Remove(query.Length - 1);
            query += ")";

            using (result = Query(query, param, dbcon))
            {
                try
                {
                    using (result.ExecuteReader())
                    {
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
                catch { }
            }
            return true;
        }

        public override bool Replace(string table, string[] keys, object[] values)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            
            string query = String.Format("replace into {0} (", table);
            Dictionary<string, object> param = new Dictionary<string, object>();

            int i = 0;
            foreach (object key in keys)
            {
                string Key = key.ToString();
                if (key.ToString().Contains('`'))
                    Key = key.ToString().Replace("`", ""); //Remove them

                param.Add("?" + Key, values[i].ToString());
                query += "`" + Key + "`" + ",";
                i++;
            }
            query = query.Remove(query.Length - 1);
            query += ") values (";

            foreach (object key in keys)
            {
                string Key = key.ToString();
                if (key.ToString().Contains('`'))
                    Key = key.ToString().Replace("`", ""); //Remove them
                query += String.Format("?{0},", Key);
            }
            query = query.Remove(query.Length - 1);
            query += ")";

            using (result = Query(query, param, dbcon))
            {
                try
                {
                    using (result.ExecuteReader())
                    {
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        public override bool DirectReplace(string table, string[] keys, object[] values)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            
            string query = String.Format("replace into {0} (", table);
            Dictionary<string, object> param = new Dictionary<string, object>();

            int i = 0;
            foreach (object key in keys)
            {
                string Key = key.ToString();
                if (key.ToString().Contains('`'))
                    Key = key.ToString().Replace("`", ""); //Remove them

                query += "`" + Key + "`" + ",";
                i++;
            }
            query = query.Remove(query.Length - 1);
            query += ") values (";

            foreach (object key in values)
            {
                string Key = key.ToString();
                query += String.Format("{0},", Key);
            }
            query = query.Remove(query.Length - 1);
            query += ")";

            using (result = Query(query, param, dbcon))
            {
                try
                {
                    using (result.ExecuteReader())
                    {
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Insert(string table, object[] values, string updateKey, object updateValue)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = String.Format("insert into {0} VALUES('", table);
            foreach (object value in values)
            {
                query += value + "','";
            }
            query = query.Remove(query.Length - 2);
            query += String.Format(") ON DUPLICATE KEY UPDATE {0} = '{1}'", updateKey, updateValue);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (result.ExecuteReader())
                {
                    result.Dispose();
                    CloseDatabase(dbcon);
                }
            }
            return true;
        }

        public override bool Delete(string table, string[] keys, object[] values)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = "delete from " + table + (keys.Length > 0 ? " WHERE " : "");
            int i = 0;
            foreach (object value in values)
            {
                query += keys[i] + " = '" + value.ToString() + "' AND ";
                i++;
            }
            if(keys.Length > 0)
                query = query.Remove(query.Length - 5);
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (result.ExecuteReader())
                {
                    result.Dispose();
                }
            }
            CloseDatabase(dbcon);
            return true;
        }

        public override string FormatDateTimeString(int time)
        {
            if (time == 0)
                return "now()";
            return "date_add(now(), interval " + time.ToString() + " minute)";
        }

        public override string IsNull(string Field, string defaultValue)
        {
            return "IFNULL(" + Field + "," + defaultValue + ")";
        }

        public override string ConCat(string[] toConcat)
        {
            string returnValue = "concat(";
            foreach (string s in toConcat)
            {
                returnValue += s + ",";
            }
            return returnValue.Substring(0, returnValue.Length - 1) + ")";
        }

        public override bool Delete(string table, string whereclause)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = "delete from " + table + " WHERE " + whereclause;
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (result.ExecuteReader ())
                {
                    result.Dispose();
                }
            }
            CloseDatabase(dbcon);
            return true;
        }

        public override bool DeleteByTime(string table, string key)
        {
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            string query = "delete from " + table + " WHERE '" + key + "' < now()";
            using (result = Query(query, new Dictionary<string, object>(), dbcon))
            {
                using (result.ExecuteReader ())
                {
                    result.Dispose();
                }
            }
            CloseDatabase(dbcon);
            return true;
        }

        public void CloseDatabase(MySqlConnection connection)
        {
            connection.Close();
            connection.Dispose();
        }

        public override void CloseDatabase()
        {
            m_connection.Close();
            m_connection.Dispose();
        }

        public override void CreateTable(string table, ColumnDefinition[] columns)
        {
            table = table.ToLower();
            if (TableExists(table))
            {
                throw new DataManagerException("Trying to create a table with name of one that already exists.");
            }

            string columnDefinition = string.Empty;
            var primaryColumns = (from cd in columns where cd.IsPrimary == true select cd);
            bool multiplePrimary = primaryColumns.Count() > 1;

            foreach (ColumnDefinition column in columns)
            {
                if (columnDefinition != string.Empty)
                {
                    columnDefinition += ", ";
                }
                columnDefinition += "`" + column.Name + "` " + GetColumnTypeStringSymbol(column.Type) + ((column.IsPrimary && !multiplePrimary) ? " PRIMARY KEY" : string.Empty);
            }

            string multiplePrimaryString = string.Empty;
            if (multiplePrimary)
            {
                string listOfPrimaryNamesString = string.Empty;
                foreach (ColumnDefinition column in primaryColumns)
                {
                    if (listOfPrimaryNamesString != string.Empty)
                    {
                        listOfPrimaryNamesString += ", ";
                    }
                    listOfPrimaryNamesString += "`" + column.Name + "`";
                }
                multiplePrimaryString = string.Format(", PRIMARY KEY ({0}) ", listOfPrimaryNamesString);
            }

            string query = string.Format("create table " + table + " ( {0} {1}) ", columnDefinition, multiplePrimaryString);

            MySqlConnection dbcon = GetLockedConnection();
            MySqlCommand dbcommand = dbcon.CreateCommand();
            dbcommand.CommandText = query;
            dbcommand.ExecuteNonQuery();
            CloseDatabase(dbcon);
        }

        public override void UpdateTable(string table, ColumnDefinition[] columns)
        {
            table = table.ToLower();
            if (!TableExists(table))
            {
                throw new DataManagerException("Trying to update a table with name of one that does not exist.");
            }

            List<ColumnDefinition> oldColumns = ExtractColumnsFromTable(table);

            Dictionary<string, ColumnDefinition> addedColumns = new Dictionary<string, ColumnDefinition>();
            Dictionary<string, ColumnDefinition> removedColumns = new Dictionary<string, ColumnDefinition>();
            Dictionary<string, ColumnDefinition> modifiedColumns = new Dictionary<string, ColumnDefinition>();
            foreach (ColumnDefinition column in columns)
            {
                if (!oldColumns.Contains(column))
                {
                    addedColumns.Add(column.Name, column);
                }
            }
            foreach (ColumnDefinition column in oldColumns)
            {
                if (!columns.Contains(column))
                {
                    if (addedColumns.ContainsKey(column.Name))
                    {
                        modifiedColumns.Add(column.Name, addedColumns[column.Name]);
                        addedColumns.Remove(column.Name);
                    }
                    else
                        removedColumns.Add(column.Name, column);
                }
            }

            string addedColumnsQuery = "";
            string modifiedColumnsQuery = "";
            string droppedColumnsQuery = "";
            MySqlConnection dbcon = GetLockedConnection();
            foreach (ColumnDefinition column in addedColumns.Values)
            {
                addedColumnsQuery = "add " + column.Name + " " + GetColumnTypeStringSymbol(column.Type) + " ";
                string query = string.Format("alter table " + table + " " + addedColumnsQuery);

                MySqlCommand dbcommand = dbcon.CreateCommand();
                dbcommand.CommandText = query;
                try
                {
                    dbcommand.ExecuteNonQuery();
                }
                catch
                {
                }
            }
            foreach (ColumnDefinition column in modifiedColumns.Values)
            {
                modifiedColumnsQuery = "modify column " + column.Name + " " + GetColumnTypeStringSymbol(column.Type) + " ";
                string query = string.Format("alter table " + table + " " + modifiedColumnsQuery);

                MySqlCommand dbcommand = dbcon.CreateCommand();
                dbcommand.CommandText = query;
                try
                {
                    dbcommand.ExecuteNonQuery();
                }
                catch
                {
                }
            }
            foreach (ColumnDefinition column in removedColumns.Values)
            {
                droppedColumnsQuery = "drop " + column.Name + " ";
                string query = string.Format("alter table " + table + " " + droppedColumnsQuery);

                MySqlCommand dbcommand = dbcon.CreateCommand();
                dbcommand.CommandText = query;
                try
                {
                    dbcommand.ExecuteNonQuery();
                }
                catch
                {
                }
            }
            CloseDatabase(dbcon);
        }

        public override string GetColumnTypeStringSymbol(ColumnTypes type)
        {
            switch (type)
            {
                case ColumnTypes.Integer11:
                    return "int(11)";
                case ColumnTypes.Integer30:
                    return "int(30)";
                case ColumnTypes.Char36:
                    return "char(36)";
                case ColumnTypes.Char32:
                    return "char(32)";
                case ColumnTypes.String:
                    return "TEXT";
                case ColumnTypes.String1:
                    return "VARCHAR(1)";
                case ColumnTypes.String2:
                    return "VARCHAR(2)";
                case ColumnTypes.String16:
                    return "VARCHAR(16)";
                case ColumnTypes.String32:
                    return "VARCHAR(32)";
                case ColumnTypes.String36:
                    return "VARCHAR(36)";
                case ColumnTypes.String45:
                    return "VARCHAR(45)";
                case ColumnTypes.String50:
                    return "VARCHAR(50)";
                case ColumnTypes.String64:
                    return "VARCHAR(64)";
                case ColumnTypes.String128:
                    return "VARCHAR(128)";
                case ColumnTypes.String100:
                    return "VARCHAR(100)";
                case ColumnTypes.String255:
                    return "VARCHAR(255)";
                case ColumnTypes.String512:
                    return "VARCHAR(512)";
                case ColumnTypes.String1024:
                    return "VARCHAR(1024)";
                case ColumnTypes.String8196:
                    return "VARCHAR(8196)";
                case ColumnTypes.Text:
                    return "TEXT";
                case ColumnTypes.Blob:
                    return "blob";
                case ColumnTypes.LongBlob:
                    return "longblob";
                case ColumnTypes.Date:
                    return "DATE";
                case ColumnTypes.DateTime:
                    return "DATETIME";
                default:
                    throw new DataManagerException("Unknown column type.");
            }
        }

        public override void DropTable(string tableName)
        {
            tableName = tableName.ToLower();
            MySqlConnection dbcon = GetLockedConnection();
            MySqlCommand dbcommand = dbcon.CreateCommand();
            dbcommand.CommandText = string.Format("drop table {0}", tableName);
            dbcommand.ExecuteNonQuery();
            CloseDatabase(dbcon);
        }

        public override void ForceRenameTable(string oldTableName, string newTableName)
        {
            newTableName = newTableName.ToLower();
            MySqlConnection dbcon = GetLockedConnection();
            MySqlCommand dbcommand = dbcon.CreateCommand();
            dbcommand.CommandText = string.Format("RENAME TABLE {0} TO {1}", oldTableName, newTableName);
            dbcommand.ExecuteNonQuery();
            CloseDatabase(dbcon);
        }

        protected override void CopyAllDataBetweenMatchingTables(string sourceTableName, string destinationTableName, ColumnDefinition[] columnDefinitions)
        {
            sourceTableName = sourceTableName.ToLower();
            destinationTableName = destinationTableName.ToLower();
            MySqlConnection dbcon = GetLockedConnection();
            MySqlCommand dbcommand = dbcon.CreateCommand();
            dbcommand.CommandText = string.Format("insert into {0} select * from {1}", destinationTableName, sourceTableName);
            dbcommand.ExecuteNonQuery();
            CloseDatabase(dbcon);
        }

        public override bool TableExists(string table)
        {
            table = table.ToLower();
            var ret = false;
            MySqlConnection dbcon = GetLockedConnection();
            IDbCommand result;
            IDataReader reader;
            List<string> RetVal = new List<string>();
            using (result = Query("show tables", new Dictionary<string, object>(), dbcon))
            {
                using (reader = result.ExecuteReader())
                {
                    try
                    {
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                RetVal.Add(reader.GetString(i));
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (reader != null)
                        {
                            reader.Close();
                            reader.Dispose();
                        }
                        result.Dispose();
                        CloseDatabase(dbcon);
                    }
                }
            }
            if (RetVal.Contains(table))
            {
                ret = true;
            }
            return ret;
        }

        protected override List<ColumnDefinition> ExtractColumnsFromTable(string tableName)
        {
            var defs = new List<ColumnDefinition>();
            tableName = tableName.ToLower();

            MySqlConnection dbcon = GetLockedConnection();
            MySqlCommand dbcommand = dbcon.CreateCommand();
            dbcommand.CommandText = string.Format("desc {0}", tableName);
            var rdr = dbcommand.ExecuteReader();
            while (rdr.Read())
            {
                var name = rdr["Field"];
                var pk = rdr["Key"];
                var type = rdr["Type"];
                defs.Add(new ColumnDefinition { Name = name.ToString(), IsPrimary = pk.ToString()=="PRI", Type = ConvertTypeToColumnType(type.ToString()) });
            }
            rdr.Close();
            rdr.Dispose();
            dbcommand.Dispose();
            CloseDatabase(dbcon);
            return defs;
        }

        private ColumnTypes ConvertTypeToColumnType(string typeString)
        {
            string tStr = typeString.ToLower();
            //we'll base our names on lowercase
            switch (tStr)
            {
                case "int(11)":
                    return ColumnTypes.Integer11;
                case "int(30)":
                    return ColumnTypes.Integer30;
                case "integer":
                    return ColumnTypes.Integer11;
                case "char(36)":
                    return ColumnTypes.Char36;
                case "char(32)":
                    return ColumnTypes.Char32;
                case "varchar(1)":
                    return ColumnTypes.String1;
                case "varchar(2)":
                    return ColumnTypes.String2;
                case "varchar(16)":
                    return ColumnTypes.String16;
                case "varchar(32)":
                    return ColumnTypes.String32;
                case "varchar(36)":
                    return ColumnTypes.String36;
                case "varchar(45)":
                    return ColumnTypes.String45;
                case "varchar(50)":
                    return ColumnTypes.String50;
                case "varchar(64)":
                    return ColumnTypes.String64;
                case "varchar(128)":
                    return ColumnTypes.String128;
                case "varchar(100)":
                    return ColumnTypes.String100;
                case "varchar(255)":
                    return ColumnTypes.String255;
                case "varchar(512)":
                    return ColumnTypes.String512;
                case "varchar(1024)":
                    return ColumnTypes.String1024;
                case "date":
                    return ColumnTypes.Date;
                case "datetime":
                    return ColumnTypes.DateTime;
                case "varchar(8196)":
                    return ColumnTypes.String8196;
                case "text":
                    return ColumnTypes.Text;
                case "blob":
                    return ColumnTypes.Blob;
                case "longblob":
                    return ColumnTypes.LongBlob;
                case "smallint(6)":
                    return ColumnTypes.Integer11;
                case "int(10)":
                    return ColumnTypes.Integer11;
                case "tinyint(4)":
                    return ColumnTypes.Integer11;
            }
            if (tStr.StartsWith ("varchar"))
            {
                //... Someone was editing the database
                // Swallow the exception... but set it to the highest setting so we don't break anything
                return ColumnTypes.String8196;
            }
            if (tStr.StartsWith ("int"))
            {
                //... Someone was editing the database
                // Swallow the exception... but set it to the highest setting so we don't break anything
                return ColumnTypes.Integer11;
            }
            throw new Exception("You've discovered some type in MySQL that's not reconized by Aurora, please place the correct conversion in ConvertTypeToColumnType. Type: " + tStr);
        }

        public override IGenericData Copy()
        {
            return new MySQLDataLoader();
        }
    }
}

