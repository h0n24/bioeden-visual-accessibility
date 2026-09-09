using System;
using System.Collections.Generic;
using System.Reflection;
using Bag.Heritage.Modifiers;
using Bag.Heritage.Settings;
using UnityEngine;

namespace BioEden.NoDOF
{
    // A normal game setting: the existing UI handles arrows, controller navigation,
    // pending selection, Confirm, discard, defaults and persistence.
    public abstract class ToggleSetting : ISetting
    {
        private readonly object manager;
        private readonly FieldInfo modifiersField;
        protected ToggleSetting(object manager)
        {
            this.manager = manager;
            modifiersField = manager.GetType().GetField("settingModifiers", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("settings manager", "settingModifiers");
        }
        public abstract string Key { get; }
        public abstract string Label { get; }
        public virtual List<string> Texts => new List<string> { "lbl_off", "lbl_on" };
        public bool Enabled => true;
        public virtual int DefaultIndex => 1;
        public bool CheckPlatform() => true;
        public bool CheckExclusion() => true;
        public virtual void Apply(int index)
        {
            int normalized = index == 1 ? 1 : 0;
            // The game's dirty check uses the pending value as the fallback for a
            // missing key. Seed it after loading, so first-install Confirm works.
            // Resolve the field each time: loading replaces the Modifiers object.
            ((Modifiers)modifiersField.GetValue(manager)).Set(Key, normalized);
            ApplyValue(normalized == 1);
        }

        protected void SaveValue(int index)
        {
            ((Modifiers)modifiersField.GetValue(manager)).Set(Key, index);
        }
        protected abstract void ApplyValue(bool value);
    }

    public sealed class DepthOfFieldSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.DepthOfField";
        public DepthOfFieldSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Depth of Field";
        public override int DefaultIndex => 0;
        protected override void ApplyValue(bool value) => Runtime.SetEnabled(value);
    }

    public sealed class AntennaOutlineSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.AntennaOutline";
        public AntennaOutlineSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Antenna Range Outline";
        protected override void ApplyValue(bool value) => Runtime.SetOutline(value);
    }

    public sealed class ExtendedZoomSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.ExtendedZoom";
        public ExtendedZoomSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Extended Zoom";
        protected override void ApplyValue(bool value) => Runtime.SetExtendedZoom(value);
    }

    public sealed class MapFilterSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.MapFilter";
        public MapFilterSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Map Desaturation Filter";
        public override int DefaultIndex => 0;
        protected override void ApplyValue(bool value) => Runtime.SetFilter(value);
    }

    public sealed class SimplifyCleanLakesSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.SimplifyCleanLakes";
        public SimplifyCleanLakesSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Simplify Clean Lakes";
        public override int DefaultIndex => 0;
        protected override void ApplyValue(bool value) => FilterController.SetSimplifyCleanLakes(value);
    }

    public sealed class MapFilterHotkeySetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.MapFilterHotkey";
        public MapFilterHotkeySetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Map Filter Hotkey";
        public override List<string> Texts => new List<string> { "F1", "F2", "F3" };
        public override int DefaultIndex => 0;
        protected override void ApplyValue(bool value) { }
        public override void Apply(int index)
        {
            int normalized = Math.Max(0, Math.Min(2, index));
            SaveValue(normalized);
            Runtime.SetFilterHotkey(normalized);
        }
    }

    public sealed class ModInfoSetting : ToggleSetting
    {
        public const string SettingKey = "BioEden.NoDOF.Info";
        public ModInfoSetting(object manager) : base(manager) { }
        public override string Key => SettingKey;
        public override string Label => "Mod " + ModUpdates.InstalledVersion;
        public override List<string> Texts => new List<string> { "Installed", "Check updates (Confirm)" };
        public override int DefaultIndex => 0;
        protected override void ApplyValue(bool value) { }
        public override void Apply(int index)
        {
            SaveValue(0); // An action must never repeat on startup.
            if (index == 1) ModUpdates.Open();
        }
    }

    public static partial class Runtime
    {
        private static bool enabled;
        private static bool applied;
        public static bool Enabled => enabled;

        public static Dictionary<string, ISetting[]> Register(Dictionary<string, ISetting[]> settings, object manager)
        {
            EnsureFilterController();
            // Find the Video tab through an existing setting's stable key, avoiding
            // assumptions about localized tab names or resource ordering.
            foreach (var key in new List<string>(settings.Keys))
            {
                var entries = new List<ISetting>(settings[key]);
                bool video = false, accessibility = false;
                foreach (var setting in entries)
                {
                    if (setting?.Key == "vsync") video = true;
                    if (setting?.Key == "Color Blind") accessibility = true;
                }
                if (video && !entries.Exists(s => s?.Key == DepthOfFieldSetting.SettingKey))
                    entries.Add(new DepthOfFieldSetting(manager));
                if (accessibility)
                {
                    if (!entries.Exists(s => s?.Key == AntennaOutlineSetting.SettingKey)) entries.Add(new AntennaOutlineSetting(manager));
                    if (!entries.Exists(s => s?.Key == ExtendedZoomSetting.SettingKey)) entries.Add(new ExtendedZoomSetting(manager));
                    if (!entries.Exists(s => s?.Key == MapFilterSetting.SettingKey)) entries.Add(new MapFilterSetting(manager));
                    if (!entries.Exists(s => s?.Key == MapFilterHotkeySetting.SettingKey)) entries.Add(new MapFilterHotkeySetting(manager));
                    if (!entries.Exists(s => s?.Key == SimplifyCleanLakesSetting.SettingKey)) entries.Add(new SimplifyCleanLakesSetting(manager));
                    if (!entries.Exists(s => s?.Key == ModInfoSetting.SettingKey)) entries.Add(new ModInfoSetting(manager));
                }
                settings[key] = entries.ToArray();
            }
            Debug.Log("[BioEden.NoDOF] " + ModUpdates.InstalledVersion + ": registered visual accessibility settings.");
            return settings;
        }

        internal static void SetEnabled(bool value)
        {
            if (!applied || enabled != value)
                Debug.Log("[BioEden.NoDOF] Depth of Field applied: " + (value ? "On" : "Off"));
            userDofEnabled = value;
            enabled = value && !extendedZoom;
            applied = true;
        }
    }
}


