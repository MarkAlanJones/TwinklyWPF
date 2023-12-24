using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;

namespace Twinkly_xled
{
    // -------------------------------------------------------------------------
    // A .net C# library to communicate with Twinkly RGB Christmas lights
    // Authentication is handled automatically - but will expire after 4hrs
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    //        Node.js - https://github.com/linkineo/tinky-twinkly
    //        another - https://github.com/timon/twinkly/blob/master/API.md
    // another Node.js- https://github.com/cosmicChild1987/twinkly-api
    // recent node.js - https://github.com/patrickbs96/ioBroker.twinkly
    //       Mongoose - https://github.com/d4rkmen/twinkly 
    //    Angular CLI - https://github.com/chris-guilliams/Christmas-Treevia
    // --------------------------------------------------------------------------
    public class XLedAPI : IDisposable
    {
        private DataAccess data;

        /// <summary>
        /// HttpStatus of last call generally
        /// </summary>
        public int Status { get; private set; }

        public string IPAddress => data == null ? string.Empty : data.IPAddress;

        private TimeSpan uptime = new TimeSpan(0);
        /// <summary>
        /// How long has the Twinkly been powered on - from the Gestalt
        /// </summary>
        public TimeSpan Uptime
        {
            get { return uptime; }
            private set { uptime = value; }
        }

        public bool Authenticated => data == null ? false : data.ExpiresIn.TotalMinutes > 0;
        public DateTime ExpiresAt => data == null ? DateTime.Now : data.ExpiresAt;
        public TimeSpan ExpiresIn => data == null ? new TimeSpan(0) : data.ExpiresIn;
        public int BytesPerLed { get; private set; }
        public int NumLED { get; private set; }

        #region Start - Construct Locate Connect
        /// <summary>
        /// Constructor
        /// </summary>
        public XLedAPI()
        {
            Status = (int)HttpStatusCode.RequestTimeout;
        }

        /// <summary>
        /// Detect all local twinklys, then pick one and pass the Address to ConnectTwinkly
        /// </summary>
        /// <returns></returns>
        public async static Task<IEnumerable<TwinklyInstance>> Detect()
        {
            var result = await TwinklyDetector.LocateAsync().ConfigureAwait(false);
            return result.Distinct(new TwinklyComparer()).OrderBy(tw => tw.Name);
        }

        /// <summary>
        /// To use most of the API, connect to a Twinkly IP address
        /// </summary>
        /// <param name="IP">IPV4 address of Twinkly</param>
        /// <returns>Status connected=200, or timeout=408</returns>
        public int ConnectTwinkly(string IP)
        {
            try
            {
                Logging.WriteDbg($"Connecting Twinkly {IP}");

                data = new DataAccess(IP);
                Status = (int)data.HttpStatus;
            }
            catch (TimeoutException tex)
            {
                Status = (int)HttpStatusCode.RequestTimeout;
                Logging.WriteDbg($"Timeout Connecting to Twinkly {tex.Message}");
            }
            catch (Exception ex)
            {
                Status = (int)HttpStatusCode.RequestTimeout;
                Logging.WriteDbg($"Error Connecting to Twinkly {ex.Message}");
            }
            return Status;
        }

        #endregion

        #region Unauthenticated

        /// <summary>
        /// Info - aka Gestalt - Bytes Per LED and the RT Buffer are set by calling 
        /// </summary>
        /// <returns>GestaltResult</returns>
        public async Task<GestaltResult> Info()
        {
            (string json, bool Error) = await data.Get("gestalt")
                                                  .ConfigureAwait(false);
            if (!Error)
            {
                // Logging.WriteDbg(json);
                Status = (int)data.HttpStatus;
                var Gestalt = JsonSerializer.Deserialize<GestaltResult>(json);

                Uptime = TimeSpan.FromMilliseconds(long.Parse(Gestalt.uptime));

                // Set properties for later
                BytesPerLed = Gestalt.bytes_per_led;
                NumLED = Gestalt.number_of_led;
                RT_Buffer = new byte[NumLED * BytesPerLed];
                data.NumLED = NumLED;

                return Gestalt;
            }
            else
            {
                return new GestaltResult() { code = (int)data.HttpStatus };
            }
        }

        /// <summary>
        /// Expect code=1000 for OK
        /// </summary>
        public async Task<VerifyResult> GetStatus()
        {
            if (data is null)
                throw new ArgumentNullException($"Connect twinkly Via IP first");

            (string json, bool Error) = await data.Get("status")
                                                  .ConfigureAwait(false);
            if (!Error)
            {
                // Logging.WriteDbg(json);
                Status = (int)data.HttpStatus;
                var result = JsonSerializer.Deserialize<VerifyResult>(json);
                return result;
            }
            else
            {
                return new VerifyResult() { code = (int)data.HttpStatus };
            }
        }

        /// <summary>
        /// Firmware Version as string
        /// </summary>
        public async Task<FWResult> Firmware()
        {
            if (data is null)
                throw new ArgumentNullException($"Connect twinkly Via IP first");

            (string json, bool Error) = await data.Get("fw/version")
                                                  .ConfigureAwait(false);
            if (!Error)
            {
                // Logging.WriteDbg(json);
                Status = (int)data.HttpStatus;
                var FW = JsonSerializer.Deserialize<FWResult>(json);
                return FW;
            }
            else
            {
                return new FWResult() { code = (int)data.HttpStatus };
            }
        }
        #endregion

        #region Login

        // uses Challenge/Response authentication
        public async Task<bool> Login()
        {
            if (data is null)
                throw new ArgumentNullException($"Connect twinkly Via IP first");
            using var crypto = System.Security.Cryptography.Aes.Create();
            crypto.GenerateKey();
            var key = Convert.ToBase64String(crypto.Key);
            var content = JsonSerializer.Serialize(new Challenge() { challenge = key });

            var json = await data.Post("login", content);
            if (!data.Error)
            {
                var result = JsonSerializer.Deserialize<LoginResult>(json);

                if (result.code == 1000)
                {
                    data.Authenticate(result.authentication_token, result.authentication_token_expires_in);

                    // verify
                    content = JsonSerializer.Serialize(new Verify() { challenge_response = result.challenge_response });
                    json = await data.Post("verify", content);
                    var result2 = JsonSerializer.Deserialize<VerifyResult>(json);

                    if (result2.code != 1000)
                    {
                        Status = result2.code;
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        // Probably invalidate access token. Doesn’t work.
        public async Task<bool> Logout()
        {
            if (Authenticated)
            {
                var json = await data.Post("logout", "{}");
                var result = JsonSerializer.Deserialize<VerifyResult>(json);

                if (result.code != 1000)
                {
                    Status = result.code;
                    return false;
                }

                // could remove auth header to ensure logout
                return true;
            }
            return true;
        }
        #endregion

        #region Authentication required

        #region DeviceName
        // Property facade to Get/Set the device name - Live
        public string DeviceName
        {
            get
            {
                if (Authenticated)
                {
                    return GetDeviceName().Result.name;
                }
                else
                {
                    return Info().Result.device_name;
                }
            }
            set
            {
                if (value is null) return;

                var result = SetDeviceName(value).Result;
                //result.code should be 1000
            }
        }


        // this is available unauthenticated from Info (gestalt)
        public async Task<DeviceNameResult> GetDeviceName()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("device_name")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var name = JsonSerializer.Deserialize<DeviceNameResult>(json);

                    return name;
                }
                else
                {
                    return new DeviceNameResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new DeviceNameResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        // Max 32 chars - 1103 if too long. - could check length or truncate
        public async Task<DeviceNameResult> SetDeviceName(string newname)
        {
            if (newname is null)
                throw new ArgumentNullException(nameof(newname));

            if (newname.Length > 32)
                throw new ArgumentOutOfRangeException("Twinkly Names are Max 32");

            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new DeviceName() { name = newname.Trim() });
                var json = await data.Post("device_name", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<DeviceNameResult>(json);

                    return result;
                }
                else
                {
                    return new DeviceNameResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new DeviceNameResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }
        #endregion

        #region Timer

        // Gets time when lights should be turned on and time to turn them off.
        // times are second since midnight -1 for not set
        public async Task<Timer> GetTimer()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("timer")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var time = JsonSerializer.Deserialize<Timer>(json);

                    return time;
                }
            }

            return new Timer();
        }

        // on/off -1 for N/A (else Seconds after midnight - 3600 per hour)
        public async Task<VerifyResult> SetTimer(DateTime now, int on, int off)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new Timer()
                {
                    time_now = (int)now.TimeOfDay.TotalSeconds,
                    time_on = on,
                    time_off = off
                });
                Logging.WriteDbg(content);
                var json = await data.Post("timer", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }
        #endregion

        //       led/
        //          mode
        //          effects/
        //            current 
        //          config
        //          movie/
        //            full - upload a movie
        //            config  
        //          out/brightness /saturation
        //          driver_params     POST /xled/v1/led/driver_params - but what is body ?
        //          reset

        #region Operation Mode

        public async Task<ModeResult> GetOperationMode()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/mode")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var mode = JsonSerializer.Deserialize<ModeResult>(json);

                    return mode;
                }
                else
                {
                    return new ModeResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new ModeResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        private LedModes CurrentMode { get; set; }
        /// <summary>
        ///  Use this to Turn on or Turn off the lights "movie" or "off" 
        ///  Also used to set "rt" mode so UDP 7777 will respond - rt stops animation from movie
        ///  Single color mode
        /// </summary>
        public async Task<VerifyResult> SetOperationMode(LedModes mode)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new Mode() { mode = mode.ToString() });
                var json = await data.Post("led/mode", content);

                if (!data.Error)
                {
                    CurrentMode = mode;
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Colour Mode

        public async Task<ColorResult> GetColor()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/color")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var c = JsonSerializer.Deserialize<ColorResult>(json);

                    return c;
                }
                else
                {
                    return new ColorResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new ColorResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// POST a single colour for all lights
        /// </summary>
        /// <param name="c">System.Drawing.Color</param>
        /// <returns>Api Code 1000=success</returns>
        public async Task<VerifyResult> SetHue(Color c)
        {
            if (Authenticated)
            {
                var changemode = await SetOperationMode(LedModes.color);
                if (changemode.code == 1000)
                {
                    ColorRGB rgb = new ColorRGB() { red = c.R, blue = c.B, green = c.G };
                    var json = await data.Post("led/color", JsonSerializer.Serialize(rgb));

                    if (!data.Error)
                    {
                        Status = (int)data.HttpStatus;
                        var result = JsonSerializer.Deserialize<VerifyResult>(json);

                        return result;
                    }
                    else
                    {
                        return new VerifyResult() { code = (int)data.HttpStatus };
                    }
                }
                else
                {
                    // could not switch to color mode
                    return new VerifyResult() { code = changemode.code };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// HSV color
        /// </summary>
        public async Task<VerifyResult> SetColorHSV(int h, int s, int v)
        {
            if (Authenticated)
            {
                var changemode = await SetOperationMode(LedModes.color);
                if (changemode.code == 1000)
                {
                    ColorHSV hsv = new ColorHSV() { hue = h, saturation = s, value = v };
                    var json = await data.Post("led/color", JsonSerializer.Serialize(hsv));

                    if (!data.Error)
                    {
                        Status = (int)data.HttpStatus;
                        var result = JsonSerializer.Deserialize<VerifyResult>(json);

                        return result;
                    }
                    else
                    {
                        return new VerifyResult() { code = (int)data.HttpStatus };
                    }
                }
                else
                {
                    // could not switch to color mode
                    return new VerifyResult() { code = changemode.code };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Effects

        // How many effects ? - what can we do with an effect ?
        // Effects are built in movies - demo mode plays through 5 effects
        public async Task<EffectsResult> Effects()
        {
            if (Authenticated)
            {
                // this is coming back with the length on the front and truncates the end of the json
                (string json, bool Error) = await data.Get("led/effects")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    if (!json.StartsWith("{"))
                    {
                        // hack for malformed resonse :(
                        json = json.Substring(6) + "00F\"]}";
                    }

                    if (json.StartsWith("{"))
                    {
                        // Logging.WriteDbg(json);
                        Status = (int)data.HttpStatus;
                        var eff = JsonSerializer.Deserialize<EffectsResult>(json);

                        return eff;
                    }
                    else
                    {
                        Logging.WriteDbg($"Truncated JSON from led/effects {json}");
                        return new EffectsResult() { code = (int)HttpStatusCode.BadRequest };
                    }
                }
                else
                {
                    return new EffectsResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new EffectsResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        public async Task<EffectsCurrentResult> CurrentEffects()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/effects/current")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var eff = JsonSerializer.Deserialize<EffectsCurrentResult>(json);

                    return eff;
                }
                else
                {
                    return new EffectsCurrentResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new EffectsCurrentResult() { code = (int)HttpStatusCode.Unauthorized };
            }

        }

        // if you are interested in effects call both at the same time 
        public async Task<MergedEffectsResult> EffectsAllinOne()
        {
            var result1 = await CurrentEffects();
            var result2 = await Effects();

            return new MergedEffectsResult()
            {
                effects_number = result2.effects_number,
                effect_id = result1.effect_id,
                unique_id = result1.unique_id,
                unique_ids = result2.unique_ids,
                code = Math.Max(result1.code, result2.code)
            };
        }

        /// <summary>
        /// Sets Effect mode and then an effect
        /// </summary>
        /// <param name="effect">0 based don't pass more than 15</param>
        /// <returns></returns>
        public async Task<VerifyResult> SetCurrentEffects(int effect)
        {
            if (Authenticated)
            {
                var changemode = await SetOperationMode(LedModes.effect);
                if (changemode.code == 1000)
                {
                    var ef = new EffectsEffect() { effect_id = effect };
                    var json = await data.Post("led/effects/current", JsonSerializer.Serialize(ef));

                    if (!data.Error)
                    {
                        Status = (int)data.HttpStatus;
                        var result = JsonSerializer.Deserialize<VerifyResult>(json);

                        return result;
                    }
                    else
                    {
                        return new VerifyResult() { code = (int)data.HttpStatus };
                    }
                }
                else
                {
                    // could not switch to effects mode
                    return new VerifyResult() { code = changemode.code };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }
        #endregion

        #region LED Config

        // For 400 LEDS - it reports 2 sets of 200 - how to use ?
        public async Task<LedConfigResult> GetLEDConfig()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/config")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var config = JsonSerializer.Deserialize<LedConfigResult>(json);

                    return config;
                }
                else
                {
                    return new LedConfigResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new LedConfigResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Why Reconfigure ? what does it do ? -
        public async Task<VerifyResult> SetLEDConfig(ConfigStrings[] strings)
        {
            if (Authenticated)
            {
                LedConfigResult config = new LedConfigResult() { strings = strings };
                var json = await data.Post("led/config", JsonSerializer.Serialize(config));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Movie
        // The frame_delay value is in msec

        public async Task<FullMovieResult> UploadMovie(byte[] movie)
        {
            if (Authenticated)
            {
                throw new NotImplementedException();

                // Frames ? Leds ?
                // Content-Type application/octet-stream
                ////var json = await data.Post("led/movie/full", "byte array goes here ?");

                ////if (!data.Error)
                ////{
                ////    Status = (int)data.HttpStatus;
                ////    var result = JsonSerializer.Deserialize<FullMovieResult>(json);

                ////    return result;
                ////}
                ////else
                ////{
                ////    return new FullMovieResult() { code = (int)data.HttpStatus };
                ////}
            }
            else
            {
                return new FullMovieResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public async Task<CurrentMovieConfig> GetMovieConfig()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/movie/config")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var config = JsonSerializer.Deserialize<CurrentMovieConfig>(json);

                    return config;
                }
                else
                {
                    return new CurrentMovieConfig() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new CurrentMovieConfig() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // After you upload the movie 
        public async Task<VerifyResult> SetMovieConfig(int frames_number, int frame_delay, int leds_number)
        {
            if (Authenticated)
            {
                MovieConfig config = new MovieConfig()
                {
                    frames_number = frames_number,
                    frame_delay = frame_delay,
                    leds_number = leds_number
                };
                var json = await data.Post("led/movie/config", JsonSerializer.Serialize(config));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// Get list of movies - Retrieve the identities and parameters of all uploaded movies.
        /// </summary>
        public async Task<MoviesResult> GetMovies()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("movies")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var movies = JsonSerializer.Deserialize<MoviesResult>(json);

                    return movies;
                }
                else
                {
                    return new MoviesResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new MoviesResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        //
        //  /led/movies/current  - set which movie to play - similar to effect
        //
        public async Task<CurrentMovieInfo> GetCurrentMovie()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/movies/current")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var config = JsonSerializer.Deserialize<CurrentMovieInfo>(json);

                    return config;
                }
                else
                {
                    return new CurrentMovieInfo() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new CurrentMovieInfo() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public async Task<VerifyResult> SetCurrentMovie(int id)
        {
            if (Authenticated)
            {
                var movieid = new CurrentMovieId() { id = id };
                var json = await data.Post("led/movie/config", JsonSerializer.Serialize(movieid));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Delete Movies
        // Any existing playlist will be removed as well. This call only works if the device is not in movie or playlist mode.

        #endregion

        #region LED Brightness

        // Disabled Brightness is 100
        public async Task<BrightnessResult> GetBrightness()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/out/brightness")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var bright = JsonSerializer.Deserialize<BrightnessResult>(json);

                    return bright;
                }
                else
                {
                    return new BrightnessResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new BrightnessResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // 0-100 setting to 100 or higher will disable dimming
        public async Task<VerifyResult> SetBrightness(byte brightness)
        {
            if (Authenticated)
            {
                Brightness bright;
                if (brightness >= 100)
                    bright = new Brightness() { mode = "disabled", value = 100 };
                else
                    bright = new Brightness() { mode = "enabled", value = brightness };

                var json = await data.Post("led/out/brightness", JsonSerializer.Serialize(bright));

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Saturation

        // Disabled Saturation is 0
        public async Task<SaturationResult> GetSaturation()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/out/saturation")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var sat = JsonSerializer.Deserialize<SaturationResult>(json);

                    return sat;
                }
                else
                {
                    return new SaturationResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new SaturationResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // 0-100 setting to 100 or higher will disable desaturation
        // desaturate to make monochrome 
        public async Task<VerifyResult> SetSaturationAbs(byte saturation)
        {
            if (Authenticated)
            {
                string content;
                if (saturation >= 100)
                {
                    var sat = new Saturation() { mode = "disabled", value = 100 };
                    content = JsonSerializer.Serialize(sat);
                }
                else
                {
                    var sat = new SaturationType() { mode = "enabled", type = "A", value = saturation };
                    content = JsonSerializer.Serialize(sat);
                }
                var json = await data.Post("led/out/saturation", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// -100 to 100 relative saturaton
        /// </summary>
        /// <param name="saturation">Relative Saturation</param>
        /// <returns>code</returns>
        public async Task<VerifyResult> SetSaturationRel(int saturation)
        {
            if (Authenticated)
            {
                string content;
                if (Math.Abs(saturation) >= 100)
                {
                    var sat = new Saturation() { mode = "disabled", value = 100 };
                    content = JsonSerializer.Serialize(sat);
                }
                else
                {
                    var sat = new SaturationType() { mode = "enabled", type = "R", value = saturation };
                    content = JsonSerializer.Serialize(sat);
                }
                var json = await data.Post("led/out/saturation", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }
        #endregion

        #region LED reset

        // Reset does what ?
        // (patrickbs96) Temporary removing Reset as API path not exists
        // (cosmicChild1987) - posts {} to this 
        public async Task<VerifyResult> Reset()
        {
            if (Authenticated)
            {
                //var json = await data.Get("led/reset");
                //var json = await data.Get("led/reset2");
                var json = await data.Post("led/reset", "{}")
                                     .ConfigureAwait(false);
                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region LED Layout
        //
        //  /layout/full  - GET or POST
        //  Could this be used to draw a rendition of the string of lights ? 
        //  Results can be truncated and sent into next request - paging ? 
        //
        public async Task<LedLayoutResult> GetLEDLayout()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("led/layout/full")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    // This is a long message and tends to be where there are issues with overloading the twinkly
                    try
                    {
                        var layout = JsonSerializer.Deserialize<LedLayoutResult>(json);
                        return layout;
                    }
                    catch (JsonException jex)
                    {
                        Logging.WriteDbg($"JSON Exception {jex.Message}");
                        return new LedLayoutResult()
                        {
                            code = (int)HttpStatusCode.PartialContent,
                            source = jex.Message
                        };
                    }
                }
                else
                {
                    return new LedLayoutResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new LedLayoutResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Upload Layout - Maybe if you remapped the lights ?
        public async Task<LedLayoutUploadResult> LedLayoutUpload(LedLayout layout)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(layout);
                Logging.WriteDbg(content);
                var json = await data.Post("led/layout/full", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<LedLayoutUploadResult>(json);

                    return result;
                }
                else
                {
                    return new LedLayoutUploadResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new LedLayoutUploadResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Update Firmware

        // can you really write better fw without bricking it ?
        // Not implemented: Update firmware - POST /xled/v1/fw/update
        // Not implemented: Upload first stage of firmware - POST /xled/v1/fw/0/update
        // Not implemented: Upload second stage of firmware - POST /xled/v1/fw/1/update

        #endregion

        #region Network

        //  Initiate WiFi network scan - GET /xled/v1/network/scan
        public async Task<VerifyResult> StartNetworkScan()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("network/scan")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    //Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Get results of WiFi network scan - GET /xled/v1/network/scan_results
        // Note this starts the Networkscan as well
        public async Task<NetworkScanResult> NetworkScan()
        {
            if (Authenticated)
            {
                var nws = await StartNetworkScan();
                if (nws.code == 1000)
                {
                    (string json, bool Error) = await data.Get("network/scan_results")
                                                          .ConfigureAwait(false);
                    if (!Error)
                    {
                        //Logging.WriteDbg(json);
                        Status = (int)data.HttpStatus;
                        var result = JsonSerializer.Deserialize<NetworkScanResult>(json);
                        return result;
                    }
                    else
                    {
                        return new NetworkScanResult() { code = (int)data.HttpStatus };
                    }
                }
                else
                {
                    return new NetworkScanResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new NetworkScanResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Get network status - GET /xled/v1/network/status
        public async Task<NetworkStatus> GetNetworkStatus()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("network/status")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    //Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<NetworkStatus>(json);

                    return result;
                }
                else
                {
                    return new NetworkStatus() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new NetworkStatus() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        // Not implemented: Set network status - POST /xled/v1/network/status
        // This would allow you to switch to AP mode 2 instead of station... but then how to you set it back ?

        #endregion

        #region MQTT

        // d4rkmen says : Unfortunatley, the newest devices (Gen2) use SSL connection to MQTT broker port 8883, which makes it impossible to use custom broker 
        // because of hardcoded CA inside the firmware.
        public async Task<MQTTConfigResult> GetMQTTConfig()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("mqtt/config")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var mqtt = JsonSerializer.Deserialize<MQTTConfigResult>(json);

                    return mqtt;
                }
                else
                {
                    return new MQTTConfigResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new MQTTConfigResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// SetMQTTConfig - You can set the host and user but can it actually connect to an MQTT broker?  
        /// </summary>
        /// <param name="settings">Host ClientId KeepAlive User</param>
        /// <returns>VerifyResult always 1000?</returns>
        public async Task<VerifyResult> SetMQTTConfig(MQTTConfig settings)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(settings);
                Logging.WriteDbg(content);
                var json = await data.Post("mqtt/config", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<VerifyResult>(json);

                    return result;
                }
                else
                {
                    return new VerifyResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new VerifyResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Playlist
        // Playlist is a collection of Movies to be played
        // 
        //  /playlist duration and uuid 
        //     GET POST DELETE
        //  /playlist/current
        //     GET POST 
        //

        public async Task<PlaylistResult> GetPlaylist()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("playlist")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    //Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<PlaylistResult>(json);

                    return result;
                }
                else
                {
                    return new PlaylistResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new PlaylistResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        /// <summary>
        /// Gets which movie is currently played in playlist mode.
        /// </summary>
        /// <returns></returns>
        public async Task<CurrentPlaylistEntry> GetPlaylistCurrent()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("playlist/current")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<CurrentPlaylistEntry>(json);

                    return result;
                }
                else
                {
                    return new CurrentPlaylistEntry() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new CurrentPlaylistEntry() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        #endregion

        #region Paint RT

        // the realtime buffer has 1 FRAME  3 or 4 bytes for every light
        // header bytes are handled in DataAccess
        private byte[] RT_Buffer;

        // Use RT 7777 UDP to set all lights to the same color 
        // pass color as byte array RGB, or RGBW

        /// <summary>
        /// Use the RT Buffer to send one RealTime Frame of a single color
        /// </summary>
        /// <param name="c">array of 3 or 4 bytes to math BytesPerLed</param>
        /// <returns></returns>
        public async Task SingleColorRT(byte[] c)
        {
            if (Authenticated && c.Length == BytesPerLed)
            {
                // Color Data
                for (int i = 0; i < RT_Buffer.Length; i += c.Length)
                {
                    Buffer.BlockCopy(c, 0, RT_Buffer, i, c.Length);
                }

                await SendRtFrame(RT_Buffer);
            }
        }

        /// <summary>
        /// RT Mode - using V3 Frames to support more than 256 lights
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public async Task SendRtFrame(byte[] frame)
        {
            if (Authenticated)
            {
                var changemode = new VerifyResult() { code = 1000 };
                if (CurrentMode != LedModes.rt)
                    changemode = await SetOperationMode(LedModes.rt);

                if (changemode.code == 1000)
                {
                    await data.RTFX(frame).ConfigureAwait(false);
                    //data.RTFX_Classic(RT_Buffer);
                }
            }
        }

        /// <summary>
        /// Pass Color, but not the WPF one 
        /// </summary>
        /// <param name="c">System Drawing Color</param>
        public async Task SingleColorRT(Color c)
        {
            switch (BytesPerLed)
            {
                case 3:
                    await SingleColorRT(new byte[3] { c.R, c.G, c.B });
                    break;

                case 4:
                    await SingleColorRT(new byte[4] { 0x00, c.R, c.G, c.B });
                    break;
            }
        }
        #endregion

        #region Summary
        public async Task<SummaryResult> GetSummary()
        {
            if (Authenticated)
            {
                (string json, bool Error) = await data.Get("summary")
                                                      .ConfigureAwait(false);
                if (!Error)
                {
                    // Logging.WriteDbg(json);
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<SummaryResult>(json);

                    return result;
                }
                else
                {
                    return new SummaryResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new SummaryResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }
        #endregion

        #region Echo
        //
        // /echo - post "message" - response with message with status 1000
        //
        public async Task<MessageResult> Echo(string hello)
        {
            if (Authenticated)
            {
                var content = JsonSerializer.Serialize(new Message() { message = hello });
                Logging.WriteDbg(content);
                var json = await data.Post("echo", content);

                if (!data.Error)
                {
                    Status = (int)data.HttpStatus;
                    var result = JsonSerializer.Deserialize<MessageResult>(json);

                    return result;
                }
                else
                {
                    return new MessageResult() { code = (int)data.HttpStatus };
                }
            }
            else
            {
                return new MessageResult() { code = (int)HttpStatusCode.Unauthorized };
            }
        }

        public void Dispose()
        {
            data.Dispose();
        }
        #endregion

        //
        //  /music/drivers  /music/drivers/sets  /music/drivers/sets/current
        //

        #endregion

    }
}

