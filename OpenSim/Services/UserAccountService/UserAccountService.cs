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
using System.Reflection;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Aurora.Framework;
using Aurora.Simulation.Base;
using OpenSim.Services.Connectors;

namespace OpenSim.Services.UserAccountService
{
    public class UserAccountService : IUserAccountService, IService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IGridService m_GridService;
        protected IAuthenticationService m_AuthenticationService;
        protected IInventoryService m_InventoryService;
        protected IUserAccountData m_Database = null;
        protected UserAccountCache m_cache = new UserAccountCache ();

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public virtual IUserAccountService InnerService
        {
            get { return this; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            IConfig handlerConfig = config.Configs["Handlers"];
            if (handlerConfig.GetString("UserAccountHandler", "") != Name)
                return;
            Configure(config, registry);
        }
        public void Configure(IConfigSource config, IRegistryCore registry)
        {
            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("UserService", false,
                        "create user",
                        "create user [<first> [<last> [<pass> [<email>]]]]",
                        "Create a new user", HandleCreateUser);
                MainConsole.Instance.Commands.AddCommand("UserService", false, "reset user password",
                        "reset user password [<first> [<last> [<password>]]]",
                        "Reset a user password", HandleResetUserPassword);
            }
            registry.RegisterModuleInterface<IUserAccountService>(this);
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            m_GridService = registry.RequestModuleInterface<IGridService>();
            m_AuthenticationService = registry.RequestModuleInterface<IAuthenticationService>();
            m_InventoryService = registry.RequestModuleInterface<IInventoryService>();
            m_Database = Aurora.DataManager.DataManager.RequestPlugin<IUserAccountData>();
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");
        }

        public void FinishedStartup()
        {
        }

        #region IUserAccountService

        public UserAccount GetUserAccount(UUID scopeID, string firstName,
                string lastName)
        {
//            m_log.DebugFormat(
//                "[USER ACCOUNT SERVICE]: Retrieving account by username for {0} {1}, scope {2}",
//                firstName, lastName, scopeID);

            UserAccount[] d;

            UserAccount account;
            if (m_cache.Get (firstName + " " + lastName, out account))
                return account;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "FirstName", "LastName" },
                        new string[] { scopeID.ToString(), firstName, lastName });
                if (d.Length < 1)
                {
                    d = m_Database.Get(
                            new string[] { "ScopeID", "FirstName", "LastName" },
                            new string[] { UUID.Zero.ToString(), firstName, lastName });
                }
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "FirstName", "LastName" },
                        new string[] { firstName, lastName });
            }

            if (d.Length < 1)
                return null;

            m_cache.Cache (d[0].PrincipalID, d[0]);
            return d[0];
        }

        public UserAccount GetUserAccount(UUID scopeID, string name)
        {
            UserAccount[] d;

            UserAccount account;
            if (m_cache.Get (name, out account))
                return account;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "Name" },
                        new string[] { scopeID.ToString(), name });
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "Name" },
                        new string[] { name });
            }

            if (d.Length < 1)
            {
                string[] split = name.Split(' ');
                if (split.Length == 2)
                    return GetUserAccount(scopeID, split[0], split[1]);
                
                return null;
            }

            m_cache.Cache (d[0].PrincipalID, d[0]);
            return d[0];
        }

        public UserAccount GetUserAccount(UUID scopeID, UUID principalID)
        {
            UserAccount[] d;

            UserAccount account;
            if (m_cache.Get (principalID, out account))
                return account;

            if (scopeID != UUID.Zero)
            {
                d = m_Database.Get(
                        new string[] { "ScopeID", "PrincipalID" },
                        new string[] { scopeID.ToString(), principalID.ToString() });
                if (d.Length < 1)
                {
                    d = m_Database.Get(
                            new string[] { "ScopeID", "PrincipalID" },
                            new string[] { UUID.Zero.ToString(), principalID.ToString() });
                }
            }
            else
            {
                d = m_Database.Get(
                        new string[] { "PrincipalID" },
                        new string[] { principalID.ToString() });
            }

            if (d.Length < 1)
            {
                m_cache.Cache (principalID, null);
                return null;
            }

            m_cache.Cache (principalID, d[0]);
            return d[0];
        }

        public bool StoreUserAccount(UserAccount data)
        {
//            m_log.DebugFormat(
//                "[USER ACCOUNT SERVICE]: Storing user account for {0} {1} {2}, scope {3}",
//                data.FirstName, data.LastName, data.PrincipalID, data.ScopeID);

            if (data.UserTitle != null)
                data.UserTitle = "";

            return m_Database.Store(data);
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            UserAccount[] d = m_Database.GetUsers(scopeID, query);

            if (d == null)
                return new List<UserAccount>();

            List<UserAccount> ret = new List<UserAccount>(d);
            return ret;
        }

        #endregion

        #region Console commands

        /// <summary>
        /// Handle the create user command from the console.
        /// </summary>
        /// <param name="cmdparams">string array with parameters: firstname, lastname, password, locationX, locationY, email</param>
        protected void HandleCreateUser(string module, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string password;
            string email;

            List<char> excluded = new List<char>(new char[] { ' ' });

            if (cmdparams.Length < 3)
                firstName = MainConsole.Instance.CmdPrompt("First name", "Default", excluded);
            else firstName = cmdparams[2];

            if (cmdparams.Length < 4)
                lastName = MainConsole.Instance.CmdPrompt("Last name", "User", excluded);
            else lastName = cmdparams[3];

            if (cmdparams.Length < 5)
                password = MainConsole.Instance.PasswdPrompt("Password");
            else password = cmdparams[4];

            if (cmdparams.Length < 6)
                email = MainConsole.Instance.CmdPrompt("Email", "");
            else email = cmdparams[5];

            CreateUser(firstName + " " + lastName, Util.Md5Hash(password), email);
        }

        protected void HandleResetUserPassword(string module, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string newPassword;

            if (cmdparams.Length < 4)
                firstName = MainConsole.Instance.CmdPrompt("First name");
            else firstName = cmdparams[3];

            if (cmdparams.Length < 5)
                lastName = MainConsole.Instance.CmdPrompt("Last name");
            else lastName = cmdparams[4];

            if (cmdparams.Length < 6)
                newPassword = MainConsole.Instance.PasswdPrompt("New password");
            else newPassword = cmdparams[5];

            UserAccount account = GetUserAccount(UUID.Zero, firstName, lastName);
            if (account == null)
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: No such user");

            bool success = false;
            if (m_AuthenticationService != null)
                success = m_AuthenticationService.SetPassword(account.PrincipalID, newPassword);
            if (!success)
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: Unable to reset password for account {0} {1}.",
                   firstName, lastName);
            else
                m_log.InfoFormat("[USER ACCOUNT SERVICE]: Password reset for user {0} {1}", firstName, lastName);
        }

        #endregion

        /// <summary>
        /// Create a user
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        /// <param name="email"></param>
        public void CreateUser(string name, string password, string email)
        {
            UserAccount account = GetUserAccount(UUID.Zero, name);
            if (null == account)
            {
                account = new UserAccount(UUID.Zero, name, email);
                if (account.ServiceURLs == null || (account.ServiceURLs != null && account.ServiceURLs.Count == 0))
                {
                    account.ServiceURLs = new Dictionary<string, object>();
                    account.ServiceURLs["HomeURI"] = string.Empty;
                    account.ServiceURLs["GatekeeperURI"] = string.Empty;
                    account.ServiceURLs["InventoryServerURI"] = string.Empty;
                    account.ServiceURLs["AssetServerURI"] = string.Empty;
                }

                if (StoreUserAccount(account))
                {
                    bool success;
                    if (m_AuthenticationService != null && password != "")
                    {
                        success = m_AuthenticationService.SetPasswordHashed(account.PrincipalID, password);
                        if (!success)
                            m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to set password for account {0}.",
                                name);
                    }

                    if (m_InventoryService != null)
                    {
                        success = m_InventoryService.CreateUserInventory(account.PrincipalID);
                        if (!success)
                            m_log.WarnFormat("[USER ACCOUNT SERVICE]: Unable to create inventory for account {0}.",
                                name);
                    }

                    m_log.InfoFormat("[USER ACCOUNT SERVICE]: Account {0} created successfully", name);
                } else {
                    m_log.ErrorFormat("[USER ACCOUNT SERVICE]: Account creation failed for account {0}", name);
                }
            }
            else
            {
                m_log.ErrorFormat("[USER ACCOUNT SERVICE]: A user with the name {0} already exists!", name);
            }
        }
    }
}
