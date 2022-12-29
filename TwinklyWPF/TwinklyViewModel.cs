using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Twinkly.Fox;
using Twinkly_xled;
using Twinkly_xled.JSONModels;
using TwinklyWPF.Util;
using HSBColor = TwinklyWPF.Util.HSBColor;
using Timer = Twinkly_xled.JSONModels.Timer;

namespace TwinklyWPF
{
    // Holds 1 Twinkly reference
    public class TwinklyViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private XLedAPI twinklyapi;

        public RelayCommand<string> ModeCommand { get; private set; }
        public RelayCommand UpdateTimerCommand { get; private set; }
        public RelayCommand IncrementEffectCommand { get; private set; }
        public RelayCommand FoxCommand { get; private set; }

        public int Status => twinklyapi.Status; // 1000 = good others not so much

        public string this[string columnName] => throw new System.NotImplementedException();

        public string Error => throw new System.NotImplementedException();

        private System.Timers.Timer updateTimer;

        public TwinkleFOX TFox { get; private set; }

        private string message = "";
        public string Message
        {
            get { return message; }
            set
            {
                message = value;
                OnPropertyChanged();
            }
        }

        private DateTime expiresat = DateTime.Now;
        public DateTime ExpiresAt
        {
            get { return expiresat; }
            private set
            {
                expiresat = value;
                OnPropertyChanged();
            }
        }

        private bool twinklyDetected = false;
        public bool TwinklyDetected
        {
            get { return twinklyDetected; }
            private set
            {
                twinklyDetected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private GestaltResult gestalt = new GestaltResult();
        public GestaltResult Gestalt
        {
            get { return gestalt; }
            set
            {
                gestalt = value;
                OnPropertyChanged();
                OnPropertyChanged("Uptime");
            }
        }

        private FWResult fw = new FWResult();
        public FWResult FW
        {
            get { return fw; }
            set
            {
                fw = value;
                OnPropertyChanged();
            }
        }

        // Schedule Twinkly On and off
        private Timer timer = new Timer() { time_on = -1, time_off = -1 };
        public Timer Timer
        {
            get { return timer; }
            set
            {
                timer = value;
                OnPropertyChanged();
                OnPropertyChanged("TimerNow");
                if (string.IsNullOrWhiteSpace(ScheduleOffText))
                    ScheduleOffText = value.time_off == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_off).ToString("HH:mm");
                if (string.IsNullOrWhiteSpace(ScheduleOnText))
                    ScheduleOnText = value.time_on == -1 ? "-1" : new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(value.time_on).ToString("HH:mm");
                OnPropertyChanged("TimerOn");
                OnPropertyChanged("TimerOff");
            }
        }

        // Edit Device Name
        private string myDeviceName;
        public string MyDeviceName
        {
            get { return myDeviceName; }
            set
            {
                if (myDeviceName == value) return;
                myDeviceName = value;
                Task.Run(async () => { await twinklyapi.SetDeviceName(myDeviceName); });
                OnPropertyChanged();
            }
        }

        public DateTime TimerNow => new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(timer.time_now);
        public DateTime TimerOn => new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(timer.time_on);
        public DateTime TimerOff => new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day).AddSeconds(timer.time_off);


        private string scheduleontext;
        public string ScheduleOnText
        {
            get { return scheduleontext; }
            set
            {
                scheduleontext = value;
                OnPropertyChanged();
            }
        }

        private string scheduleofftext;
        public string ScheduleOffText
        {
            get { return scheduleofftext; }
            set
            {
                scheduleofftext = value;
                OnPropertyChanged();
            }
        }


        private ModeResult currentmode = new ModeResult() { mode = "unknown" };
        public ModeResult CurrentMode
        {
            get { return currentmode; }
            set
            {
                if (currentmode.mode != value.mode)
                {
                    currentmode = value;
                    OnPropertyChanged();
                    OnPropertyChanged("CurrentMode_Movie");
                    OnPropertyChanged("CurrentMode_Off");
                    OnPropertyChanged("CurrentMode_Demo");
                    OnPropertyChanged("CurrentMode_Color");
                    OnPropertyChanged("CurrentMode_Effects");
                    OnPropertyChanged("CurrentMode_DemoOrEffects");
                    OnPropertyChanged("CurrentMode_Rt");
                }
            }
        }

        public bool CurrentMode_Movie => CurrentMode.mode == LedModes.movie.ToString();
        public bool CurrentMode_Off => CurrentMode.mode == LedModes.off.ToString();
        public bool CurrentMode_Demo => CurrentMode.mode == LedModes.demo.ToString();
        public bool CurrentMode_Color => CurrentMode.mode == LedModes.color.ToString();
        public bool CurrentMode_Effects => CurrentMode.mode == LedModes.effect.ToString();
        public bool CurrentMode_Rt => CurrentMode.mode == LedModes.rt.ToString();
        public bool CurrentMode_DemoOrEffects => CurrentMode_Demo || CurrentMode_Effects;

        private MergedEffectsResult effects;
        public MergedEffectsResult Effects
        {
            get { return effects; }
            set
            {
                effects = value;
                OnPropertyChanged();
            }
        }

        private MQTTConfigResult mqttconfig;
        public MQTTConfigResult MQTTConfig
        {
            get { return mqttconfig; }
            set
            {
                mqttconfig = value;
                OnPropertyChanged();
            }
        }

        private LedConfigResult ledconfg = new LedConfigResult();
        public LedConfigResult LedConfig
        {
            get { return ledconfg; }
            set
            {
                ledconfg = value;
                OnPropertyChanged();
                OnPropertyChanged("LedConfigDesc");
            }
        }

        public string LedConfigDesc
        {
            get
            {
                if (LedConfig?.strings is null)
                    return string.Empty;

                var sb = new StringBuilder();
                sb.Append($"{LedConfig.strings.Length} strings: ");
                foreach (var s in LedConfig.strings)
                {
                    sb.Append($"[{s.first_led_id}-{s.first_led_id + s.length - 1}],");
                }
                return sb.ToString().TrimEnd(',');
            }
        }

        private LedLayoutResult ledLayout = new LedLayoutResult();
        public LedLayoutResult LedLayout
        {
            get { return ledLayout; }
            set
            {
                ledLayout = value;
                OnPropertyChanged();
            }
        }

        private ColorResult ledColor = new ColorResult();
        public ColorResult LedColor
        {
            get { return ledColor; }
            set
            {
                ledColor = value;
                OnPropertyChanged();
            }
        }

        private SummaryResult summary = new SummaryResult();
        public SummaryResult Summary
        {
            get { return summary; }
            set
            {
                summary = value;
                OnPropertyChanged();
            }
        }

        private CurrentMovieConfig currentmovie = new CurrentMovieConfig();
        public CurrentMovieConfig CurrentMovie
        {
            get { return currentmovie; }
            set
            {
                currentmovie = value;
                OnPropertyChanged();
            }
        }

        private MoviesResult moviesresult = new MoviesResult();
        public MoviesResult MoviesResult
        {
            get { return moviesresult; }
            set
            {
                moviesresult = value;
                OnPropertyChanged();
            }
        }

        private PlaylistResult plresult = new PlaylistResult();
        public PlaylistResult Playlist
        {
            get { return plresult; }
            set
            {
                plresult = value;
                OnPropertyChanged();
            }
        }

        private CurrentPlaylistEntry cple = new CurrentPlaylistEntry();
        public CurrentPlaylistEntry PlaylistEntry
        {
            get { return cple; }
            set
            {
                cple = value;
                OnPropertyChanged();
            }
        }

        public bool DoNetworkScan { get; set; } = false;

        private NetworkScanResult networkscan = new NetworkScanResult();
        public NetworkScanResult NetworkScan
        {
            get { return networkscan; }
            set
            {
                networkscan = value;
                OnPropertyChanged();
            }
        }

        private NetworkStatus networkstatus = new NetworkStatus();
        public NetworkStatus NetworkStatus
        {
            get { return networkstatus; }
            set
            {
                networkstatus = value;
                OnPropertyChanged();
            }
        }

        private BrightnessResult brightness = new BrightnessResult() { mode = "disabled", value = 100 };
        public BrightnessResult Brightness
        {
            get { return brightness; }
            set
            {
                brightness = value;
                OnPropertyChanged();
                OnPropertyChanged("SliderBrightness");
            }
        }

        private SaturationResult saturation = new SaturationResult() { mode = "disabled", value = 100 };
        public SaturationResult Saturation
        {
            get { return saturation; }
            set
            {
                saturation = value;
                OnPropertyChanged();
                OnPropertyChanged("SliderSaturation");
            }
        }

        public int SliderBrightness
        {
            get { return Brightness.value; }
            set
            {
                if (value != Brightness.value)
                {
                    updateBrightness((byte)value).Wait(100);
                    OnPropertyChanged();
                }
            }
        }

        public int SliderSaturation
        {
            get { return Saturation.value; }
            set
            {
                if (value != Saturation.value)
                {
                    updateSaturation((byte)value).Wait(100);
                    OnPropertyChanged();
                }
            }
        }

        private async Task updateBrightness(byte b)
        {
            VerifyResult result = await twinklyapi.SetBrightness(b);
            if (result.code != 1000)
                Logging.WriteDbg($"Set Brightness fail - {result.code}");
            Brightness = await twinklyapi.GetBrightness();
        }

        private async Task updateSaturation(byte b)
        {
            VerifyResult result = await twinklyapi.SetSaturationAbs(b);
            if (result.code != 1000)
                Logging.WriteDbg($"Set Saturation fail - {result.code}");
            Saturation = await twinklyapi.GetSaturation();
        }

        // Set by view so our colours are calculated from the same source
        public GradientStopCollection GradientStops { get; private set; }

        private System.Timers.Timer slidepause;
        private Color TargetColor;

        private double currentcolor;

        // This is a Hue 0 - 359.999
        public double SliderColor
        {
            get { return currentcolor; }
            set
            {
                if (value != currentcolor)
                {
                    currentcolor = value;
                    TargetColor = HSBColor.FromHSB(new HSBColor((float)(value / 359.0 * 255), 255, 255));
                    //GradientStops.GetRelativeColor(value);

                    // user can keep sliding - wait for 1sec of no movement to change color
                    if (slidepause != null)
                        slidepause.Dispose();
                    slidepause = new System.Timers.Timer(1000) { AutoReset = false };
                    slidepause.Elapsed += ElapsedUpdateColor;
                    slidepause.Start();
                    OnPropertyChanged();
                }
            }
        }

        private void ElapsedUpdateColor(object sender, System.Timers.ElapsedEventArgs e)
        {
            Logging.WriteDbg($"Slider Color {TargetColor}");
            updateColor(TargetColor).Wait(100);
        }

        private async Task updateColor(Color c)
        {
            await twinklyapi.SingleColorRT(c.ToDrawingColor());

            //await twinklyapi.SingleColorMode(c.ToDrawingColor());
            //LedColor = await twinklyapi.GetColor();
        }

        public string IPAddress => twinklyapi.IPAddress;

        public TimeSpan Uptime => twinklyapi.Uptime;

        #region IDataErrorInfo

        private string errtext = string.Empty;
        string IDataErrorInfo.Error
        {
            get { return errtext; }
        }

        public object ErrorContent { get; set; }
        public object SingleGradient { get; set; }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName == "ScheduleOnText")
                {
                    if (isScheduleOnTextValid())
                    {
                        return null;
                    }
                    else
                    {
                        return "What Time?";
                    }
                }

                if (columnName == "ScheduleOffText")
                {
                    if (isScheduleOffTextValid())
                    {
                        return null;
                    }
                    else
                    {
                        return "What Time?";
                    }
                }

                // If there's no error, null gets returned
                return null;
            }
        }

        private bool isScheduleOnTextValid()
        {
            if (string.IsNullOrWhiteSpace(ScheduleOnText))
                return false;

            if (ScheduleOnText.Trim() == "-1")
                return true;
            if (DateTime.TryParse(ScheduleOnText, out _))
                return true;

            return false;
        }

        private bool isScheduleOffTextValid()
        {
            if (string.IsNullOrWhiteSpace(ScheduleOffText))
                return false;

            if (ScheduleOffText.Trim() == "-1")
                return true;
            if (DateTime.TryParse(ScheduleOffText, out _))
                return true;

            return false;
        }

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ip">IP to talk to</param>
        /// <param name="lgb">Gradient for color passing</param>
        public TwinklyViewModel(string ip, LinearGradientBrush lgb)
        {
            twinklyapi = new XLedAPI();
            var result = twinklyapi.ConnectTwinkly(ip);

            if (result == (int)HttpStatusCode.OK)
            {
                TwinklyDetected = true;
            }
            else
            {
                Message = "Twinkly Not Found !";
            }

            ModeCommand = new RelayCommand<string>(async (x) => await ChangeMode(x));

            UpdateTimerCommand = new RelayCommand(async () => await ChangeTimer());

            IncrementEffectCommand = new RelayCommand(async () => await IncrementEffect());

            FoxCommand = new RelayCommand(async () => await TFoxRunner());

            GradientStops = lgb.GradientStops.Clone();

        }

        #region TwinkleFOX

        private string foxpal;
        public string FoxPal
        {
            get { return foxpal; }
            set
            {
                foxpal = value;
                OnPropertyChanged();
            }
        }

        private int foxloop;
        public int FoxLoop
        {
            get { return foxloop; }
            set
            {
                foxloop = value;
                OnPropertyChanged();
            }
        }

        private double foxfps;
        public double FoxFPS
        {
            get { return foxfps; }
            set
            {
                foxfps = value;
                OnPropertyChanged();
            }
        }

        public int FoxSpeed
        {
            get { return TFox?.TWINKLE_SPEED ?? 0; }
            set
            {
                TFox.TWINKLE_SPEED = value;
                OnPropertyChanged();
            }
        }

        public int FoxDensity
        {
            get { return TFox?.TWINKLE_DENSITY ?? 0; }
            set
            {
                TFox.TWINKLE_DENSITY = value;
                OnPropertyChanged();
            }
        }

        public bool FoxAutoBG
        {
            get { return TFox?.AUTO_SELECT_BACKGROUND_COLOR ?? false; }
            set
            {
                TFox.AUTO_SELECT_BACKGROUND_COLOR = value;
                OnPropertyChanged();
            }
        }

        public bool FoxCool
        {
            get { return TFox?.COOL_LIKE_INCANDESCENT ?? false; }
            set
            {
                TFox.COOL_LIKE_INCANDESCENT = value;
                OnPropertyChanged();
            }
        }

        private bool Foxrunning = false;
        private async Task TFoxRunner()
        {
            if (Foxrunning)
                Foxrunning = false;
            else
            {
                Foxrunning = true;
                TFox.Reset();
                while (Foxrunning)
                {
                    await TFox.loop()
                        .ContinueWith(async (x) => await twinklyapi.SendRtFrame(TFox.Leds))
                        .ContinueWith((x) =>
                        {
                            FoxPal = TFox.CurrentPalette;
                            FoxLoop = TFox.LoopCount;
                            FoxFPS = TFox.FPS;
                        });
                    // limit FPS ?
                    var MaxFPS = 255;
                    if (FoxFPS > MaxFPS)
                    {
                        await Task.Delay((int)((int)(FoxFPS - MaxFPS) * 1000 / FoxFPS));
                    }
                }
            }
        }

        #endregion;

        public async Task Load()
        {
            if (!TwinklyDetected)
                return;

            try
            {
                Message = "Loading...";

                //gestalt
                Gestalt = await twinklyapi.Info();
                MyDeviceName = Gestalt.device_name;
                if (twinklyapi.Status == (int)HttpStatusCode.OK)
                {
                    FW = await twinklyapi.Firmware();
                }

                if (twinklyapi.Status == (int)HttpStatusCode.OK)
                {
                    if (!await twinklyapi.Login())
                        Message = $"Login Fail {twinklyapi.Status}";
                    else
                    {
                        ExpiresAt = twinklyapi.ExpiresAt;
                        Message = $"Login Success until {twinklyapi.ExpiresAt:g}";
                    }
                }
                else
                    Message = $"ERROR: {twinklyapi.Status}";

                // update the authenticated api models
                await UpdateAuthModels();

                updateTimer = new System.Timers.Timer(5000) { AutoReset = true };
                updateTimer.Elapsed += refreshGui;
                updateTimer.Start();

                TFox = new TwinkleFOX(twinklyapi.NumLED, twinklyapi.BytesPerLed);
                FoxDensity = TFox.TWINKLE_DENSITY;
                FoxSpeed = TFox.TWINKLE_SPEED;
                FoxAutoBG = TFox.AUTO_SELECT_BACKGROUND_COLOR;
                FoxCool = TFox.COOL_LIKE_INCANDESCENT;
            }
            catch (Exception ex)
            {
                Message = $"Exception Loading {ex.Message}";
            }
        }

        public void Unload()
        {
            if (!TwinklyDetected) return;

            TwinklyDetected = false;
            updateTimer.Stop();
            Message = "Unloaded";
            Logging.WriteDbg($"Unloading {twinklyapi.IPAddress}");
        }

        private async void refreshGui(object sender, System.Timers.ElapsedEventArgs e)
        {
            await UpdateAuthModels();
        }

        private int loopcounter = 0;
        /// <summary>
        /// Main Update Json objects
        /// </summary>
        private async Task UpdateAuthModels()
        {
            Logging.WriteDbg("----------------");
            if (twinklyapi is null) return;

            Gestalt = await twinklyapi.Info();
            MyDeviceName = Gestalt.device_name;

            // update the authenticated api models
            if (twinklyapi.Authenticated)
            {
                Timer = await twinklyapi.GetTimer();
                CurrentMode = await twinklyapi.GetOperationMode();
                Effects = await twinklyapi.EffectsAllinOne();
                Brightness = await twinklyapi.GetBrightness();
                Saturation = await twinklyapi.GetSaturation();

                MQTTConfig = await twinklyapi.GetMQTTConfig();
                if (DoNetworkScan || loopcounter == 0)
                {
                    NetworkScan = await twinklyapi.NetworkScan();
                }
                NetworkStatus = await twinklyapi.GetNetworkStatus();

                MoviesResult = await twinklyapi.GetMovies();
                CurrentMovie = await twinklyapi.GetMovieConfig();
                Playlist = await twinklyapi.GetPlaylist();
                if (Playlist.entries?.Length > 0)
                {
                    PlaylistEntry = await twinklyapi.GetPlaylistCurrent();  // returns 204 NoContent if Playlist is empty
                }

                LedConfig = await twinklyapi.GetLEDConfig();
                LedColor = await twinklyapi.GetColor();
                Summary = await twinklyapi.GetSummary();
                LedLayout = await twinklyapi.GetLEDLayout();

                //var x = await twinklyapi.Echo("KMB was here");
                OnPropertyChanged("Status");
                loopcounter++;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// Command to call the API to change the mode
        /// </summary>
        private async Task ChangeMode(string mode)
        {
            var result = new VerifyResult();
            switch (mode.ToLowerInvariant())
            {
                case "off":
                    result = await twinklyapi.SetOperationMode(LedModes.off);
                    break;
                case "demo":
                    result = await twinklyapi.SetOperationMode(LedModes.demo);
                    break;
                case "color":
                    result = await twinklyapi.SetOperationMode(LedModes.color);
                    break;
                case "movie":
                    result = await twinklyapi.SetOperationMode(LedModes.movie);
                    break;
                case "effect":
                    result = await twinklyapi.SetOperationMode(LedModes.effect);
                    break;
                case "playlist":
                    result = await twinklyapi.SetOperationMode(LedModes.playlist);
                    break;
            }

            // refresh gui
            if (result.code == 1000)
                CurrentMode = await twinklyapi.GetOperationMode();
        }

        private async Task ChangeTimer()
        {

            if (isScheduleOnTextValid() && isScheduleOffTextValid())
            {
                int on;
                int off;
                if (ScheduleOnText.Trim() == "-1")
                    on = -1;
                else
                {
                    var dton = DateTime.Parse(ScheduleOnText);
                    on = (int)(dton - DateTime.Today).TotalSeconds;
                }

                if (ScheduleOffText.Trim() == "-1")
                    off = -1;
                else
                {
                    var dtoff = DateTime.Parse(ScheduleOffText);
                    off = (int)(dtoff - DateTime.Today).TotalSeconds;
                }
                VerifyResult result = await twinklyapi.SetTimer(DateTime.Now, on, off);

                // refresh gui
                if (result.code == 1000)
                {
                    ScheduleOnText = null;
                    ScheduleOffText = null;
                    Timer = await twinklyapi.GetTimer();
                }
            }
        }

        /// <summary>
        /// Next Effect with wrapping
        /// </summary>
        private async Task IncrementEffect()
        {
            var fx = Effects.effect_id;
            fx += 1;
            if (fx >= Effects.effects_number)
                fx = 0;

            if (!CurrentMode_Effects)
                await ChangeMode(LedModes.effect.ToString());
            await twinklyapi.SetCurrentEffects(fx);
            Effects = await twinklyapi.EffectsAllinOne();
        }

    }
}
