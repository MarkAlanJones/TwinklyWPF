﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        public MainViewModel(string[] args)
        {
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

        public ObservableCollection<TwinklyViewModel> DetectedTwinklys
        {
            get { return twinklyViewModels; }
            set
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

        internal async void Load()
        {
            try
            {
                IEnumerable<TwinklyInstance> twinklyips;
                twinklyips = await Task.Run(() => { return XLedAPI.Detect(); });
                if ((bool)(twinklyips?.Any()))
                {
                    twinklyViewModels.Clear();
                    foreach (var twink in twinklyips)
                        twinklyViewModels.Add(new TwinklyViewModel(twink.Address));

                    foreach (var twink in DetectedTwinklys)
                        await twink.Load();

                    Message = $"Detected {twinklyViewModels.Count} Twinklys 💡";
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
