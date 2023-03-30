using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using dgMicMute.Implementations;
using dgMicMute.MvvmHelper;
using dgMicMute.Properties;
using Microsoft.Win32;

namespace dgMicMute
{
    public class NotifyIconViewModel : INotifyPropertyChanged
    {
        private static RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone", true);
        private static RegistryKey registryKeyNonPackaged = registryKey.OpenSubKey("NonPackaged", true);
        private string _iconPath;
        private DgMic _mic;
        private readonly System.Media.SoundPlayer _offSoundPlayer = new System.Media.SoundPlayer(Resources.off);
        private readonly System.Media.SoundPlayer _onSoundPlayer = new System.Media.SoundPlayer(Resources.on);

        public bool IsBlockMicPrivacy
        {
            get
            {
                RegistryKey registryKeyNonPackaged = registryKey.OpenSubKey("NonPackaged", true);
                bool value = false;
                if (registryKey.GetValue("Value") == null)
                {
                    registryKey.SetValue("Value", "Allow");
                }
                if (registryKeyNonPackaged.GetValue("Value") == null)
                {
                    registryKeyNonPackaged.SetValue("Value", "Allow");
                }
                if (registryKey.GetValue("Value").ToString() == "Deny" || registryKeyNonPackaged.GetValue("Value").ToString() == "Deny")
                {
                    value = true;
                }
                else
                {
                    value = false;
                }
                return value;
            }
            set
            {

                if (registryKey.GetValue("Value") == null)
                {
                    registryKey.SetValue("Value", "Allow");
                }
                if (registryKeyNonPackaged.GetValue("Value") == null)
                {
                    registryKeyNonPackaged.SetValue("Value", "Allow");
                }
                if (value == false)
                {
                    registryKey.SetValue("Value", "Allow");
                    registryKeyNonPackaged.SetValue("Value", "Allow");
                    Settings.IsBlockMicPrivacy = false;
                }
                else
                {
                    registryKey.SetValue("Value", "Deny");
                    registryKeyNonPackaged.SetValue("Value", "Deny");
                    Settings.IsBlockMicPrivacy = true;
                }
                SerializeStatic.Save(typeof(Settings));
                OnPropertyChanged("IsBlockMicPrivacy");
            }
        }

        public bool IsMuted
        {
            get
            {
                IconPath = Settings.IsMuted ? @"res\microphone_muted.ico" : @"res\microphone_unmuted.ico";
                if (Settings.IsBlockMicPrivacy == true)
                {
                    IconPath = @"res\microphone_muted.ico";
                }
                _mic.SetMicStateTo(Settings.IsMuted ? DgMicStates.Muted : DgMicStates.Unmuted);
                return Settings.IsMuted;
            }
            set
            {
                if (Settings.PlaysSound && Settings.IsMuted != value)
                {
                    PlaySound(value);
                }
                Settings.IsMuted = value;
                IconPath = Settings.IsMuted ? @"res\microphone_muted.ico" : @"res\microphone_unmuted.ico";
                if (Settings.IsBlockMicPrivacy == true)
                {
                    IconPath = @"res\microphone_muted.ico";
                }
                _mic.SetMicStateTo(Settings.IsMuted ? DgMicStates.Muted : DgMicStates.Unmuted);
                SerializeStatic.Save(typeof(Settings));
                OnPropertyChanged("IsMuted");
            }
        }

        public bool IsForced
        {
            get
            {
                return Settings.IsForced;
            }
            set
            {
                Settings.IsForced = value;
                SerializeStatic.Save(typeof(Settings));
                OnPropertyChanged("IsForced");
            }
        }

        public string IconPath
        {
            get
            {
                return _iconPath;
            }
            set
            {
                _iconPath = value;
                OnPropertyChanged("IconPath");
            }
        }

        public ICommand ExitApplicationCommand { get; set; }
        public ICommand ForkOnGitHubCommand { get; set; }
        public ICommand OpenSettingsWindowCommand { get; set; }
        public ICommand ToggleMicrophoneCommand { get; set; }

        public NotifyIconViewModel()
        {
            IconPath = @"res\microphone_unmuted.ico";
            _mic = new DgMic();
            _mic.OnVolumeNotification += _mic_OnVolumeNotification;
            ExitApplicationCommand = new RelayCommand(ExitApplication);
            ForkOnGitHubCommand = new RelayCommand(ForkOnGithub);
            OpenSettingsWindowCommand = new RelayCommand(OpenSettingsWindow);
            ToggleMicrophoneCommand = new RelayCommand(ToggleMicrophone);
            TimerStart();
        }

        public void RefreshMicList(object sender, EventArgs e)
        {
            _mic.Dispose();
            _mic = new DgMic();
            _mic.OnVolumeNotification += _mic_OnVolumeNotification;
            IsMuted = Settings.IsMuted; // reassert the muted state on the new set of mics
            IsBlockMicPrivacy = Settings.IsBlockMicPrivacy;
            TimerStart();
        }

        private void ToggleMicrophone(object obj)
        {
            IsMuted = !Settings.IsMuted; // changed this to Settings.IsMuted instead of this.IsMuted to skip the unneeded side effect calls of that getter
        }

        public void HotkeyPressed(object sender, KeyPressedEventArgs e)
        {
            ToggleMicrophone(null);
        }

        private void OpenSettingsWindow(object obj)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }

        private void ForkOnGithub(object obj)
        {
            Process.Start("https://github.com/DanielGilbert/dgMicMute");
        }

        private void PlaySound(bool isMuted)
        {
            var sound = isMuted ? _offSoundPlayer : _onSoundPlayer;
            sound.Play();
        }

        private void _mic_OnVolumeNotification(Implementations.AudioVolumeNotificationData data)
        {
            if (IsForced)
                //Everything that happens in the callback,
                //must be done in a non-blocking way.
                //Therefore, we need to invoke a new thread via the dispatcher,
                //because we cannot simply get information from the interface while
                //we are handling the callback.
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    IsMuted = IsMuted;
                }));
            else
                IsMuted = data.Muted;
        }

        private void ExitApplication(object obj)
        {
            _mic.SetMicStateTo(DgMicStates.Unmuted);
            Application.Current.Shutdown();
        }

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void TimerStart()
        {
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 2);
            dispatcherTimer.Start();
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            bool status = IsBlockMicPrivacy;
            OnPropertyChanged("IsBlockMicPrivacy");
            IconPath = status ? @"res\microphone_muted.ico" : @"res\microphone_unmuted.ico";
            if (Settings.IsMuted == true)
            {
                IconPath = Settings.IsMuted ? @"res\microphone_muted.ico" : @"res\microphone_unmuted.ico";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}