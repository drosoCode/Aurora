﻿using Aurora.Profiles.Aurora_Wrapper;
using Aurora.Profiles.Desktop;
using Aurora.Profiles.Generic_Application;
using Aurora.Profiles.Overlays.SkypeOverlay;
using Aurora.Settings;
using Aurora.Settings.Layers;
using Aurora.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace Aurora.Profiles
{
    public class LayerHandlerEntry
    {
        public Type Type { get; set; }

        public string Title { get; set; }

        public string Key { get; set; }

        public LayerHandlerEntry(string key, string title, Type type)
        {
            this.Type = type;
            this.Title = title;
            this.Key = key;
        }

        public override string ToString()
        {
            return Title;
        }
    }    

    public class ProfilesManagerSettings
    {
        public ProfilesManagerSettings()
        {

        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {

        }
    }

    public class LightingStateManager : ObjectSettings<ProfilesManagerSettings>, IInit
    {

        public Desktop.Desktop DesktopProfile { get { return (Desktop.Desktop)Events["desktop"]; } }

        private List<string> Underlays = new List<string>();
        private List<string> Overlays = new List<string>();

        private Dictionary<string, string> EventProcesses { get; set; } = new Dictionary<string, string>();

        private Dictionary<string, string> EventAppIDs { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, LayerHandlerEntry> LayerHandlers { get; private set; } = new Dictionary<string, LayerHandlerEntry>();

        public List<string> DefaultLayerHandlers { get; private set; } = new List<string>();

        public string AdditionalProfilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora", "AdditionalProfiles");

        private ActiveProcessMonitor processMonitor;

        public LightingStateManager()
        {
            SettingsSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aurora", "ProfilesSettings.json");
        }

        public bool Initialized { get; private set; }

        public bool Initialize()
        {
            if (Initialized)
                return true;

            processMonitor = new ActiveProcessMonitor();

            #region Initiate Defaults
            RegisterEvents(new List<ILightEvent> {
                new Desktop.Desktop(),
                new Dota_2.Dota2(),
                new CSGO.CSGO(),
                new GTA5.GTA5(),
                new RocketLeague.RocketLeague(),
                new Overwatch.Overwatch(),
                new Payday_2.PD2(),
                new TheDivision.TheDivision(),
                new LeagueOfLegends.LoL(),
                new HotlineMiami.HotlineMiami(),
                new TheTalosPrinciple.TalosPrinciple(),
                new BF3.BF3(),
                new Blacklight.Blacklight(),
                new Magic_Duels_2012.MagicDuels2012(),
                new ShadowOfMordor.ShadowOfMordor(),
                new Serious_Sam_3.SSam3(),
                new DiscoDodgeball.DiscoDodgeballApplication(),
                new XCOM.XCOM(),
                new Evolve.Evolve(),
                new Metro_Last_Light.MetroLL(),
                new Guild_Wars_2.GW2(),
                new WormsWMD.WormsWMD(),
                new Blade_and_Soul.BnS(),
                new Event_SkypeOverlay()
            });

            RegisterLayerHandlers(new List<LayerHandlerEntry> {
                new LayerHandlerEntry("Default", "Default Layer", typeof(DefaultLayerHandler)),
                new LayerHandlerEntry("Solid", "Solid Color Layer", typeof(SolidColorLayerHandler)),
                new LayerHandlerEntry("SolidFilled", "Solid Fill Color Layer", typeof(SolidFillLayerHandler)),
                new LayerHandlerEntry("Gradient", "Gradient Layer", typeof(GradientLayerHandler)),
                new LayerHandlerEntry("GradientFill", "Gradient Fill Layer", typeof(GradientFillLayerHandler)),
                new LayerHandlerEntry("Breathing", "Breathing Layer", typeof(BreathingLayerHandler)),
                new LayerHandlerEntry("Blinking", "Blinking Layer", typeof(BlinkingLayerHandler)),
                new LayerHandlerEntry("Image", "Image Layer", typeof(ImageLayerHandler)),
                new LayerHandlerEntry("Script", "Script Layer", typeof(ScriptLayerHandler)),
                new LayerHandlerEntry("Percent", "Percent Effect Layer", typeof(PercentLayerHandler)),
                new LayerHandlerEntry("PercentGradient", "Percent (Gradient) Effect Layer", typeof(PercentGradientLayerHandler)),
                new LayerHandlerEntry("Interactive", "Interactive Layer", typeof(InteractiveLayerHandler) ),
                new LayerHandlerEntry("ShortcutAssistant", "Shortcut Assistant Layer", typeof(ShortcutAssistantLayerHandler) ),
                new LayerHandlerEntry("Equalizer", "Equalizer Layer", typeof(EqualizerLayerHandler) ),
                new LayerHandlerEntry("Ambilight", "Ambilight Layer", typeof(AmbilightLayerHandler) ),
                new LayerHandlerEntry("LockColor", "Lock Color Layer", typeof(LockColourLayerHandler) ),
                new LayerHandlerEntry("Glitch", "Glitch Effect Layer", typeof(GlitchLayerHandler) ),
                new LayerHandlerEntry("Animation", "Animation Layer", typeof(AnimationLayerHandler) ),
            }, true);

            #endregion

            LoadSettings();

            this.LoadPlugins();

            if (Directory.Exists(AdditionalProfilesPath))
            {
                List<string> additionals = new List<string>(Directory.EnumerateDirectories(AdditionalProfilesPath));
                foreach (var dir in additionals)
                {
                    if (File.Exists(Path.Combine(dir, "default.json")))
                    {
                        string proccess_name = Path.GetFileName(dir);
                        RegisterEvent(new GenericApplication(proccess_name));
                    }
                }
            }

            foreach (var profile in Events)
            {
                profile.Value.Initialize();
            }

            this.InitUpdate();

            Initialized = true;
            return Initialized;
        }

        private void LoadPlugins()
        {
            Global.PluginManager.ProcessManager(this);
        }

        protected override void LoadSettings(Type settingsType)
        {
            base.LoadSettings(settingsType);

            foreach (var kvp in Events)
            {
                if (!Global.Configuration.ProfileOrder.Contains(kvp.Key) && kvp.Value is Application)
                    Global.Configuration.ProfileOrder.Add(kvp.Key);
            }

            foreach(string key in Global.Configuration.ProfileOrder.ToList())
            {
                if (!Events.ContainsKey(key) || !(Events[key] is Application))
                    Global.Configuration.ProfileOrder.Remove(key);
            }

            Global.Configuration.ProfileOrder.Remove("desktop");
            Global.Configuration.ProfileOrder.Insert(0, "desktop");
        }

        public void SaveAll()
        {
            SaveSettings();

            foreach(var profile in Events)
                if (profile.Value is Application)
                    ((Application)profile.Value).SaveAll();
            }
        }

        public bool RegisterProfile(LightEventConfig config)
        {
            return RegisterEvent(new Application(config));
        }

        private List<string> GetEventTable(LightEventType type)
        {
            List<string> events;
            switch (type)
            {
                case LightEventType.Normal:
                    events = Normal;
                    break;
                case LightEventType.Overlay:
                    events = Overlays;
                    break;
                case LightEventType.Underlay:
                    events = Underlays;
                    break;
                default:
                    throw new NotImplementedException();
            }
            return events;
        }

        private bool InsertLightEvent(ILightEvent lightEvent, LightEventType? old = null)
        {
            LightEventType type = lightEvent.Config.Type ?? LightEventType.Normal;
            lightEvent.Config.Type = type;

            if (old == null)
            {
                lightEvent.Config.PropertyChanged += LightEvent_PropertyChanged;
            }
            else
            {
                var oldEvents = GetEventTable((LightEventType)old);
                oldEvents.Remove(lightEvent.Config.ID);
            }

            var events = GetEventTable(type);

            events.Add(lightEvent.Config.ID);

            return true;   
        }

        private void LightEvent_PropertyChanged(object sender, PropertyChangedExEventArgs e)
        {
            ILightEvent lightEvent = (ILightEvent)sender;
            if (e.PropertyName.Equals(nameof(LightEventConfig.Type)))
            {
                LightEventType old = (LightEventType)e.OldValue;
                LightEventType newVal = (LightEventType)e.NewValue;

                if (!old.Equals(newVal))
                {
                    InsertLightEvent(lightEvent, old);
                }
            }
        }

        public bool RegisterEvent(ILightEvent @event)
        {
            string key = @event.Config.ID;
            if (string.IsNullOrWhiteSpace(key) || Events.ContainsKey(key))
                return false;

            Events.Add(key, @event);

            if (@event.Config.ProcessNames != null)
            {
                foreach (string exe in @event.Config.ProcessNames)
                {
                    if (!exe.Equals(key))
                        EventProcesses.Add(exe, key);
                }
            }

            if (!String.IsNullOrWhiteSpace(@event.Config.AppID))
                EventAppIDs.Add(@event.Config.AppID, key);

            if (@event is Application)
            {
                if (!Global.Configuration.ProfileOrder.Contains(key))
                    Global.Configuration.ProfileOrder.Add(key);
            }

            this.InsertLightEvent(@event);

            if (Initialized)
                @event.Initialize();

            return true;
        }

        public void RegisterProfiles(List<LightEventConfig> profiles)
        {
            foreach (var profile in profiles)
            {
                RegisterProfile(profile);
            }
        }

        public void RegisterEvents(List<ILightEvent> profiles)
        {
            foreach (var profile in profiles)
            {
                RegisterEvent(profile);
            }
        }

        public void RemoveGenericProfile(string key)
        {
            if (Events.ContainsKey(key))
            {
                if (!(Events[key] is GenericApplication))
                    return;
                GenericApplication profile = (GenericApplication)Events[key];
                string path = profile.GetProfileFolderPath();
                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                Events.Remove(key);
                Global.Configuration.ProfileOrder.Remove(key);

                //SaveSettings();
            }
        }

        public ILightEvent GetProfileFromProcess(string process)
        {
            if (EventProcesses.ContainsKey(process))
            {
                if (!Events.ContainsKey(EventProcesses[process]))
                    Global.logger.LogLine($"GetProfileFromProcess: The process '{process}' exists in EventProcesses but subsequently '{EventProcesses[process]}' does not in Events!", Logging_Level.Warning);

                return Events[EventProcesses[process]];
            }
            else if (Events.ContainsKey(process))
                return Events[process];

            return null;
        }

        public ILightEvent GetProfileFromAppID(string appid)
        {
            if (EventAppIDs.ContainsKey(appid))
            {
                if (!Events.ContainsKey(EventAppIDs[appid]))
                    Global.logger.LogLine($"GetProfileFromAppID: The appid '{appid}' exists in EventAppIDs but subsequently '{EventAppIDs[appid]}' does not in Events!", Logging_Level.Warning);
                return Events[EventAppIDs[appid]];
            }
            else if (Events.ContainsKey(appid))
                return Events[appid];

            return null;
        }

        public void RegisterLayerHandlers(List<LayerHandlerEntry> layers, bool @default = true)
        {
            foreach(var layer in layers)
            {
                RegisterLayerHandler(layer, @default);
            }
        }

        public bool RegisterLayerHandler(LayerHandlerEntry entry, bool @default = true)
        {
            if (LayerHandlers.ContainsKey(entry.Key) || DefaultLayerHandlers.Contains(entry.Key))
                return false;

            LayerHandlers.Add(entry.Key, entry);

            if (@default)
                DefaultLayerHandlers.Add(entry.Key);

            return true;
        }

        public bool RegisterLayerHandler(string key, string title, Type type, bool @default = true)
        {
            return RegisterLayerHandler(new LayerHandlerEntry(key, title, type));
        }

        public Type GetLayerHandlerType(string key)
        {
            return LayerHandlers.ContainsKey(key) ? LayerHandlers[key].Type : null;
        }

        public ILayerHandler GetLayerHandlerInstance(LayerHandlerEntry entry)
        {
            return (ILayerHandler)Activator.CreateInstance(entry.Type);
        }

        public ILayerHandler GetLayerHandlerInstance(string key)
        {
            if (LayerHandlers.ContainsKey(key))
            {
                return GetLayerHandlerInstance(LayerHandlers[key]);
            }


            return null;
        }

        private Timer updateTimer;

        private const int defaultTimerInterval = 33;
        private int timerInterval = defaultTimerInterval;

        private long nextProcessNameUpdate;
        private long currentTick;
        private string previewModeProfileKey = "";

        private List<TimedListObject> overlays = new List<TimedListObject>();
        private Event_Idle idle_e = new Event_Idle();

        public string PreviewProfileKey { get { return previewModeProfileKey; } set { previewModeProfileKey = value ?? string.Empty; } }

        private void InitUpdate()
        {
            updateTimer = new System.Threading.Timer(g => {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                if (Global.isDebug)
                    Update();
                else
                {
                    try
                    {
                        Update();
                    }
                    catch (Exception exc)
                    {
                        Global.logger.LogLine("ProfilesManager.Update() Exception, " + exc, Logging_Level.Error);
                    }
                }

                watch.Stop();
                currentTick += timerInterval + watch.ElapsedMilliseconds;
                updateTimer?.Change(Math.Max(timerInterval, 0), Timeout.Infinite);
            }, null, 0, System.Threading.Timeout.Infinite);
            GC.KeepAlive(updateTimer);
        }

        private void UpdateProcess()
        {
            if (Global.Configuration.detection_mode == ApplicationDetectionMode.ForegroroundApp && (currentTick >= nextProcessNameUpdate))
            {
                processMonitor.GetActiveWindowsProcessname();
                nextProcessNameUpdate = currentTick + 1000L;
            }
        }

        private void UpdateIdleEffects(EffectsEngine.EffectFrame newFrame)
        {
            tagLASTINPUTINFO LastInput = new tagLASTINPUTINFO();
            Int32 IdleTime;
            LastInput.cbSize = (uint)Marshal.SizeOf(LastInput);
            LastInput.dwTime = 0;

            if (ActiveProcessMonitor.GetLastInputInfo(ref LastInput))
            {
                IdleTime = System.Environment.TickCount - LastInput.dwTime;

                if (IdleTime >= Global.Configuration.idle_delay * 60 * 1000)
                {
                    if (!(Global.Configuration.time_based_dimming_enabled &&
                    Utils.Time.IsCurrentTimeBetween(Global.Configuration.time_based_dimming_start_hour, Global.Configuration.time_based_dimming_start_minute, Global.Configuration.time_based_dimming_end_hour, Global.Configuration.time_based_dimming_end_minute))
                    )
                    {
                        idle_e.UpdateLights(newFrame);
                    }
                }
            }
        }

        private void Update()
        {
            //Blackout. TODO: Cleanup this a bit. Maybe push blank effect frame to keyboard incase it has existing stuff displayed
            if ((Global.Configuration.time_based_dimming_enabled &&
               Utils.Time.IsCurrentTimeBetween(Global.Configuration.time_based_dimming_start_hour, Global.Configuration.time_based_dimming_start_minute, Global.Configuration.time_based_dimming_end_hour, Global.Configuration.time_based_dimming_end_minute)))
                return;

            UpdateProcess();

            string process_name = System.IO.Path.GetFileName(processMonitor.ProcessPath).ToLowerInvariant();

            EffectsEngine.EffectFrame newFrame = new EffectsEngine.EffectFrame();

            

            //TODO: Move these IdleEffects to an event
            //this.UpdateIdleEffects(newFrame);

            ILightEvent profile = null;
            ILightEvent tempProfile = null;
            bool preview = false;
            //Global.logger.LogLine(process_name);
            if (!Global.Configuration.excluded_programs.Contains(process_name)) {
                //TODO: GetProfile that checks based on event type
                if (((tempProfile = GetProfileFromProcess(process_name)) != null) && tempProfile.Config.Type == LightEventType.Normal && tempProfile.IsEnabled)
                    profile = tempProfile;
                else if ((tempProfile = GetProfileFromProcess(previewModeProfileKey)) != null) //Don't check for it being Enabled as a preview should always end-up with the previewed profile regardless of it being disabled
                {
                    profile = tempProfile;
                    preview = true;
                }
                else if (Global.Configuration.allow_wrappers_in_background && Global.net_listener != null && Global.net_listener.IsWrapperConnected && ((tempProfile = GetProfileFromProcess(Global.net_listener.WrappedProcess)) != null) && tempProfile.Config.Type == LightEventType.Normal && tempProfile.Config.ProcessNames.Contains(process_name) && tempProfile.IsEnabled)
                    profile = tempProfile;
            }

            profile = profile ?? DesktopProfile;

            timerInterval = (profile as Application)?.Config?.UpdateInterval ?? defaultTimerInterval;

            //Check if any keybinds have been triggered
            foreach(var prof in (profile as Application).Profiles)
            {
                if(prof.TriggerKeybind.IsPressed() && !(profile as Application).Profile.ProfileName.Equals(prof.ProfileName))
                {
                    (profile as Application).SwitchToProfile(prof);
                    break;
                }
            }

            Global.dev_manager.InitializeOnce();
            if (Global.Configuration.OverlaysInPreview || !preview)
            {
                foreach (var underlay in Underlays)
                {
                    ILightEvent @event = Events[underlay];
                    if (@event.IsEnabled && (@event.Config.ProcessNames == null || ProcessUtils.AnyProcessExists(@event.Config.ProcessNames)))
                        @event.UpdateLights(newFrame);
                }
            }

            //Need to do another check in case Desktop is disabled or the selected preview is disabled
            if (profile.IsEnabled)
                profile.UpdateLights(newFrame);

            if (Global.Configuration.OverlaysInPreview || !preview)
            {
                foreach (var overlay in Overlays)
                {
                    ILightEvent @event = Events[overlay];
                    if (@event.IsEnabled && (@event.Config.ProcessNames == null || ProcessUtils.AnyProcessExists(@event.Config.ProcessNames)))
                        @event.UpdateLights(newFrame);
                }

                //Add overlays
                TimedListObject[] overlay_events = overlays.ToArray();
                foreach (TimedListObject evnt in overlay_events)
                {
                    if ((evnt.item as LightEvent).IsEnabled)
                        (evnt.item as LightEvent).UpdateLights(newFrame);
                }

                UpdateIdleEffects(newFrame);
            }

            Global.effengine.PushFrame(newFrame);
        }

        public void GameStateUpdate(IGameState gs)
        {
            //Debug.WriteLine("Received gs!");

            //Global.logger.LogLine(gs.ToString(), Logging_Level.None, false);

            //UpdateProcess();

            string process_name = System.IO.Path.GetFileName(processMonitor.ProcessPath).ToLowerInvariant();

            //EffectsEngine.EffectFrame newFrame = new EffectsEngine.EffectFrame();

            try
            {
                ILightEvent profile = this.GetProfileFromProcess(process_name);

                JObject provider = Newtonsoft.Json.Linq.JObject.Parse(gs.GetNode("provider"));
                string appid = provider.GetValue("appid").ToString();
                string name = provider.GetValue("name").ToString().ToLowerInvariant();

                if (profile != null || (profile = GetProfileFromAppID(appid)) != null || (profile = GetProfileFromProcess(name)) != null)
                    profile.SetGameState((IGameState)Activator.CreateInstance(profile.Config.GameStateType, gs));
                else if (gs is GameState_Wrapper && Global.Configuration.allow_all_logitech_bitmaps)
                {
                    string gs_process_name = Newtonsoft.Json.Linq.JObject.Parse(gs.GetNode("provider")).GetValue("name").ToString().ToLowerInvariant();

                    profile = profile ?? GetProfileFromProcess(gs_process_name);

                    if (profile == null)
                    {
                        Events.Add(gs_process_name, new Application(new LightEventConfig { ProfileType = typeof(ApplicationProfile), GameStateType = typeof(GameState_Wrapper), Event = new GameEvent_Aurora_Wrapper() }));
                        profile = Events[gs_process_name];
                    }

                    profile.SetGameState(gs);
                }

            }
            catch (Exception e)
            {
                Global.logger.LogLine("Exception during GameStateUpdate(), error: " + e, Logging_Level.Warning);
            }
        }

        public void ResetGameState(string process)
        {
            ILightEvent profile;
            if (((profile = GetProfileFromProcess(process)) != null))
                profile.ResetGameState();
        }

        public void AddOverlayForDuration(LightEvent overlay_event, int duration, bool isUnique = true)
        {
            if (isUnique)
            {
                TimedListObject[] overlays_array = overlays.ToArray();
                bool isFound = false;

                foreach (TimedListObject obj in overlays_array)
                {
                    if (obj.item.GetType() == overlay_event.GetType())
                    {
                        isFound = true;
                        obj.AdjustDuration(duration);
                        break;
                    }
                }

                if (!isFound)
                {
                    overlays.Add(new TimedListObject(overlay_event, duration, overlays));
                }
            }
            else
            {
                overlays.Add(new TimedListObject(overlay_event, duration, overlays));
            }
        }

        public void Dispose()
        {
            updateTimer.Dispose();