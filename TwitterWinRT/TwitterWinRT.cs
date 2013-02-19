using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Security.Authentication.Web;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;

namespace TwitterWinRT
{
    public class TwitterWinRT
    {
        private const string requestTokenUrl = "https://api.twitter.com/oauth/request_token";
        private const string authorizeUrl = "https://api.twitter.com/oauth/authorize";
        private const string accessTokenUrl = "https://api.twitter.com/oauth/access_token";
        private const string updateStatusUrl = "https://api.twitter.com/1/statuses/update.json";
        private const string timelineUrl = "https://api.twitter.com/1/statuses/user_timeline.json";
        private const string homelineUrl = "https://api.twitter.com/1/statuses/home_timeline.json";
        private const string userUrl = "https://api.twitter.com/1/users/show.json";

        private const string signatureMethod = "HMAC-SHA1";
        private const string oauthVersion = "1.0";

        private readonly string consumerKey;
        private readonly string consumerSecret;
        private readonly string callbackUrl;

        private Random rand = new Random();

        public Boolean AccessGranted { get; private set; }
        public Boolean IsTweeting { get; private set; }
        public string OauthToken { get; private set; }
        public string OauthTokenSecret { get; private set; }
        public string UserID { get; private set; }
        public string ScreenName { get; private set; }
        public string Status { get; private set; }

        public TwitterWinRT(string consumerKey, string consumerSecret, string callbackUrl)
        {
            this.consumerKey = consumerKey;
            this.consumerSecret = consumerSecret;
            this.callbackUrl = callbackUrl;

            if (!Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                LoadSettings();
            }
        }

        async Task<TwitterRtPostResults> Step2(String oauthToken)
        {
            try
            {
                var url = authorizeUrl + "?oauth_token=" + oauthToken;
                var war = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, new Uri(url), new Uri(callbackUrl));
                switch (war.ResponseStatus)
                {
                    case WebAuthenticationStatus.Success:
                        return new TwitterRtPostResults
                        {
                            Status = TwitterRtPostResults.EStatus.Success,
                            Dictionary = new TwitterRtDictionary(war.ResponseData) // should contain oauth_token and oauth_verifier
                        };

                    case WebAuthenticationStatus.UserCancel:
                        return new TwitterRtPostResults
                        {
                            Status = TwitterRtPostResults.EStatus.Canceled,
                        };

                    default:
                    case WebAuthenticationStatus.ErrorHttp:
                        return new TwitterRtPostResults
                        {
                            Status = TwitterRtPostResults.EStatus.Error,
                            Description = war.ResponseErrorDetail.ToString()
                        };
                }
            }
            catch (Exception e)
            {
                return new TwitterRtPostResults
                {
                    Status = TwitterRtPostResults.EStatus.Error,
                    Description = e.Message
                };
            }
        }

        /// <summary>
        /// Obtain the oAuth token for the user
        /// </summary>
        /// <returns>return true is the login was succesful </returns>
        public async Task<Boolean> GainAccessToTwitter()
        {
            ResetSettings();
            Status = "Authorizing";

            var step1 = await Step1();
            if (step1.Status != TwitterRtPostResults.EStatus.Success || step1.Dictionary.ContainsKey("oauth_token") == false)
            {
                Status = String.IsNullOrEmpty(step1.Description) ? "Step 1 failed" : step1.Description;
                SaveSettings();
                return false;
            }

            var step2 = await Step2(step1.Dictionary["oauth_token"]);
            if (step2.Status != TwitterRtPostResults.EStatus.Success || step2.Dictionary.ContainsKey("oauth_token") == false || step2.Dictionary.ContainsKey("oauth_verifier") == false)
            {
                if (step2.Status == TwitterRtPostResults.EStatus.Canceled)
                {
                    LoadSettings();
                }
                else if (step2.Dictionary != null && step2.Dictionary.ContainsKey("denied"))
                {
                    Status = "Denied by user";
                    SaveSettings();
                }
                else
                {
                    Status = String.IsNullOrEmpty(step2.Description) ? "Step 2 failed" : step2.Description;
                    SaveSettings();
                }
                return false;
            }

            var step3 = await Step3(step2.Dictionary["oauth_token"], step2.Dictionary["oauth_verifier"]);
            if (step3.Status != TwitterRtPostResults.EStatus.Success || step3.Dictionary.ContainsKey("oauth_token") == false || step3.Dictionary.ContainsKey("oauth_token_secret") == false || step3.Dictionary.ContainsKey("user_id") == false || step3.Dictionary.ContainsKey("screen_name") == false)
            {
                Status = String.IsNullOrEmpty(step3.Description) ? "Step 3 failed" : step3.Description;
                SaveSettings();
                return false;
            }

            OauthToken = step3.Dictionary["oauth_token"];
            OauthTokenSecret = step3.Dictionary["oauth_token_secret"];
            UserID = step3.Dictionary["user_id"];
            ScreenName = step3.Dictionary["screen_name"];
            AccessGranted = true;
            Status = "Access granted";

            SaveSettings();
            return true;
        }

        /// <summary>
        /// Get the user timeline for the user
        /// </summary>
        /// <returns>List of tweet</returns>
        public async Task<List<Status>> GetUserTimeline()
        {
            var header = PrepareAuth();

            var response = await GetData<List<Status>>(timelineUrl, header);

            return response;
        }

        /// <summary>
        /// Get the home timeline for the user
        /// </summary>
        /// <returns>List of tweet</returns>
        public async Task<List<Status>> GetTimeline()
        {
            var header = PrepareAuth();

            var response = await GetData<List<Status>>(homelineUrl, header);

            return response;
        }

        /// <summary>
        /// Get the profil of the current user
        /// </summary>
        /// <returns>return the user informations</returns>
        public async Task<Profil> GetProfil()
        {
            var header = PrepareAuth();

            var url = userUrl + "?user_id=" + this.UserID;
            var response = await GetData<Profil>(url, header);

            return response;
        }

        private TwitterRtDictionary PrepareAuth()
        {
            var header = new TwitterRtDictionary();
            header.Add("oauth_consumer_key", consumerKey);
            header.Add("oauth_nonce", GenerateNonce());
            header.Add("oauth_signature_method", signatureMethod);
            header.Add("oauth_timestamp", GenerateSinceEpoch());
            header.Add("oauth_token", OauthToken);
            header.Add("oauth_version", oauthVersion);
            return header;
        }

        /// <summary>
        /// Get the profil of the user
        /// </summary>
        /// <param name="userId">user id</param>
        /// <returns>return the user informations</returns>
        public async Task<Profil> GetProfil(int userId)
        {
            var header = PrepareAuth();

            var url = userUrl + "?user_id=" + userId;
            var response = await GetData<Profil>(url, header);

            return response;
        }

        /// <summary>
        /// Get the profil of the user
        /// </summary>
        /// <param name="userId">user name</param>
        /// <returns>return the user informations</returns>
        public async Task<Profil> GetProfil(string username)
        {
            var header = PrepareAuth();

            var url = userUrl + "?screen_name=" + username;
            var response = await GetData<Profil>(url, header);

            return response;
        }

        /// <summary>
        /// Sends a status for the user
        /// </summary>
        /// <param name="status">text of the status</param>
        /// <returns>return true if the operation was a success</returns>
        public async Task<Boolean> UpdateStatus(String status)
        {
            IsTweeting = true;
            Status = "Tweeting";
            var header = PrepareAuth();
            var request = new TwitterRtDictionary();
            request.Add("status", Uri.EscapeDataString(status));
            var response = await PostData(updateStatusUrl, header, request);
            IsTweeting = false;

            if (response.Status == TwitterRtPostResults.EStatus.Success)
            {
                Status = status;
                return true;
            }
            else
            {
                Status = response.Description;
                return false;
            }
        }

        private async Task<TwitterRtPostResults> Step1()
        {
            var header = new TwitterRtDictionary();
            header.Add("oauth_callback", Uri.EscapeDataString(callbackUrl));
            header.Add("oauth_consumer_key", consumerKey);
            header.Add("oauth_nonce", GenerateNonce());
            header.Add("oauth_signature_method", signatureMethod);
            header.Add("oauth_timestamp", GenerateSinceEpoch());
            header.Add("oauth_version", oauthVersion);
            return await PostData(requestTokenUrl, header); // should contain oauth_token, oauth_token_secret, and oauth_callback_confirmed
        }

        private async Task<TwitterRtPostResults> Step3(String oauthToken, String oauthVerifier)
        {
            var header = new TwitterRtDictionary();
            header.Add("oauth_consumer_key", consumerKey);
            header.Add("oauth_nonce", GenerateNonce());
            header.Add("oauth_signature_method", signatureMethod);
            header.Add("oauth_timestamp", GenerateSinceEpoch());
            header.Add("oauth_token", oauthToken);
            header.Add("oauth_version", oauthVersion);
            var request = new TwitterRtDictionary();
            request.Add("oauth_verifier", Uri.EscapeDataString(oauthVerifier));
            return await PostData(accessTokenUrl, header, request);  // should contain oauth_token, oauth_token_secret, user_id, and screen_name
        }

        private async Task<TwitterRtPostResults> PostData(String url, TwitterRtDictionary headerDictionary, TwitterRtDictionary requestDictionary = null)
        {
            // See https://dev.twitter.com/docs/auth/creating-signature
            var combinedDictionaries = new TwitterRtDictionary(headerDictionary);
            combinedDictionaries.Add(requestDictionary);
            var signatureBase = "POST&" + Uri.EscapeDataString(url) + "&" + Uri.EscapeDataString(combinedDictionaries.ToStringA());
            var keyMaterial = CryptographicBuffer.ConvertStringToBinary(consumerSecret + "&" + OauthTokenSecret, BinaryStringEncoding.Utf8);
            var algorithm = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
            var key = algorithm.CreateKey(keyMaterial);
            var dataToBeSigned = CryptographicBuffer.ConvertStringToBinary(signatureBase, BinaryStringEncoding.Utf8);
            var signatureBuffer = CryptographicEngine.Sign(key, dataToBeSigned);
            var signature = CryptographicBuffer.EncodeToBase64String(signatureBuffer);
            var headers = "OAuth " + headerDictionary.ToStringQ() + ", oauth_signature=\"" + Uri.EscapeDataString(signature) + "\"";
            return await PostData(url, headers, (requestDictionary == null) ? String.Empty : requestDictionary.ToString());
        }

        private async Task<TwitterRtPostResults> PostData(String url, String headers, String requestData = null)
        {
            try
            {
                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(url);
                Request.Method = "POST";
                Request.Headers["Authorization"] = headers;

                if (!String.IsNullOrEmpty(requestData))
                {
                    using (StreamWriter RequestDataStream = new StreamWriter(await Request.GetRequestStreamAsync()))
                    {
                        await RequestDataStream.WriteAsync(requestData);
                    }
                }

                HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync();

                if (Response.StatusCode != HttpStatusCode.OK)
                {
                    return new TwitterRtPostResults
                    {
                        Status = TwitterRtPostResults.EStatus.Error,
                        Description = Response.StatusDescription
                    };
                }

                using (StreamReader ResponseDataStream = new StreamReader(Response.GetResponseStream()))
                {
                    var response = await ResponseDataStream.ReadToEndAsync();
                    return new TwitterRtPostResults
                    {
                        Status = TwitterRtPostResults.EStatus.Success,
                        Dictionary = new TwitterRtDictionary(response)
                    };
                }
            }
            catch (Exception e)
            {
                return new TwitterRtPostResults
                {
                    Status = TwitterRtPostResults.EStatus.Error,
                    Description = e.Message,
                };
            }
        }

        private async Task<T> GetData<T>(String url, TwitterRtDictionary headerDictionary, TwitterRtDictionary requestDictionary = null)
            where T : new()
        {
            // See https://dev.twitter.com/docs/auth/creating-signature
            var combinedDictionaries = new TwitterRtDictionary(headerDictionary);
            combinedDictionaries.Add(requestDictionary);
            var signatureBase = "GET&" + Uri.EscapeDataString(url) + "&" + Uri.EscapeDataString(combinedDictionaries.ToStringA());
            var keyMaterial = CryptographicBuffer.ConvertStringToBinary(consumerSecret + "&" + OauthTokenSecret, BinaryStringEncoding.Utf8);
            var algorithm = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
            var key = algorithm.CreateKey(keyMaterial);
            var dataToBeSigned = CryptographicBuffer.ConvertStringToBinary(signatureBase, BinaryStringEncoding.Utf8);
            var signatureBuffer = CryptographicEngine.Sign(key, dataToBeSigned);
            var signature = CryptographicBuffer.EncodeToBase64String(signatureBuffer);
            var headers = "OAuth " + headerDictionary.ToStringQ() + ", oauth_signature=\"" + Uri.EscapeDataString(signature) + "\"";
            return await GetData<T>(url, headers, (requestDictionary == null) ? String.Empty : requestDictionary.ToString());
        }

        private async Task<T> GetData<T>(String url, String headers, String requestData = null)
            where T : new()
        {
            try
            {
                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(url);
                Request.Method = "GET";
                Request.Headers["Authorization"] = headers;

                if (!String.IsNullOrEmpty(requestData))
                {
                    using (StreamWriter RequestDataStream = new StreamWriter(await Request.GetRequestStreamAsync()))
                    {
                        await RequestDataStream.WriteAsync(requestData);
                    }
                }

                HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync();

                if (Response.StatusCode != HttpStatusCode.OK)
                {
                    return new T();
                }

                using (StreamReader ResponseDataStream = new StreamReader(Response.GetResponseStream()))
                {
                    var response = await ResponseDataStream.ReadToEndAsync();

                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
                    using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(response)))
                    {
                        var tmp = ser.ReadObject(stream);
                        return (T)tmp;
                    }
                }
            }
            catch (Exception e)
            {
                return new T();
            }
        }

        #region Settings
        void ResetSettings()
        {
            AccessGranted = false;
            OauthToken = String.Empty;
            OauthTokenSecret = String.Empty;
            UserID = String.Empty;
            ScreenName = String.Empty;
            IsTweeting = false;
            Status = "Disconnected";
        }

        void LoadSettings()
        {
            AccessGranted = LoadSetting("AccessGranted", false);
            OauthToken = LoadSetting("OauthToken", String.Empty);
            OauthTokenSecret = LoadSetting("OauthTokenSecret", String.Empty);
            UserID = LoadSetting("UserID", String.Empty);
            ScreenName = LoadSetting("ScreenName", String.Empty);

            if (AccessGranted)
            {
                Status = "Access granted";
            }
            else
            {
                Status = "Unauthorized";
            }
        }

        Boolean LoadSetting(String name, Boolean defaultValue)
        {
            var settings = ApplicationData.Current.RoamingSettings;
            if (settings.Values.ContainsKey("Twitter" + name))
            {
                Boolean value;
                if (Boolean.TryParse(settings.Values["Twitter" + name].ToString(), out value))
                {
                    return value;
                }
            }
            return defaultValue;
        }

        String LoadSetting(String name, String defaultValue)
        {
            var settings = ApplicationData.Current.RoamingSettings;
            if (settings.Values.ContainsKey("Twitter" + name))
            {
                return settings.Values["Twitter" + name].ToString();
            }
            return defaultValue;
        }

        void SaveSettings()
        {
            SaveSetting("AccessGranted", AccessGranted.ToString());
            SaveSetting("OauthToken", OauthToken);
            SaveSetting("OauthTokenSecret", OauthTokenSecret);
            SaveSetting("UserID", UserID);
            SaveSetting("ScreenName", ScreenName);
        }

        void SaveSetting(String key, String value)
        {
            var settings = ApplicationData.Current.RoamingSettings;
            settings.Values["Twitter" + key] = value;
        }
        #endregion

        #region Tools
        String GenerateSinceEpoch()
        {
            return Math.Round((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds).ToString();
        }

        String GenerateNonce()
        {
            return rand.Next(Int32.MaxValue).ToString();
        }
        internal class TwitterRtDictionary : SortedDictionary<String, String>
        {
            public TwitterRtDictionary()
            {
            }

            public TwitterRtDictionary(string response)
            {
                var qSplit = response.Split('?');
                foreach (var kvp in qSplit[qSplit.Length - 1].Split('&'))
                {
                    var kvpSplit = kvp.Split('=');
                    if (kvpSplit.Length == 2)
                    {
                        Add(kvpSplit[0], kvpSplit[1]);
                    }
                }
            }

            public TwitterRtDictionary(TwitterRtDictionary src)
            {
                Add(src);
            }

            public void Add(TwitterRtDictionary src)
            {
                if (src != null)
                {
                    foreach (var kvp in src)
                    {
                        Add(kvp.Key, kvp.Value);
                    }
                }
            }

            public String ToStringA()
            {
                String retVal = String.Empty;
                foreach (var kvp in this)
                {
                    retVal += ((retVal.Length > 0) ? "&" : "") + kvp.Key + "=" + kvp.Value;
                }
                return retVal;
            }

            public String ToStringQ()
            {
                String retVal = String.Empty;
                foreach (var kvp in this)
                {
                    retVal += ((retVal.Length > 0) ? ", " : "") + kvp.Key + "=" + "\"" + kvp.Value + "\"";
                }
                return retVal;
            }

            public override String ToString()
            {
                String retVal = String.Empty;
                foreach (var kvp in this)
                {
                    retVal += ((retVal.Length > 0) ? ", " : "") + kvp.Key + "=" + kvp.Value;
                }
                return retVal;
            }
        }
        internal class TwitterRtPostResults
        {
            public enum EStatus
            {
                Success = 0,
                Canceled = 1,
                Error = 2,
            }

            public EStatus Status { get; set; }
            public String Description { get; set; }
            public TwitterRtDictionary Dictionary { get; set; }
        }
        #endregion
    }

    public class Status
    {
        public string created_at { get; set; }
        public string id { get; set; }
        public string text { get; set; }

        public User user { get; set; }
    }
    public class User
    {
        public string id { get; set; }
        public string name { get; set; }
        public string screen_name { get; set; }
        public string profile_image_url { get; set; }
        public string url { get; set; }
        public string description { get; set; }
    }

    public class Profil
    {
        public string profile_sidebar_fill_color { get; set; }
        public string name { get; set; }
        public string profile_sidebar_border_color { get; set; }
        public string profile_background_tile { get; set; }
        public string created_at { get; set; }
        public string profile_image_url { get; set; }
        public string location { get; set; }
        public string follow_request_sent { get; set; }
        public string id_str { get; set; }
        public string profile_link_color { get; set; }
        public string is_translator { get; set; }
        public string contributors_enabled { get; set; }
        public string url { get; set; }
        public string favourites_count { get; set; }
        public string utc_offset { get; set; }
        public string id { get; set; }
        public string profile_use_background_image { get; set; }
        public string listed_count { get; set; }
        public string profile_text_color { get; set; }
        public string Protected { get; set; }
        public string followers_count { get; set; }
        public string lang { get; set; }
        public string notifications { get; set; }
        public string geo_enabled { get; set; }
        public string profile_background_color { get; set; }
        public string verified { get; set; }
        public string description { get; set; }
        public string time_zone { get; set; }
        public string profile_background_image_url { get; set; }
        public string friends_count { get; set; }
        public string statuses_count { get; set; }
        public string following { get; set; }
        public string screen_name { get; set; }
        public string show_all_inline_media { get; set; }
    }
}
