// ----------------------------------------------------------------------------
// <copyright file="AuthenticationValues.cs" company="Exit Games GmbH">
// Photon Realtime API - Copyright (C) 2022 Exit Games GmbH
// </copyright>
// <summary>
// Provides operations implemented by Photon servers.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------


#if UNITY_2017_4_OR_NEWER
#define SUPPORTED_UNITY
#endif


namespace Photon.Realtime
{
    using System;
    using System.Text.RegularExpressions;
    using System.Collections.Generic;


    /// <summary>
    /// Container for user authentication in Photon. Set AuthValues before you connect - all else is handled.
    /// </summary>
    /// <remarks>
    /// On Photon, user authentication is optional but can be useful in many cases.
    /// If you want to FindFriends, a unique ID per user is very practical.
    ///
    /// There are basically three options for user authentication: None at all, the client sets some UserId
    /// or you can use some account web-service to authenticate a user (and set the UserId server-side).
    ///
    /// Custom Authentication lets you verify end-users by some kind of login or token. It sends those
    /// values to Photon which will verify them before granting access or disconnecting the client.
    ///
    /// The AuthValues are sent in OpAuthenticate when you connect, so they must be set before you connect.
    /// If the AuthValues.UserId is null or empty when it's sent to the server, then the Photon Server assigns a UserId!
    ///
    /// The Photon Cloud Dashboard will let you enable this feature and set important server values for it.
    /// https://dashboard.photonengine.com
    /// </remarks>
    public class AuthenticationValues
    {
        /// <summary>See AuthType.</summary>
        private CustomAuthenticationType authType = CustomAuthenticationType.None;

        /// <summary>The type of authentication provider that should be used. Defaults to None (no auth whatsoever).</summary>
        /// <remarks>Several auth providers are available and CustomAuthenticationType.Custom can be used if you build your own service.</remarks>
        public CustomAuthenticationType AuthType
        {
            get { return this.authType; }
            set { this.authType = value; }
        }

        /// <summary>This string must contain any (http get) parameters expected by the used authentication service. By default, username and token.</summary>
        /// <remarks>
        /// Maps to operation parameter 216.
        /// Standard http get parameters are used here and passed on to the service that's defined in the server (Photon Cloud Dashboard).
        /// </remarks>
        public string AuthGetParameters { get; set; }

        /// <summary>Data to be passed-on to the auth service via POST. Default: null (not sent). Either string or byte[] (see setters).</summary>
        /// <remarks>Maps to operation parameter 214.</remarks>
        public object AuthPostData { get; private set; }

        /// <summary>Internal <b>Photon token</b>. After initial authentication, Photon provides a token for this client, subsequently used as (cached) validation.</summary>
        /// <remarks>Any token for custom authentication should be set via SetAuthPostData or AddAuthParameter.</remarks>
        protected internal object Token { get; set; }

        /// <summary>The UserId should be a unique identifier per user. This is for finding friends, etc..</summary>
        /// <remarks>See remarks of AuthValues for info about how this is set and used.</remarks>
        public string UserId { get; set; }


        /// <summary>Creates empty auth values without any info.</summary>
        public AuthenticationValues()
        {
        }

        /// <summary>Creates minimal info about the user. If this is authenticated or not, depends on the set AuthType.</summary>
        /// <param name="userId">Some UserId to set in Photon.</param>
        public AuthenticationValues(string userId)
        {
            this.UserId = userId;
        }

        /// <summary>Sets the data to be passed-on to the auth service via POST.</summary>
        /// <remarks>AuthPostData is just one value. Each SetAuthPostData replaces any previous value. It can be either a string, a byte[] or a dictionary.</remarks>
        /// <param name="stringData">String data to be used in the body of the POST request. Null or empty string will set AuthPostData to null.</param>
        public virtual void SetAuthPostData(string stringData)
        {
            this.AuthPostData = (string.IsNullOrEmpty(stringData)) ? null : stringData;
        }

        /// <summary>Sets the data to be passed-on to the auth service via POST.</summary>
        /// <remarks>AuthPostData is just one value. Each SetAuthPostData replaces any previous value. It can be either a string, a byte[] or a dictionary.</remarks>
        /// <param name="byteData">Binary token / auth-data to pass on.</param>
        public virtual void SetAuthPostData(byte[] byteData)
        {
            this.AuthPostData = byteData;
        }

        /// <summary>Sets data to be passed-on to the auth service as Json (Content-Type: "application/json") via Post.</summary>
        /// <remarks>AuthPostData is just one value. Each SetAuthPostData replaces any previous value. It can be either a string, a byte[] or a dictionary.</remarks>
        /// <param name="dictData">A authentication-data dictionary will be converted to Json and passed to the Auth webservice via HTTP Post.</param>
        public virtual void SetAuthPostData(Dictionary<string, object> dictData)
        {
            this.AuthPostData = dictData;
        }

        /// <summary>Adds a key-value pair to the get-parameters used for Custom Auth (AuthGetParameters).</summary>
        /// <remarks>This method does uri-encoding for you.</remarks>
        /// <param name="key">Key for the value to set.</param>
        /// <param name="value">Some value relevant for Custom Authentication.</param>
        public virtual void AddAuthParameter(string key, string value)
        {
            string ampersand = string.IsNullOrEmpty(this.AuthGetParameters) ? "" : "&";
            this.AuthGetParameters = string.Format("{0}{1}{2}={3}", this.AuthGetParameters, ampersand, System.Uri.EscapeDataString(key), System.Uri.EscapeDataString(value));
        }

        /// <summary>Shallow validation if the mandatory data / parameters are set for the given AuthType.</summary>
        /// <returns>True if mandatory values are set.</returns>
        public virtual bool AreValid()
        {
            switch (this.authType)
            {
                case CustomAuthenticationType.Steam:
                    return this.AuthGetParametersContain("ticket");

                case CustomAuthenticationType.NintendoSwitch:
                case CustomAuthenticationType.Epic:
                case CustomAuthenticationType.Facebook:
                case CustomAuthenticationType.FacebookGaming:
                    return this.AuthGetParametersContain("token");

                case CustomAuthenticationType.Oculus:
                    return this.AuthGetParametersContain("userid", "nonce");


                case CustomAuthenticationType.PlayStation4:
                case CustomAuthenticationType.PlayStation5:
                    return this.AuthGetParametersContain("userName", "token", "env");

                case CustomAuthenticationType.Xbox:
                    return this.AuthPostData != null;

                case CustomAuthenticationType.Viveport:
                    return this.AuthGetParametersContain("userToken");
            }

            return true;
        }

        /// <summary>Uses Regex to make sure the url-parameters are in the AuthGetParameters and have some value.</summary>
        /// <param name="keys">Keys which must be present.</param>
        /// <returns>False if any key isn't present with "=" and some value in the AuthGetParameters.</returns>
        public bool AuthGetParametersContain(params string[] keys)
        {
            if (string.IsNullOrEmpty(this.AuthGetParameters))
            {
                return false;
            }

            if (keys == null)
            {
                return true;
            }

            foreach (string key in keys)
            {
                string keyEquals = $".*{key}=\\w+";
                bool ok = Regex.IsMatch(this.AuthGetParameters, keyEquals);

                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Transform this object into string.
        /// </summary>
        /// <returns>String info about this object's values.</returns>
        public override string ToString()
        {
            return string.Format("AuthenticationValues = AuthType: {0} UserId: {1}{2}{3}{4}",
                                 this.AuthType,
                                 this.UserId,
                                 string.IsNullOrEmpty(this.AuthGetParameters) ? " GetParameters: yes" : "",
                                 this.AuthPostData == null ? "" : " PostData: yes",
                                 this.Token == null ? "" : " Token: yes");
        }

        /// <summary>
        /// Make a copy of the current object.
        /// </summary>
        /// <param name="copy">The object to be copied into.</param>
        /// <returns>The copied object.</returns>
        public AuthenticationValues CopyTo(AuthenticationValues copy)
        {
            copy.AuthType = this.AuthType;
            copy.AuthGetParameters = this.AuthGetParameters;
            copy.AuthPostData = this.AuthPostData;
            copy.UserId = this.UserId;
            return copy;
        }
    }
}