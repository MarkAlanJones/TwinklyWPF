using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Twinkly_xled;

namespace TwinklyWPF
{
    // --------------------------------------------------------------------------
    //       API docs - https://xled-docs.readthedocs.io/en/latest/rest_api.html
    // Python library - https://github.com/scrool/xled
    // --------------------------------------------------------------------------

    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand ReDetectCommand { get; private set; }

        public MainViewModel(string[] args)
        {
            ReDetectCommand = new RelayCommand(async () => await Reload());
        }

        public bool TwinklyDetected => DetectedTwinklys?.Any() ?? false;

        private string message = string.Empty;
        public string Message
        {
            get { return message; }
            set
            {
                message = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<TwinklyViewModel> twinklyViewModels = new();

        /// <summary>
        /// Main ViewModel collects the Twinklys on the Network
        /// </summary>
        public ObservableCollection<TwinklyViewModel> DetectedTwinklys
        {
            get { return twinklyViewModels; }
            private set
            {
                twinklyViewModels = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TwinklyDetected));
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private LinearGradientBrush gradBrush;
        internal async Task Load(LinearGradientBrush lgb)
        {
            gradBrush = lgb;
            await Reload();
            
        }

        internal async Task Reload()
        {
            Message = "Searching 🕵...";

            if (DetectedTwinklys.Any())
            {
                foreach (var twink in DetectedTwinklys)
                    twink.Unload();
            }

            try
            {
                IEnumerable<TwinklyInstance> twinklyips;
                twinklyips = await Task.Run(XLedAPI.Detect);
                if ((bool)(twinklyips?.Any()))
                {
                    twinklyViewModels.Clear();
                    foreach (var twink in twinklyips)
                        twinklyViewModels.Add(new TwinklyViewModel(twink.Address, gradBrush));

                    foreach (var twink in DetectedTwinklys)
                        await twink.Load();

                    Message = $"Detected {twinklyViewModels.Count} Twinkly{(twinklyViewModels.Count != 1 ? "s" : "")} 💡";
                    OnPropertyChanged(nameof(TwinklyDetected));
                    OnPropertyChanged(nameof(DetectedTwinklys));
                }
                else
                    Message = "No Twinklys Detected 😿";
            }
            catch (Exception ex)
            {
                Message = ex.Message;
            }
        }
    }
}
